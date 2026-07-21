using Unity.Profiling;
using UnityEngine;

public static class TitanConstrainedLegIkSolver
{
    private const float SingularDeterminant = 0.0000001f;
    private const float QueryPositionEpsilonSqr = 0.0000000001f;
    private const float QueryRotationEpsilonDegrees = 0.001f;

    private static readonly ProfilerMarker TrackingMarker = new ProfilerMarker("Titan.Leg.IK.Tracking");
    private static readonly ProfilerMarker RecoveryMarker = new ProfilerMarker("Titan.Leg.IK.Recovery");
    private static readonly ProfilerMarker CanonicalizeMarker = new ProfilerMarker("Titan.Leg.IK.Canonicalize");
    private static readonly ProfilerMarker MathFkMarker = new ProfilerMarker("Titan.Leg.IK.MathFK");
    private static readonly ProfilerMarker ApplyTransformsMarker = new ProfilerMarker("Titan.Leg.IK.ApplyTransforms");

    public static int LastSeedAttemptCount { get; private set; }
    public static int LastTrackingSeedAttemptCount { get; private set; }
    public static int LastRecoverySeedAttemptCount { get; private set; }
    public static int LastCanonicalSeedAttemptCount { get; private set; }
    public static int LastIterationCount { get; private set; }
    public static int LastTrackingIterationCount { get; private set; }
    public static int LastRecoveryIterationCount { get; private set; }
    public static int LastBoneTransformWriteCount { get; private set; }
    public static bool LastSolveUsedCache { get; private set; }
    public static TitanLegIkSolveMode LastSolveMode { get; private set; }

    public static TitanLegIkResult Solve(
        Transform hip,
        Transform knee,
        Transform foot,
        Quaternion hipBaseLocalRotation,
        Quaternion kneeBaseLocalRotation,
        Vector3 desiredWorldTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles)
    {
        TitanLegKinematicModel model = BuildKinematicModel(hip.root, hip, knee, foot, hipBaseLocalRotation, kneeBaseLocalRotation, 0);
        TitanLegSolveCache cache = default;
        TitanLegIkResult result = Solve(
            model,
            TitanRootPose.From(hip.root),
            desiredWorldTarget,
            settings,
            TitanLegIkSolveMode.CanonicalizePosture,
            previousReached: false,
            previousTargetClamped: false,
            ref cache,
            ref angles);
        ApplyPose(hip, knee, hipBaseLocalRotation, kneeBaseLocalRotation, settings, result.Angles);
        angles = result.Angles;
        return result;
    }

    public static TitanLegIkResult Solve(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        in TitanLegSolverSettings settings,
        TitanLegIkSolveMode mode,
        bool previousReached,
        bool previousTargetClamped,
        ref TitanLegSolveCache cache,
        ref TitanLegIkAngles angles)
    {
        ResetCounters(mode);
        TitanLegSolverSettings resolvedSettings = ResolveSettings(settings);
        if (!model.Valid)
        {
            return default;
        }

        ClampAngles(ref angles, resolvedSettings);
        TitanLegPoseEvaluation initialEvaluation = EvaluatePoseMath(model, rootPose, resolvedSettings, angles);
        Vector3 reachableTarget = ClampTargetToReach(
            initialEvaluation.HipPosition,
            initialEvaluation.FootPosition,
            desiredWorldTarget,
            model.UpperLength,
            model.LowerLength,
            resolvedSettings.ReachMargin);

        bool cacheMatches = cache.Valid
            && cache.KinematicModelVersion == model.Version
            && (cache.Target - desiredWorldTarget).sqrMagnitude <= QueryPositionEpsilonSqr
            && (cache.RootPosition - rootPose.Position).sqrMagnitude <= QueryPositionEpsilonSqr
            && Quaternion.Angle(cache.RootRotation, rootPose.Rotation) <= QueryRotationEpsilonDegrees
            && cache.Result.Reached
            && !cache.Result.TargetWasClamped
            && previousReached
            && !previousTargetClamped;
        if (cacheMatches)
        {
            LastSolveUsedCache = true;
            angles = cache.Angles;
            return cache.Result;
        }

        TitanLegIkResult result;
        if (mode == TitanLegIkSolveMode.Tracking)
        {
            using (TrackingMarker.Auto())
            {
                result = SolveTracking(model, rootPose, desiredWorldTarget, reachableTarget, resolvedSettings, ref angles);
            }
        }
        else if (mode == TitanLegIkSolveMode.Recovery)
        {
            using (RecoveryMarker.Auto())
            {
                result = SolveRecovery(model, rootPose, desiredWorldTarget, reachableTarget, resolvedSettings, ref angles, hardRecovery: false);
            }
        }
        else
        {
            using (CanonicalizeMarker.Auto())
            {
                result = SolveCanonicalize(model, rootPose, desiredWorldTarget, reachableTarget, resolvedSettings, ref angles);
            }
        }

        cache = new TitanLegSolveCache
        {
            Valid = true,
            KinematicModelVersion = model.Version,
            Target = desiredWorldTarget,
            RootPosition = rootPose.Position,
            RootRotation = rootPose.Rotation,
            Angles = result.Angles,
            Result = result,
        };

        return result;
    }

