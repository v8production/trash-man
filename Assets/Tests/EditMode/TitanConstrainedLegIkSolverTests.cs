#if UNITY_EDITOR
using NUnit.Framework;
using System;
using UnityEngine;

public sealed class TitanConstrainedLegIkSolverTests
{
    [Test]
    public void ScrollDirection_DoesNotMatter()
    {
        TitanAggregatedInput positive = new TitanAggregatedInput { LegScrollInput = 1f };
        TitanAggregatedInput negative = new TitanAggregatedInput { LegScrollInput = -1f };

        Assert.That(TitanLegInputCommand.From(positive).LiftInput, Is.EqualTo(TitanLegInputCommand.From(negative).LiftInput));
    }

    [Test]
    public void PureVerticalLift_HasNegHipRollAndPosKneeRoll()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegIkAngles angles = InitialAngles(settings);

        TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
            leg.Hip,
            leg.Knee,
            leg.Foot,
            leg.HipBaseRotation,
            leg.KneeBaseRotation,
            leg.Foot.position + TitanGroundFrame.Up * 0.35f,
            settings,
            ref angles);

        Assert.That(settings.HipRoll.ToPhysicalAngle(result.Angles.HipRoll), Is.LessThan(0f));
        Assert.That(settings.KneeRoll.ToPhysicalAngle(result.Angles.KneeRoll), Is.GreaterThan(0f));
        Assert.That(result.PositionError, Is.LessThanOrEqualTo(0.01f));
    }

    [Test]
    public void KneeRoll_IsNotArtificiallyCappedNearFortyDegrees()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegIkAngles angles = InitialAngles(settings);

        TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
            leg.Hip,
            leg.Knee,
            leg.Foot,
            leg.HipBaseRotation,
            leg.KneeBaseRotation,
            leg.Foot.position + TitanGroundFrame.Up * 0.85f,
            settings,
            ref angles);

        Assert.That(result.Angles.KneeRoll, Is.GreaterThan(40f));
        Assert.That(result.Angles.KneeRoll, Is.InRange(1f, 179f));
    }

    [Test]
    public void JointLimits_AreAlwaysRespected()
    {
        Vector3[] targetOffsets =
        {
            new Vector3(0.2f, 0.3f, 0f),
            new Vector3(-0.4f, 0.8f, 0.1f),
            new Vector3(0.5f, -0.2f, 0.2f),
            new Vector3(1.5f, 0.2f, 0.4f),
            new Vector3(-1.5f, 1.2f, -0.4f),
        };

        foreach (Vector3 targetOffset in targetOffsets)
        {
            using TestLeg leg = new TestLeg();
            TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
            TitanLegIkAngles angles = InitialAngles(settings);

            TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
                leg.Hip,
                leg.Knee,
                leg.Foot,
                leg.HipBaseRotation,
                leg.KneeBaseRotation,
                leg.Foot.position + targetOffset,
                settings,
                ref angles);

            Assert.That(result.Angles.HipYaw, Is.InRange(0f, 100f));
            Assert.That(result.Angles.HipRoll, Is.InRange(0f, 100f));
            Assert.That(result.Angles.KneeRoll, Is.InRange(1f, 179f));
        }
    }

    [Test]
    public void BackwardConstrainedIk_UsesAlternateYawSeeds()
    {
        Vector3[] offsets =
        {
            Vector3.forward * 0.45f + Vector3.up * 0.2f,
            Vector3.back * 0.45f + Vector3.up * 0.2f,
        };

        foreach (Vector3 offset in offsets)
        {
            using TestLeg leg = new TestLeg();
            TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
            TitanLegIkAngles angles = InitialAngles(settings);

            TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
                leg.Hip,
                leg.Knee,
                leg.Foot,
                leg.HipBaseRotation,
                leg.KneeBaseRotation,
                leg.Foot.position + offset,
                settings,
                ref angles);

            Assert.That(result.Reached, Is.True, $"offset={offset} error={result.PositionError} desiredError={result.DesiredPositionError}");
            Assert.That(result.Angles.HipYaw, Is.InRange(0f, 100f));
            Assert.That(result.Angles.HipRoll, Is.InRange(0f, 100f));
            Assert.That(result.Angles.KneeRoll, Is.InRange(1f, 179f));
        }
    }

    [Test]
    public void ConstrainedLegIkSolver_AfterWarmup_AllocatesZeroManagedBytes()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegIkAngles angles = InitialAngles(settings);
        Vector3 target = leg.Foot.position + Vector3.up * 0.15f + Vector3.forward * 0.1f;

        for (int i = 0; i < 16; i++)
        {
            TitanConstrainedLegIkSolver.Solve(
                leg.Hip,
                leg.Knee,
                leg.Foot,
                leg.HipBaseRotation,
                leg.KneeBaseRotation,
                target,
                settings,
                ref angles);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++)
        {
            TitanConstrainedLegIkSolver.Solve(
                leg.Hip,
                leg.Knee,
                leg.Foot,
                leg.HipBaseRotation,
                leg.KneeBaseRotation,
                target,
                settings,
                ref angles);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.That(after - before, Is.EqualTo(0));
    }

    [Test]
    public void MathForwardKinematics_MatchesTransformHierarchy()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegIkAngles[] samples =
        {
            new TitanLegIkAngles { HipYaw = 0f, HipRoll = 0f, KneeRoll = 1f },
            new TitanLegIkAngles { HipYaw = 100f, HipRoll = 100f, KneeRoll = 179f },
            new TitanLegIkAngles { HipYaw = 50f, HipRoll = 20f, KneeRoll = 40f },
            new TitanLegIkAngles { HipYaw = 8f, HipRoll = 37f, KneeRoll = 82f },
            new TitanLegIkAngles { HipYaw = 92f, HipRoll = 12f, KneeRoll = 35f },
        };

        leg.Root.position = new Vector3(0.4f, -0.2f, 0.7f);
        leg.Root.rotation = Quaternion.Euler(0f, 35f, 0f);
        TitanLegKinematicModel model = TitanConstrainedLegIkSolver.BuildKinematicModel(leg.Root, leg.Hip, leg.Knee, leg.Foot, leg.HipBaseRotation, leg.KneeBaseRotation, 1);
        TitanRootPose rootPose = TitanRootPose.From(leg.Root);

        foreach (TitanLegIkAngles angles in samples)
        {
            TitanConstrainedLegIkSolver.ApplyPose(leg.Hip, leg.Knee, leg.HipBaseRotation, leg.KneeBaseRotation, settings, angles);
            TitanLegPoseEvaluation evaluation = TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles);
            Assert.That(Vector3.Distance(evaluation.HipPosition, leg.Hip.position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(evaluation.KneePosition, leg.Knee.position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(evaluation.FootPosition, leg.Foot.position), Is.LessThanOrEqualTo(0.0001f));
        }
    }

    [Test]
    public void AnalyticalJacobian_MatchesFiniteDifferenceReference()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegKinematicModel model = TitanConstrainedLegIkSolver.BuildKinematicModel(leg.Root, leg.Hip, leg.Knee, leg.Foot, leg.HipBaseRotation, leg.KneeBaseRotation, 1);
        TitanRootPose rootPose = TitanRootPose.From(leg.Root);
        TitanLegIkAngles angles = new TitanLegIkAngles { HipYaw = 74f, HipRoll = 31f, KneeRoll = 67f };
        TitanLegPoseEvaluation evaluation = TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles);
        TitanConstrainedLegIkSolver.ComputeAnalyticalJacobianColumns(evaluation, out Vector3 yaw, out Vector3 hipRoll, out Vector3 kneeRoll);

        AssertColumnMatchesFiniteDifference(model, rootPose, settings, angles, yaw, 0);
        AssertColumnMatchesFiniteDifference(model, rootPose, settings, angles, hipRoll, 1);
        AssertColumnMatchesFiniteDifference(model, rootPose, settings, angles, kneeRoll, 2);
    }

    [Test]
    public void TrackingReached_DoesNotEnterRecovery()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegKinematicModel model = TitanConstrainedLegIkSolver.BuildKinematicModel(leg.Root, leg.Hip, leg.Knee, leg.Foot, leg.HipBaseRotation, leg.KneeBaseRotation, 1);
        TitanRootPose rootPose = TitanRootPose.From(leg.Root);
        TitanLegIkAngles angles = InitialAngles(settings);
        Vector3 target = TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles).FootPosition;
        TitanLegSolveCache cache = default;

        TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(model, rootPose, target, settings, TitanLegIkSolveMode.Tracking, true, false, ref cache, ref angles);

        Assert.That(result.Reached, Is.True);
        Assert.That(TitanConstrainedLegIkSolver.LastTrackingIterationCount, Is.EqualTo(0));
        Assert.That(TitanConstrainedLegIkSolver.LastRecoverySeedAttemptCount, Is.EqualTo(0));
        Assert.That(TitanConstrainedLegIkSolver.LastTrackingSeedAttemptCount, Is.EqualTo(1));
    }

    [Test]
    public void CachedSolve_UnchangedQueryRunsZeroIterations()
    {
        using TestLeg leg = new TestLeg();
        TitanLegSolverSettings settings = TitanLegSolverSettings.CreateDefault();
        TitanLegKinematicModel model = TitanConstrainedLegIkSolver.BuildKinematicModel(leg.Root, leg.Hip, leg.Knee, leg.Foot, leg.HipBaseRotation, leg.KneeBaseRotation, 1);
        TitanRootPose rootPose = TitanRootPose.From(leg.Root);
        TitanLegIkAngles angles = new TitanLegIkAngles { HipYaw = 50f, HipRoll = 20f, KneeRoll = 40f };
        Vector3 target = TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles).FootPosition;
        TitanLegSolveCache cache = default;

        TitanConstrainedLegIkSolver.Solve(model, rootPose, target, settings, TitanLegIkSolveMode.Tracking, true, false, ref cache, ref angles);
        TitanConstrainedLegIkSolver.Solve(model, rootPose, target, settings, TitanLegIkSolveMode.Tracking, true, false, ref cache, ref angles);

        Assert.That(TitanConstrainedLegIkSolver.LastSolveUsedCache, Is.True);
        Assert.That(TitanConstrainedLegIkSolver.LastIterationCount, Is.EqualTo(0));
    }

    private static TitanLegIkAngles InitialAngles(in TitanLegSolverSettings settings)
    {
        return new TitanLegIkAngles
        {
            HipYaw = settings.HipYaw.NeutralAngle,
            HipRoll = settings.HipRoll.MinAngle,
            KneeRoll = settings.KneeRoll.MinAngle,
        };
    }

    private static void AssertColumnMatchesFiniteDifference(
        in TitanLegKinematicModel model,
        in TitanRootPose rootPose,
        in TitanLegSolverSettings settings,
        TitanLegIkAngles angles,
        Vector3 analyticalColumn,
        int column)
    {
        const float EpsilonDegrees = 0.01f;
        Vector3 basePosition = TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles).FootPosition;
        if (column == 0)
        {
            angles.HipYaw += EpsilonDegrees;
        }
        else if (column == 1)
        {
            angles.HipRoll += EpsilonDegrees;
        }
        else
        {
            angles.KneeRoll += EpsilonDegrees;
        }

        Vector3 finiteDifference = (TitanConstrainedLegIkSolver.EvaluatePoseMath(model, rootPose, settings, angles).FootPosition - basePosition) / (EpsilonDegrees * Mathf.Deg2Rad);
        Assert.That(Vector3.Distance(analyticalColumn, finiteDifference), Is.LessThanOrEqualTo(0.005f));
    }

    private sealed class TestLeg : System.IDisposable
    {
        private readonly GameObject root;

        public TestLeg()
        {
            root = new GameObject("LegSolverTestRoot");
            Hip = new GameObject("Hip").transform;
            Knee = new GameObject("Knee").transform;
            Foot = new GameObject("Foot").transform;
            Hip.SetParent(root.transform);
            Knee.SetParent(Hip);
            Foot.SetParent(Knee);
            Hip.localPosition = new Vector3(0f, 1.8f, 0f);
            Knee.localPosition = new Vector3(0f, -0.9f, 0f);
            Foot.localPosition = new Vector3(0f, -0.9f, 0f);
            HipBaseRotation = Hip.localRotation;
            KneeBaseRotation = Knee.localRotation;
        }

        public Transform Hip { get; }
        public Transform Knee { get; }
        public Transform Foot { get; }
        public Transform Root => root.transform;
        public Quaternion HipBaseRotation { get; }
        public Quaternion KneeBaseRotation { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