    public static TitanLegKinematicModel BuildKinematicModel(
        Transform movementRoot,
        Transform hip,
        Transform knee,
        Transform foot,
        Quaternion hipBaseLocalRotation,
        Quaternion kneeBaseLocalRotation,
        int version)
    {
        if (movementRoot == null || hip == null || knee == null || foot == null || !IsAncestorOrSelf(movementRoot, hip) || !IsAncestorOrSelf(hip, knee) || !IsAncestorOrSelf(knee, foot))
        {
            return default;
        }

        Matrix4x4 rootWorldToLocal = movementRoot.worldToLocalMatrix;
        Matrix4x4 rootToHipParent = hip.parent == null ? rootWorldToLocal : rootWorldToLocal * hip.parent.localToWorldMatrix;
        Matrix4x4 hipToKneeParent = hip.worldToLocalMatrix * knee.parent.localToWorldMatrix;
        Matrix4x4 kneeToFoot = knee.worldToLocalMatrix * foot.localToWorldMatrix;
        Vector3 hipRootLocal = rootWorldToLocal.MultiplyPoint3x4(hip.position);
        float upperLength = Mathf.Max(0.001f, Vector3.Distance(hip.position, knee.position));
        float lowerLength = Mathf.Max(0.001f, Vector3.Distance(knee.position, foot.position));

        return new TitanLegKinematicModel
        {
            Valid = true,
            RootToHipParent = rootToHipParent,
            HipLocalPosition = hip.localPosition,
            HipLocalScale = hip.localScale,
            HipBaseLocalRotation = hipBaseLocalRotation,
            HipToKneeParentStatic = hipToKneeParent,
            KneeLocalPosition = knee.localPosition,
            KneeLocalScale = knee.localScale,
            KneeBaseLocalRotation = kneeBaseLocalRotation,
            KneeToFootStatic = kneeToFoot,
            UpperLength = upperLength,
            LowerLength = lowerLength,
            MaxReach = upperLength + lowerLength,
            HipOffsetRootLocal = hipRootLocal,
            Version = version,
        };
    }

    public static TitanLegPoseEvaluation EvaluatePoseMath(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles angles)
    {
        using (MathFkMarker.Auto())
        {
            TitanLegSolverSettings resolvedSettings = ResolveSettings(settings);
            TitanLegIkAngles clamped = angles;
            ClampAngles(ref clamped, resolvedSettings);

            float physicalYaw = resolvedSettings.HipYaw.ToPhysicalAngle(clamped.HipYaw);
            float physicalHipRoll = resolvedSettings.HipRoll.ToPhysicalAngle(clamped.HipRoll);
            float physicalKneeRoll = resolvedSettings.KneeRoll.ToPhysicalAngle(clamped.KneeRoll);
            Quaternion yawRotation = Quaternion.AngleAxis(physicalYaw, resolvedSettings.HipYaw.LocalAxis);
            Quaternion hipRollRotation = Quaternion.AngleAxis(physicalHipRoll, resolvedSettings.HipRoll.LocalAxis);
            Quaternion kneeRollRotation = Quaternion.AngleAxis(physicalKneeRoll, resolvedSettings.KneeRoll.LocalAxis);
            Quaternion hipLocalRotation = model.HipBaseLocalRotation * yawRotation * hipRollRotation;
            Quaternion kneeLocalRotation = model.KneeBaseLocalRotation * kneeRollRotation;

            Matrix4x4 rootWorld = Matrix4x4.TRS(rootPose.Position, rootPose.Rotation, rootPose.Scale);
            Matrix4x4 hipParentWorld = rootWorld * model.RootToHipParent;
            Matrix4x4 hipBaseWorld = hipParentWorld * Matrix4x4.TRS(model.HipLocalPosition, model.HipBaseLocalRotation, model.HipLocalScale);
            Matrix4x4 hipWorld = hipParentWorld * Matrix4x4.TRS(model.HipLocalPosition, hipLocalRotation, model.HipLocalScale);
            Matrix4x4 kneeParentWorld = hipWorld * model.HipToKneeParentStatic;
            Matrix4x4 kneeBaseWorld = kneeParentWorld * Matrix4x4.TRS(model.KneeLocalPosition, model.KneeBaseLocalRotation, model.KneeLocalScale);
            Matrix4x4 kneeWorld = kneeParentWorld * Matrix4x4.TRS(model.KneeLocalPosition, kneeLocalRotation, model.KneeLocalScale);
            Matrix4x4 footWorld = kneeWorld * model.KneeToFootStatic;

            Quaternion hipBaseWorldRotation = hipParentWorld.rotation * model.HipBaseLocalRotation;
            Quaternion hipWorldRotation = hipParentWorld.rotation * hipLocalRotation;
            Quaternion kneeBaseWorldRotation = kneeParentWorld.rotation * model.KneeBaseLocalRotation;
            float yawSign = resolvedSettings.HipYaw.AxisSign >= 0f ? 1f : -1f;
            float hipRollSign = resolvedSettings.HipRoll.AxisSign >= 0f ? 1f : -1f;
            float kneeRollSign = resolvedSettings.KneeRoll.AxisSign >= 0f ? 1f : -1f;

            return new TitanLegPoseEvaluation
            {
                HipPosition = hipWorld.MultiplyPoint3x4(Vector3.zero),
                KneePosition = kneeWorld.MultiplyPoint3x4(Vector3.zero),
                FootPosition = footWorld.MultiplyPoint3x4(Vector3.zero),
                HipBaseWorldRotation = hipBaseWorld.rotation,
                HipWorldRotation = hipWorldRotation,
                KneeBaseWorldRotation = kneeBaseWorld.rotation,
                HipYawAxisWorld = (hipBaseWorldRotation * resolvedSettings.HipYaw.LocalAxis).normalized * yawSign,
                HipRollAxisWorld = (hipBaseWorldRotation * yawRotation * resolvedSettings.HipRoll.LocalAxis).normalized * hipRollSign,
                KneeRollAxisWorld = (kneeBaseWorldRotation * resolvedSettings.KneeRoll.LocalAxis).normalized * kneeRollSign,
            };
        }
    }

    public static void ComputeAnalyticalJacobianColumns(
        in TitanLegPoseEvaluation evaluation,
        out Vector3 column0,
        out Vector3 column1,
        out Vector3 column2)
    {
        column0 = Vector3.Cross(evaluation.HipYawAxisWorld, evaluation.FootPosition - evaluation.HipPosition);
        column1 = Vector3.Cross(evaluation.HipRollAxisWorld, evaluation.FootPosition - evaluation.HipPosition);
        column2 = Vector3.Cross(evaluation.KneeRollAxisWorld, evaluation.FootPosition - evaluation.KneePosition);
    }

    public static void ApplyPose(
        Transform hip,
        Transform knee,
        Quaternion hipBaseLocalRotation,
        Quaternion kneeBaseLocalRotation,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles angles)
    {
        using (ApplyTransformsMarker.Auto())
        {
            TitanLegSolverSettings resolvedSettings = ResolveSettings(settings);
            float physicalYaw = resolvedSettings.HipYaw.ToPhysicalAngle(angles.HipYaw);
            float physicalHipRoll = resolvedSettings.HipRoll.ToPhysicalAngle(angles.HipRoll);
            float physicalKneeRoll = resolvedSettings.KneeRoll.ToPhysicalAngle(angles.KneeRoll);

            hip.localRotation = hipBaseLocalRotation
                * Quaternion.AngleAxis(physicalYaw, resolvedSettings.HipYaw.LocalAxis)
                * Quaternion.AngleAxis(physicalHipRoll, resolvedSettings.HipRoll.LocalAxis);
            knee.localRotation = kneeBaseLocalRotation
                * Quaternion.AngleAxis(physicalKneeRoll, resolvedSettings.KneeRoll.LocalAxis);
            LastBoneTransformWriteCount += 2;
        }
    }

    private static TitanLegIkResult SolveTracking(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles)
    {
        LastSeedAttemptCount++;
        LastTrackingSeedAttemptCount++;
        TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, angles);
        float initialError = Vector3.Distance(evaluation.FootPosition, reachableTarget);
        if (initialError <= settings.PositionTolerance)
        {
            return CreateResult(angles, desiredWorldTarget, reachableTarget, evaluation.FootPosition, settings);
        }

        TitanLegIkAngles candidate = angles;
        IterateFromSeed(model, rootPose, reachableTarget, settings, ref candidate, Mathf.Min(Mathf.Max(settings.Iterations, 1), 5), tracking: true);
        TitanLegPoseEvaluation finalEvaluation = EvaluatePoseMath(model, rootPose, settings, candidate);
        angles = candidate;
        return CreateResult(candidate, desiredWorldTarget, reachableTarget, finalEvaluation.FootPosition, settings);
    }

    private static TitanLegIkResult SolveRecovery(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles,
        bool hardRecovery)
    {
        TitanLegIkAngles previous = angles;
        TitanLegIkAngles best = angles;
        float bestError = float.PositiveInfinity;
        bool hasBest = false;

        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.Recovery);
        if (bestError <= settings.PositionTolerance)
        {
            angles = best;
            return CreateResultFromAngles(model, rootPose, desiredWorldTarget, reachableTarget, settings, best);
        }

        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, CreateAnalyticalSeed(model, rootPose, reachableTarget, settings, previous), previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.Recovery);
        if (bestError <= settings.PositionTolerance)
        {
            angles = best;
            return CreateResultFromAngles(model, rootPose, desiredWorldTarget, reachableTarget, settings, best);
        }

        TitanLegIkAngles yaw0 = CreateYawCoverageSeed(settings, 0f, 5f, 10f);
        TitanLegIkAngles yaw1 = CreateYawCoverageSeed(settings, 0.25f, 12f, 24f);
        TitanLegIkAngles yaw2 = CreateYawCoverageSeed(settings, 0.5f, 16f, 32f);
        TitanLegIkAngles yaw3 = CreateYawCoverageSeed(settings, 0.75f, 12f, 24f);
        TitanLegIkAngles yaw4 = CreateYawCoverageSeed(settings, 1f, 5f, 10f);
        SortYawSeedsByError(model, rootPose, reachableTarget, settings, ref yaw0, ref yaw1, ref yaw2, ref yaw3, ref yaw4);
        int yawBudget = hardRecovery ? 5 : 3;
        TryRecoveryYawSeed(0, yawBudget, yaw0, model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, ref best, ref bestError, ref hasBest);
        TryRecoveryYawSeed(1, yawBudget, yaw1, model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, ref best, ref bestError, ref hasBest);
        TryRecoveryYawSeed(2, yawBudget, yaw2, model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, ref best, ref bestError, ref hasBest);
        TryRecoveryYawSeed(3, yawBudget, yaw3, model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, ref best, ref bestError, ref hasBest);
        TryRecoveryYawSeed(4, yawBudget, yaw4, model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, ref best, ref bestError, ref hasBest);

        angles = best;
        return CreateResultFromAngles(model, rootPose, desiredWorldTarget, reachableTarget, settings, best);
    }

    private static void TryRecoveryYawSeed(
        int index,
        int yawBudget,
        TitanLegIkAngles seed,
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles previous,
        ref TitanLegIkAngles best,
        ref float bestError,
        ref bool hasBest)
    {
        if (index >= yawBudget || bestError <= settings.PositionTolerance)
        {
            return;
        }

        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, seed, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.Recovery);
    }

    private static TitanLegIkResult SolveCanonicalize(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles)
    {
        TitanLegIkAngles previous = angles;
        TitanLegIkAngles best = angles;
        float bestError = float.PositiveInfinity;
        bool hasBest = false;
        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, previous, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.CanonicalizePosture);
        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, new TitanLegIkAngles { HipYaw = settings.HipYaw.Clamp(previous.HipYaw), HipRoll = settings.HipRoll.MinAngle, KneeRoll = settings.KneeRoll.MinAngle }, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.CanonicalizePosture);
        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, new TitanLegIkAngles { HipYaw = settings.HipYaw.Clamp(previous.HipYaw), HipRoll = settings.HipRoll.Clamp(settings.HipRoll.MinAngle + 1.5f), KneeRoll = settings.KneeRoll.MinAngle }, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.CanonicalizePosture);
        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, CreateAnalyticalSeed(model, rootPose, reachableTarget, settings, previous), previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.CanonicalizePosture);
        EvaluateIteratedCandidate(model, rootPose, desiredWorldTarget, reachableTarget, settings, new TitanLegIkAngles { HipYaw = previous.HipYaw, HipRoll = settings.HipRoll.Clamp(20f), KneeRoll = settings.KneeRoll.Clamp(40f) }, previous, settings.Iterations, ref best, ref bestError, ref hasBest, TitanLegIkSolveMode.CanonicalizePosture);
        if (bestError > settings.PositionTolerance)
        {
            TitanLegIkResult recovery = SolveRecovery(model, rootPose, desiredWorldTarget, reachableTarget, settings, ref best, hardRecovery: true);
            angles = recovery.Angles;
            return recovery;
        }

        angles = best;
        return CreateResultFromAngles(model, rootPose, desiredWorldTarget, reachableTarget, settings, best);
    }

    private static void EvaluateIteratedCandidate(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        TitanLegIkAngles seed,
        in TitanLegIkAngles previous,
        int iterations,
        ref TitanLegIkAngles best,
        ref float bestError,
        ref bool hasBest,
        TitanLegIkSolveMode mode)
    {
        LastSeedAttemptCount++;
        if (mode == TitanLegIkSolveMode.Recovery)
        {
            LastRecoverySeedAttemptCount++;
        }
        else if (mode == TitanLegIkSolveMode.CanonicalizePosture)
        {
            LastCanonicalSeedAttemptCount++;
        }

        TitanLegIkAngles candidate = seed;
        IterateFromSeed(model, rootPose, reachableTarget, settings, ref candidate, iterations, tracking: false);
        TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, candidate);
        float error = Vector3.Distance(evaluation.FootPosition, reachableTarget);
        if (!hasBest || PreferCandidate(candidate, error, best, bestError, previous, settings))
        {
            best = candidate;
            bestError = error;
            hasBest = true;
        }
    }

    private static void IterateFromSeed(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles,
        int iterations,
        bool tracking)
    {
        ClampAngles(ref angles, settings);
        SeedSingularityIfNeeded(model, rootPose, reachableTarget, settings, ref angles);
        for (int i = 0; i < iterations; i++)
        {
            LastIterationCount++;
            if (tracking)
            {
                LastTrackingIterationCount++;
            }
            else
            {
                LastRecoveryIterationCount++;
            }

            TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, angles);
            Vector3 error = reachableTarget - evaluation.FootPosition;
            float currentErrorSqr = error.sqrMagnitude;
            if (currentErrorSqr <= settings.PositionTolerance * settings.PositionTolerance)
            {
                break;
            }

            ComputeAnalyticalJacobianColumns(evaluation, out Vector3 column0, out Vector3 column1, out Vector3 column2);
            if (!TryComputeDampedDelta(column0, column1, column2, error, settings.Damping, out Vector3 deltaRadians))
            {
                break;
            }

            Vector3 deltaDegrees = deltaRadians * Mathf.Rad2Deg;
            deltaDegrees.x = ProjectBoundedDelta(angles.HipYaw, deltaDegrees.x, settings.HipYaw);
            deltaDegrees.y = ProjectBoundedDelta(angles.HipRoll, deltaDegrees.y, settings.HipRoll);
            deltaDegrees.z = ProjectBoundedDelta(angles.KneeRoll, deltaDegrees.z, settings.KneeRoll);
            deltaDegrees.x = Mathf.Clamp(deltaDegrees.x, -settings.MaxStepDegrees, settings.MaxStepDegrees);
            deltaDegrees.y = Mathf.Clamp(deltaDegrees.y, -settings.MaxStepDegrees, settings.MaxStepDegrees);
            deltaDegrees.z = Mathf.Clamp(deltaDegrees.z, -settings.MaxStepDegrees, settings.MaxStepDegrees);
            if (!TryAcceptStep(model, rootPose, reachableTarget, settings, angles, deltaDegrees, currentErrorSqr, out TitanLegIkAngles acceptedAngles))
            {
                break;
            }

            angles = acceptedAngles;
        }
    }

    private static bool TryAcceptStep(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles currentAngles,
        Vector3 deltaDegrees,
        float currentErrorSqr,
        out TitanLegIkAngles acceptedAngles)
    {
        const int ScaleCount = 3;
        acceptedAngles = currentAngles;
        for (int i = 0; i < ScaleCount; i++)
        {
            float scale = i == 0 ? 1f : i == 1 ? 0.5f : 0.25f;
            TitanLegIkAngles candidate = new TitanLegIkAngles
            {
                HipYaw = settings.HipYaw.Clamp(currentAngles.HipYaw + deltaDegrees.x * scale),
                HipRoll = settings.HipRoll.Clamp(currentAngles.HipRoll + deltaDegrees.y * scale),
                KneeRoll = settings.KneeRoll.Clamp(currentAngles.KneeRoll + deltaDegrees.z * scale),
            };
            TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, candidate);
            if ((reachableTarget - evaluation.FootPosition).sqrMagnitude < currentErrorSqr - 0.00000001f)
            {
                acceptedAngles = candidate;
                return true;
            }
        }

        return false;
    }

    private static float ProjectBoundedDelta(float angle, float delta, in TitanJointConstraint constraint)
    {
        const float LimitEpsilon = 0.0001f;
        if (angle <= constraint.MinAngle + LimitEpsilon && delta < 0f)
        {
            return 0f;
        }

        if (angle >= constraint.MaxAngle - LimitEpsilon && delta > 0f)
        {
            return 0f;
        }

        return delta;
    }

    private static TitanLegIkResult CreateResultFromAngles(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles angles)
    {
        TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, angles);
        return CreateResult(angles, desiredWorldTarget, reachableTarget, evaluation.FootPosition, settings);
    }

    private static TitanLegIkResult CreateResult(
        in TitanLegIkAngles angles,
        Vector3 desiredWorldTarget,
        Vector3 reachableTarget,
        Vector3 actualPosition,
        in TitanLegSolverSettings settings)
    {
        float positionError = Vector3.Distance(actualPosition, reachableTarget);
        return new TitanLegIkResult
        {
            Angles = angles,
            DesiredTarget = desiredWorldTarget,
            ReachableTarget = reachableTarget,
            ActualPosition = actualPosition,
            PositionError = positionError,
            TargetWasClamped = Vector3.Distance(desiredWorldTarget, reachableTarget) > 0.0001f,
            DesiredPositionError = Vector3.Distance(actualPosition, desiredWorldTarget),
            Reached = positionError <= settings.PositionTolerance,
        };
    }

    private static bool PreferCandidate(
        in TitanLegIkAngles candidate,
        float candidateError,
        in TitanLegIkAngles best,
        float bestError,
        in TitanLegIkAngles previous,
        in TitanLegSolverSettings settings)
    {
        bool candidateReached = candidateError <= settings.PositionTolerance;
        bool bestReached = bestError <= settings.PositionTolerance;
        float errorTieEpsilon = Mathf.Max(settings.PositionTolerance, 0.0005f);
        if (candidateReached != bestReached)
        {
            return candidateReached;
        }

        if (candidateError < bestError - errorTieEpsilon)
        {
            return true;
        }

        if (Mathf.Abs(candidateError - bestError) > errorTieEpsilon)
        {
            return false;
        }

        float candidateKneeFlex = NormalizedConstraintOffset(settings.KneeRoll, candidate.KneeRoll);
        float bestKneeFlex = NormalizedConstraintOffset(settings.KneeRoll, best.KneeRoll);
        if (candidateKneeFlex < bestKneeFlex - 0.0001f)
        {
            return true;
        }

        if (candidateKneeFlex > bestKneeFlex + 0.0001f)
        {
            return false;
        }

        float candidateHipCost = Mathf.Abs(candidate.HipRoll - settings.HipRoll.MinAngle);
        float bestHipCost = Mathf.Abs(best.HipRoll - settings.HipRoll.MinAngle);
        if (candidateHipCost < bestHipCost - 0.0001f)
        {
            return true;
        }

        if (candidateHipCost > bestHipCost + 0.0001f)
        {
            return false;
        }

        return AngleDelta(previous, candidate) < AngleDelta(previous, best);
    }

    private static void SortYawSeedsByError(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles a,
        ref TitanLegIkAngles b,
        ref TitanLegIkAngles c,
        ref TitanLegIkAngles d,
        ref TitanLegIkAngles e)
    {
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref a, ref b);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref b, ref c);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref c, ref d);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref d, ref e);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref a, ref b);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref b, ref c);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref c, ref d);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref a, ref b);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref b, ref c);
        SwapIfWorse(model, rootPose, reachableTarget, settings, ref a, ref b);
    }

    private static void SwapIfWorse(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles a,
        ref TitanLegIkAngles b)
    {
        float errorA = (EvaluatePoseMath(model, rootPose, settings, a).FootPosition - reachableTarget).sqrMagnitude;
        float errorB = (EvaluatePoseMath(model, rootPose, settings, b).FootPosition - reachableTarget).sqrMagnitude;
        if (errorB < errorA)
        {
            TitanLegIkAngles temp = a;
            a = b;
            b = temp;
        }
    }

    private static TitanLegIkAngles CreateYawCoverageSeed(in TitanLegSolverSettings settings, float yawT, float hipRollOffset, float kneeRollOffset)
    {
        return new TitanLegIkAngles
        {
            HipYaw = settings.HipYaw.Clamp(Mathf.Lerp(settings.HipYaw.MinAngle, settings.HipYaw.MaxAngle, yawT)),
            HipRoll = settings.HipRoll.Clamp(settings.HipRoll.MinAngle + hipRollOffset),
            KneeRoll = settings.KneeRoll.Clamp(settings.KneeRoll.MinAngle + kneeRollOffset),
        };
    }

    private static TitanLegIkAngles CreateAnalyticalSeed(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        in TitanLegIkAngles previousAngles)
    {
        TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, previousAngles);
        float distance = Mathf.Clamp(
            Vector3.Distance(evaluation.HipPosition, reachableTarget),
            Mathf.Abs(model.UpperLength - model.LowerLength) + settings.ReachMargin,
            model.UpperLength + model.LowerLength - settings.ReachMargin);
        float kneeFlexRadians = Mathf.Acos(Mathf.Clamp((distance * distance - model.UpperLength * model.UpperLength - model.LowerLength * model.LowerLength) / (2f * model.UpperLength * model.LowerLength), -1f, 1f));
        float hipFlexRadians = Mathf.Acos(Mathf.Clamp((model.UpperLength * model.UpperLength + distance * distance - model.LowerLength * model.LowerLength) / (2f * model.UpperLength * distance), -1f, 1f));
        float hipSign = settings.HipRoll.AxisSign >= 0f ? 1f : -1f;
        float kneeSign = settings.KneeRoll.AxisSign >= 0f ? 1f : -1f;
        return new TitanLegIkAngles
        {
            HipYaw = previousAngles.HipYaw,
            HipRoll = settings.HipRoll.Clamp(settings.HipRoll.NeutralAngle + (-hipFlexRadians * Mathf.Rad2Deg) / hipSign),
            KneeRoll = settings.KneeRoll.Clamp(settings.KneeRoll.NeutralAngle + (kneeFlexRadians * Mathf.Rad2Deg) / kneeSign),
        };
    }

    private static void SeedSingularityIfNeeded(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        Vector3 reachableTarget,
        in TitanLegSolverSettings settings,
        ref TitanLegIkAngles angles)
    {
        TitanLegPoseEvaluation evaluation = EvaluatePoseMath(model, rootPose, settings, angles);
        float currentDistance = Vector3.Distance(evaluation.HipPosition, evaluation.FootPosition);
        float targetDistance = Vector3.Distance(evaluation.HipPosition, reachableTarget);
        if (angles.HipRoll <= settings.HipRoll.MinAngle + 0.1f && angles.KneeRoll <= settings.KneeRoll.MinAngle + 0.1f)
        {
            return;
        }

        if (currentDistance <= targetDistance + 0.001f || angles.KneeRoll > settings.KneeRoll.MinAngle + 0.1f)
        {
            return;
        }

        angles.HipRoll = settings.HipRoll.Clamp(settings.HipRoll.MinAngle + 1.5f);
        angles.KneeRoll = settings.KneeRoll.Clamp(settings.KneeRoll.MinAngle + 3f);
    }

    private static Vector3 ClampTargetToReach(Vector3 hipPosition, Vector3 currentFootPosition, Vector3 desiredWorldTarget, float upperLength, float lowerLength, float reachMargin)
    {
        Vector3 hipToTarget = desiredWorldTarget - hipPosition;
        Vector3 direction = hipToTarget.sqrMagnitude > 0.0001f ? hipToTarget.normalized : currentFootPosition - hipPosition;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.down;
        }
        else
        {
            direction.Normalize();
        }

        float minReach = Mathf.Abs(upperLength - lowerLength) + reachMargin;
        float maxReach = Mathf.Max(minReach, upperLength + lowerLength - reachMargin);
        return hipPosition + direction * Mathf.Clamp(hipToTarget.magnitude, minReach, maxReach);
    }

    private static bool TryComputeDampedDelta(Vector3 j0, Vector3 j1, Vector3 j2, Vector3 error, float damping, out Vector3 delta)
    {
        float lambda = Mathf.Max(0.0001f, damping);
        float a00 = Vector3.Dot(j0, j0) + lambda * lambda;
        float a01 = Vector3.Dot(j0, j1);
        float a02 = Vector3.Dot(j0, j2);
        float a11 = Vector3.Dot(j1, j1) + lambda * lambda;
        float a12 = Vector3.Dot(j1, j2);
        float a22 = Vector3.Dot(j2, j2) + lambda * lambda;
        float b0 = Vector3.Dot(j0, error);
        float b1 = Vector3.Dot(j1, error);
        float b2 = Vector3.Dot(j2, error);
        return SolveSymmetric3x3(a00, a01, a02, a11, a12, a22, b0, b1, b2, out delta);
    }

    private static bool SolveSymmetric3x3(float a00, float a01, float a02, float a11, float a12, float a22, float b0, float b1, float b2, out Vector3 x)
    {
        float det = a00 * (a11 * a22 - a12 * a12) - a01 * (a01 * a22 - a12 * a02) + a02 * (a01 * a12 - a11 * a02);
        if (Mathf.Abs(det) <= SingularDeterminant)
        {
            x = default;
            return false;
        }

        float invDet = 1f / det;
        x = new Vector3(
            (b0 * (a11 * a22 - a12 * a12) - a01 * (b1 * a22 - a12 * b2) + a02 * (b1 * a12 - a11 * b2)) * invDet,
            (a00 * (b1 * a22 - a12 * b2) - b0 * (a01 * a22 - a12 * a02) + a02 * (a01 * b2 - b1 * a02)) * invDet,
            (a00 * (a11 * b2 - b1 * a12) - a01 * (a01 * b2 - b1 * a02) + b0 * (a01 * a12 - a11 * a02)) * invDet);
        return float.IsFinite(x.x) && float.IsFinite(x.y) && float.IsFinite(x.z);
    }

    private static TitanLegSolverSettings ResolveSettings(in TitanLegSolverSettings settings)
    {
        TitanLegSolverSettings resolved = settings;
        if (resolved.Iterations <= 0 || resolved.HipYaw.LocalAxis.sqrMagnitude < 0.0001f)
        {
            resolved = TitanLegSolverSettings.CreateDefault();
        }

        if (resolved.HipRoll.LocalAxis.sqrMagnitude < 0.0001f)
        {
            resolved.HipRoll.LocalAxis = Vector3.forward;
        }

        if (resolved.KneeRoll.LocalAxis.sqrMagnitude < 0.0001f)
        {
            resolved.KneeRoll.LocalAxis = Vector3.forward;
        }

        resolved.HipYaw.LocalAxis = resolved.HipYaw.LocalAxis.normalized;
        resolved.HipRoll.LocalAxis = resolved.HipRoll.LocalAxis.normalized;
        resolved.KneeRoll.LocalAxis = resolved.KneeRoll.LocalAxis.normalized;
        return resolved;
    }

    private static void ClampAngles(ref TitanLegIkAngles angles, in TitanLegSolverSettings settings)
    {
        angles.HipYaw = settings.HipYaw.Clamp(angles.HipYaw);
        angles.HipRoll = settings.HipRoll.Clamp(angles.HipRoll);
        angles.KneeRoll = settings.KneeRoll.Clamp(angles.KneeRoll);
    }

    private static float NormalizedConstraintOffset(in TitanJointConstraint constraint, float value)
    {
        float range = Mathf.Max(0.0001f, constraint.MaxAngle - constraint.MinAngle);
        return (constraint.Clamp(value) - constraint.MinAngle) / range;
    }

    private static float AngleDelta(in TitanLegIkAngles a, in TitanLegIkAngles b)
    {
        return Mathf.Abs(a.HipYaw - b.HipYaw) + Mathf.Abs(a.HipRoll - b.HipRoll) + Mathf.Abs(a.KneeRoll - b.KneeRoll);
    }

    private static bool IsAncestorOrSelf(Transform ancestor, Transform child)
    {
        for (Transform current = child; current != null; current = current.parent)
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetCounters(TitanLegIkSolveMode mode)
    {
        LastSeedAttemptCount = 0;
        LastTrackingSeedAttemptCount = 0;
        LastRecoverySeedAttemptCount = 0;
        LastCanonicalSeedAttemptCount = 0;
        LastIterationCount = 0;
        LastTrackingIterationCount = 0;
        LastRecoveryIterationCount = 0;
        LastBoneTransformWriteCount = 0;
        LastSolveUsedCache = false;
        LastSolveMode = mode;
    }
}
