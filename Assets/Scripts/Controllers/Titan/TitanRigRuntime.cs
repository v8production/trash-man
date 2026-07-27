using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public sealed class TitanRigRuntime : MonoBehaviour
{
    private const float HorizontalInputEpsilonSqr = 0.00000001f;
    private const float RootQueryEpsilonSqr = 0.00000001f;
    private const float StrictSwingTargetEpsilon = 0.00001f;
    private static readonly ProfilerMarker TitanLegTickMarker = new ProfilerMarker("Titan.Leg.Tick");
    private static readonly ProfilerMarker TitanLegActiveRootGeometryMarker = new ProfilerMarker("Titan.Leg.ActiveRootGeometry");
    private static readonly ProfilerMarker TitanLegRootConstraintFallbackMarker = new ProfilerMarker("Titan.Leg.RootFallback");
    private static readonly ProfilerMarker TitanLegFinalIkMarker = new ProfilerMarker("Titan.Leg.FinalIK");
    private static readonly ProfilerMarker TitanLegTouchdownContactMarker = new ProfilerMarker("Titan.Leg.TouchdownContact");
    private static readonly ProfilerMarker TitanLegPhysicsSyncMarker = new ProfilerMarker("Titan.Leg.PhysicsSync");

    [Header("Optional Bone Overrides")]
    [SerializeField] private Transform mechaRoot;
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform leftElbow;
    [SerializeField] private Transform rightShoulder;
    [SerializeField] private Transform rightElbow;
    [SerializeField] private Transform leftHip;
    [SerializeField] private Transform leftKnee;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightHip;
    [SerializeField] private Transform rightKnee;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform drill;
    [SerializeField] private Transform claw;
    private FootAttachmentController leftFootAttachment;
    private FootAttachmentController rightFootAttachment;

    [Header("Torso")]
    [SerializeField] private float waistYaw;

    [Header("Arm States")]
    [SerializeField] private TitanArmControlState leftArm;
    [SerializeField] private TitanArmControlState rightArm;

    [Header("Leg States")]
    [SerializeField] private TitanLegControlState leftLeg;
    [SerializeField] private TitanLegControlState rightLeg;

    [Header("Leg Input")]
    [SerializeField] private float footMoveSensitivity = 0.008f;
    [SerializeField] private float footLiftRiseSpeed = 7.2f;
    [SerializeField] private float footLiftSmoothTime = 0.12f;
    [SerializeField] private float footLiftFallAcceleration = 2.4f;
    [SerializeField] private float footLiftMaxFallSpeed = 1.2f;
    [SerializeField] private float maxFootLift = 1.2f;

    [Header("Leg Support")]
    [SerializeField] private float supportSwitchHysteresis = 0.01f;
    [SerializeField] private float supportContactTolerance = 0.015f;
    [SerializeField] private float groundingStableTime = 0.05f;
    [SerializeField] private float groundLossGraceTime = 0.10f;

    [Header("Grounded Stance Motor")]
    [SerializeField] private float groundedRootMoveSpeed = 1.5f;
    [SerializeField] private float stanceReachSafetyMargin = 0.0005f;
    [SerializeField] private float liftSnapEpsilon = 0.003f;
    [SerializeField] private float touchdownLiftTolerance = 0.015f;
    [SerializeField] private float touchdownSolveTolerance = 0.035f;
    [SerializeField] private float touchdownSoleTolerance = 0.01f;
    [SerializeField] private float touchdownPlanarTolerance = 0.02f;
    [SerializeField] private float legTargetDiscontinuityThreshold = 0.15f;
    [SerializeField] private float plantedSoleClearance = 0.002f;
    [SerializeField] private float touchdownPenetrationTolerance = 0.002f;
    [SerializeField] private float touchdownMaximumSoleGap = 0.008f;
    [SerializeField] private float touchdownRecoveryDelay = 0.08f;
    [SerializeField] private float touchdownRecoveryTargetTolerance = 0.12f;
    [SerializeField] private float plantedFootTargetTolerance = 0.004f;
    [SerializeField] private float plantedPenetrationTolerance = 0.002f;
    [SerializeField] private float activeSwingInputSolveTolerance = 0.00005f;
    [SerializeField] private float footGroundedFeedbackMinimumLift = 0.03f;
    [SerializeField] private float footGroundedFeedbackMinimumMove = 0.02f;

    [Header("Leg IK")]
    [SerializeField] private TitanLegSolverSettings leftLegSolverSettings;
    [SerializeField] private TitanLegSolverSettings rightLegSolverSettings;

    [Header("Leg Debug")]
    [SerializeField] private bool drawLegDebug;
    [SerializeField] private float solveWarningThreshold = 0.03f;

    private Animator animator;

    private Quaternion leftShoulderBaseRotation;
    private Quaternion leftElbowBaseRotation;
    private Quaternion rightShoulderBaseRotation;
    private Quaternion rightElbowBaseRotation;
    private Quaternion leftHipBaseRotation;
    private Quaternion leftKneeBaseRotation;
    private Quaternion leftFootBaseRotation;
    private Quaternion leftFootGroundRotationOffset = Quaternion.identity;
    private Vector3 leftFootSoleDownLocal = Vector3.down;
    private Quaternion rightHipBaseRotation;
    private Quaternion rightKneeBaseRotation;
    private Quaternion rightFootBaseRotation;
    private Quaternion rightFootGroundRotationOffset = Quaternion.identity;
    private Vector3 rightFootSoleDownLocal = Vector3.down;
    private Quaternion spineBaseRotation;
    private Quaternion transformBaseRotation = Quaternion.identity;
    private Quaternion movementRootBaseRotation = Quaternion.identity;

    private bool warnedMissingBones;
    private bool loggedResolvedBones;
    private bool basePoseInitialized;
    private Rigidbody movementRigidbody;
    private bool legSystemInitialized;
    private bool groundTargetsInitialized;
    private TitanLegGroundingState groundingState;
    private bool supportAnchorValid;
    private float groundingStableTimer;
    private float groundLossTimer;
    private bool landingCandidateValid;
    private TitanSupportFoot landingCandidateFoot;
    private Collider landingCandidateCollider;
    private TitanSupportFoot supportFoot;
    private Vector3 supportAnchorWorld;
    private bool remotePhysicsOverride;
    private bool stepActive;
    private TitanSupportFoot activeSwingFoot;
    private float touchdownRecoveryTimer;
    private Vector3 stepRootReferenceWorld;
    private Vector3 stepSupportPlantAnchor;
    private bool activeStepFootGroundedEventArmed;
    private Vector3 activeStepFootGroundedStartTarget;
    private float activeStepMaxFootLift;
    private float activeStepMaxGroundTargetMove;
    private bool pelvisAlignmentToTorsoRequested;
    private readonly Vector3[] stepRootCandidateBuffer = new Vector3[40];
    private TitanActiveStepSolveResult activeStepSolveThisTick;
    private TitanLegKinematicModel leftLegKinematicModel;
    private TitanLegKinematicModel rightLegKinematicModel;
    private int legKinematicModelVersion;
    private int lastLegRenderFrame = -1;
    private int legTicksThisRenderedFrame;
    private FootGroundContact leftGroundContact;
    private FootGroundContact rightGroundContact;
    private TitanPlantSurfaceState leftPlantSurface;
    private TitanPlantSurfaceState rightPlantSurface;
    private TitanValidGroundedPose lastValidGroundedPose;
    private int legPipelineTickCountThisFixedFrame;
    private int leftSolveCountThisFixedFrame;
    private int rightSolveCountThisFixedFrame;
    private int rootGeometrySolveCountThisFixedFrame;
    private int exhaustiveRootFallbackCountThisFixedFrame;
    private int fullPreviewLegSolveCountThisFixedFrame;
    private int ikSeedAttemptCountThisFixedFrame;
    private int trackingSeedAttemptCountThisFixedFrame;
    private int recoverySeedAttemptCountThisFixedFrame;
    private int canonicalSeedAttemptCountThisFixedFrame;
    private int ikIterationCountThisFixedFrame;
    private int trackingIterationCountThisFixedFrame;
    private int recoveryIterationCountThisFixedFrame;
    private int boneTransformWriteCountThisFixedFrame;
    private int previewTransformWriteCountThisFixedFrame;
    private int geometryBinarySearchIterationCountThisFixedFrame;
    private int activeStepConstrainedSupportPreviewCountThisFixedFrame;
    private int doubleSupportConstrainedTargetCacheHitCount;
    private int doubleSupportConstrainedTargetCacheMissCount;
    private int emergencyGroundPoseRestoreCount;
    private int groundedRootWriteCountWhileAirborne;
    private double lastLegPipelineFixedTime = double.NegativeInfinity;
    private float nextSolveWarningTime;

    public float LeftActualFootTargetError;
    public float RightActualFootTargetError;
    public float LeftMinimumSignedSoleGap;
    public float RightMinimumSignedSoleGap;
    public float LeftMaximumColliderPenetration;
    public float RightMaximumColliderPenetration;
    public bool LeftPlantedInvariantValid;
    public bool RightPlantedInvariantValid;
    public bool LeftRootPreviewReached;
    public bool RightRootPreviewReached;
    public bool LeftActualTransformReached;
    public bool RightActualTransformReached;
    public int PlantedInvariantFailureCount;
    public float LeftMathFkTransformMismatch;
    public float RightMathFkTransformMismatch;

    private struct TitanActiveStepSolveResult
    {
        public bool Valid;
        public Vector3 AcceptedGroundTarget;
        public Vector3 CurrentSwingTarget;
        public Vector3 RequiredRoot;
        public Vector3 PreferredRoot;
        public Vector3 FinalRootThisTick;
        public bool HorizontalTargetChanged;
        public bool TargetWasWorkspaceClamped;
        public bool UsedConstraintFallback;
    }

    private struct TitanPlantSurfaceState
    {
        public bool Valid;
        public Collider GroundCollider;
        public Vector3 GroundPointWorld;
        public Vector3 GroundPointColliderLocal;
        public Vector3 ContactNormalWorld;
    }

    private struct TitanPlantedFootValidation
    {
        public bool Valid;
        public float TargetError;
        public float MinimumSignedSoleGap;
        public float MaximumColliderPenetration;
        public bool RootPreviewReached;
        public bool ActualTransformReached;
    }

    private struct TitanValidGroundedPose
    {
        public bool Valid;
        public Vector3 RootPosition;
        public Quaternion RootRotation;
        public TitanLegIkAngles LeftAngles;
        public TitanLegIkAngles RightAngles;
        public Vector3 LeftPlantAnchor;
        public Vector3 RightPlantAnchor;
    }

    private struct TitanSwingTargetUpdate
    {
        public bool Changed;
        public Vector3 PreviousTarget;
        public Vector3 RequestedTarget;
        public Vector3 AcceptedTarget;
        public Vector3 RequestedWorldDelta;
        public bool WorkspaceClamped;
    }

    private readonly struct TitanStrictSwingTargetResult
    {
        public TitanStrictSwingTargetResult(Vector3 target, bool clamped)
        {
            Target = target;
            Clamped = clamped;
        }

        public Vector3 Target { get; }
        public bool Clamped { get; }
    }

    [ContextMenu("TitanRigRuntime/Bake Bone References")]
    private void BakeBoneReferencesContextMenu()
    {
        BakeBoneReferences();
    }

    public bool BakeBoneReferences()
    {
        ResolveAndCacheIfNeeded(forceCache: true);

        // Reset warnings so play mode can log a single clear message if still missing.
        warnedMissingBones = false;
        loggedResolvedBones = false;

        return HasAnyDrivenBone();
    }

    public Transform MovementRoot => mechaRoot != null ? mechaRoot : transform;
    public Transform LeftShoulder => leftShoulder;
    public Transform LeftElbow => leftElbow;
    public Transform RightShoulder => rightShoulder;
    public Transform RightElbow => rightElbow;
    public Transform LeftHip => leftHip;
    public Transform LeftKnee => leftKnee;
    public Transform LeftFoot => leftFoot;
    public Transform RightHip => rightHip;
    public Transform RightKnee => rightKnee;
    public Transform RightFoot => rightFoot;
    public Transform Drill => drill;
    public Transform Claw => claw;
    public event System.Action<bool> FootGrounded;
    public Rigidbody MovementRigidbody
    {
        get
        {
            EnsureMovementRigidbodyCached();
            return movementRigidbody;
        }
    }
    public Transform Spine => spine;
    public float WaistYaw => waistYaw;
    public TitanSupportFoot SupportFoot => supportFoot;
    public TitanLegGroundingState GroundingState => groundingState;
    public bool SupportAnchorValid => supportAnchorValid;
    public bool GroundTargetsInitialized => groundTargetsInitialized;
    public Vector3 SupportAnchorWorld => supportAnchorWorld;
    public TitanLegControlState LeftLegState => leftLeg;
    public TitanLegControlState RightLegState => rightLeg;
    public TitanLegSolverSettings LeftLegSolverSettings => leftLegSolverSettings;
    public TitanLegSolverSettings RightLegSolverSettings => rightLegSolverSettings;
    public int LegPipelineTickCountThisFixedFrame => legPipelineTickCountThisFixedFrame;
    public int LegTicksThisRenderedFrame => legTicksThisRenderedFrame;
    public int LeftSolveCountThisFixedFrame => leftSolveCountThisFixedFrame;
    public int RightSolveCountThisFixedFrame => rightSolveCountThisFixedFrame;
    public int RootGeometrySolveCountThisFixedFrame => rootGeometrySolveCountThisFixedFrame;
    public int ExhaustiveRootFallbackCountThisFixedFrame => exhaustiveRootFallbackCountThisFixedFrame;
    public int FullPreviewLegSolveCountThisFixedFrame => fullPreviewLegSolveCountThisFixedFrame;
    public int IkSeedAttemptCountThisFixedFrame => ikSeedAttemptCountThisFixedFrame;
    public int TrackingSeedAttemptCountThisFixedFrame => trackingSeedAttemptCountThisFixedFrame;
    public int RecoverySeedAttemptCountThisFixedFrame => recoverySeedAttemptCountThisFixedFrame;
    public int CanonicalSeedAttemptCountThisFixedFrame => canonicalSeedAttemptCountThisFixedFrame;
    public int IkIterationCountThisFixedFrame => ikIterationCountThisFixedFrame;
    public int TrackingIterationCountThisFixedFrame => trackingIterationCountThisFixedFrame;
    public int RecoveryIterationCountThisFixedFrame => recoveryIterationCountThisFixedFrame;
    public int BoneTransformWriteCountThisFixedFrame => boneTransformWriteCountThisFixedFrame;
    public int PreviewTransformWriteCountThisFixedFrame => previewTransformWriteCountThisFixedFrame;
    public int GeometryBinarySearchIterationCountThisFixedFrame => geometryBinarySearchIterationCountThisFixedFrame;
    public int ActiveStepConstrainedSupportPreviewCountThisFixedFrame => activeStepConstrainedSupportPreviewCountThisFixedFrame;
    public int DoubleSupportConstrainedTargetCacheHitCount => doubleSupportConstrainedTargetCacheHitCount;
    public int DoubleSupportConstrainedTargetCacheMissCount => doubleSupportConstrainedTargetCacheMissCount;
    public int EmergencyGroundPoseRestoreCount => emergencyGroundPoseRestoreCount;
    public int GroundedRootWriteCountWhileAirborne => groundedRootWriteCountWhileAirborne;
    public FootAttachmentController LeftFootAttachment => leftFootAttachment;
    public FootAttachmentController RightFootAttachment => rightFootAttachment;
    public bool IsDoubleSupport => leftLeg.IsPlanted && rightLeg.IsPlanted;
    public bool IsStepActive => stepActive;
    public Vector2 ConsumedMouseDeltaThisTick { get; private set; }
    public Vector3 RequestedFootWorldDeltaThisTick { get; private set; }
    public Vector3 AcceptedFootWorldDeltaThisTick { get; private set; }
    public float FootInputAcceptanceRatioThisTick { get; private set; }
    public bool FootTargetWorkspaceClampedThisTick { get; private set; }
    public Vector3 RequiredRootCorrectionThisTick { get; private set; }
    public float FinalSwingTargetErrorThisTick { get; private set; }
    public bool RootFallbackUsedThisTick { get; private set; }

    public void SetPlantSurfaceForTests(bool left, Collider collider, Vector3 point, Vector3 normal)
    {
        SetPlantSurface(left, new FootGroundContact(collider, point, normal, 0f));
    }

    public bool TryGetFootGroundContactRobustForTests(bool left, out FootGroundContact contact)
    {
        return TryGetFootGroundContactRobust(left, out contact);
    }

    public Vector3 GetFootProbeWorldPosition(bool left)
    {
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        Transform foot = left ? leftFoot : rightFoot;
        Transform probe = attachment != null ? attachment.BottomProbe : foot;
        return probe != null ? probe.position : Vector3.zero;
    }

    public float GetFootSoleAngleFromGround(bool left)
    {
        Transform foot = left ? leftFoot : rightFoot;
        if (foot == null)
        {
            return 180f;
        }

        return Vector3.Angle(GetSoleDownWorld(left, foot.rotation), -TitanGroundFrame.Up);
    }

    public void Init()
    {
        ResolveAndCacheIfNeeded(forceCache: true);
        EnsureMovementRigidbodyCached();
        ConfigureMovementRigidbodyForTitanControl();
    }

    public void Clear()
    {
        warnedMissingBones = false;
        loggedResolvedBones = false;
        basePoseInitialized = false;
        movementRigidbody = null;
        remotePhysicsOverride = false;
        leftFootAttachment = null;
        rightFootAttachment = null;
        legSystemInitialized = false;
        groundTargetsInitialized = false;
        leftLegKinematicModel = default;
        rightLegKinematicModel = default;
        legKinematicModelVersion++;
        groundingState = TitanLegGroundingState.Airborne;
        supportAnchorValid = false;
        leftPlantSurface = default;
        rightPlantSurface = default;
        lastValidGroundedPose = default;
        groundingStableTimer = 0f;
        groundLossTimer = 0f;
        landingCandidateValid = false;
        landingCandidateFoot = TitanSupportFoot.Left;
        landingCandidateCollider = null;
        supportFoot = TitanSupportFoot.Left;
        supportAnchorWorld = default;
        pelvisAlignmentToTorsoRequested = false;
        ClearStepState();
        leftLeg.Initialized = false;
        rightLeg.Initialized = false;
        legPipelineTickCountThisFixedFrame = 0;
        legTicksThisRenderedFrame = 0;
        lastLegRenderFrame = -1;
        leftSolveCountThisFixedFrame = 0;
        rightSolveCountThisFixedFrame = 0;
        emergencyGroundPoseRestoreCount = 0;
        groundedRootWriteCountWhileAirborne = 0;
        lastLegPipelineFixedTime = double.NegativeInfinity;
    }

    private void OnValidate()
    {
        EnsureSolverSettingsInitialized();
    }

    [ContextMenu("TitanRigRuntime/Validate Leg IK Configuration")]
    private void ValidateLegIkConfigurationContextMenu()
    {
        if (!EnsureReady())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[TitanRigRuntime] Cannot validate leg IK configuration because driven bones are missing.", this);
#endif
            return;
        }

        TitanLegControlState leftStateBefore = leftLeg;
        TitanLegControlState rightStateBefore = rightLeg;
        TitanSupportFoot supportBefore = supportFoot;
        Vector3 supportAnchorBefore = supportAnchorWorld;
        bool initializedBefore = legSystemInitialized;
        bool anchorValidBefore = supportAnchorValid;
        TitanLegGroundingState groundingStateBefore = groundingState;
        Quaternion leftHipRotation = leftHip != null ? leftHip.localRotation : Quaternion.identity;
        Quaternion leftKneeRotation = leftKnee != null ? leftKnee.localRotation : Quaternion.identity;
        Quaternion leftFootRotation = leftFoot != null ? leftFoot.rotation : Quaternion.identity;
        Quaternion rightHipRotation = rightHip != null ? rightHip.localRotation : Quaternion.identity;
        Quaternion rightKneeRotation = rightKnee != null ? rightKnee.localRotation : Quaternion.identity;
        Quaternion rightFootRotation = rightFoot != null ? rightFoot.rotation : Quaternion.identity;

        EnsureLegSystemInitialized();
        RunValidationLift(TitanSupportFoot.Left, 0.35f);
        RunValidationLift(TitanSupportFoot.Right, 0.35f);

        leftLeg = leftStateBefore;
        rightLeg = rightStateBefore;
        supportFoot = supportBefore;
        supportAnchorWorld = supportAnchorBefore;
        supportAnchorValid = anchorValidBefore;
        groundingState = groundingStateBefore;
        legSystemInitialized = initializedBefore;
        if (leftHip != null) leftHip.localRotation = leftHipRotation;
        if (leftKnee != null) leftKnee.localRotation = leftKneeRotation;
        if (leftFoot != null) leftFoot.rotation = leftFootRotation;
        if (rightHip != null) rightHip.localRotation = rightHipRotation;
        if (rightKnee != null) rightKnee.localRotation = rightKneeRotation;
        if (rightFoot != null) rightFoot.rotation = rightFootRotation;
    }

    private void RunValidationLift(TitanSupportFoot validationSupport, float lift)
    {
        FootGroundContact contact = CreateValidationContact(validationSupport == TitanSupportFoot.Left);
        SetSupportFoot(validationSupport, contact);
        if (validationSupport == TitanSupportFoot.Left)
        {
            rightLeg.FootLiftTarget = lift;
            rightLeg.FootLift = lift;
            Vector3 target = rightLeg.DesiredGroundTarget + TitanGroundFrame.Up * lift;
            SolveLegOnce(false, target);
            ApplyFootTargetRotation(false);
            LogValidationResult(false, rightLeg);
            return;
        }

        leftLeg.FootLiftTarget = lift;
        leftLeg.FootLift = lift;
        Vector3 leftTarget = leftLeg.DesiredGroundTarget + TitanGroundFrame.Up * lift;
        SolveLegOnce(true, leftTarget);
        ApplyFootTargetRotation(true);
        LogValidationResult(true, leftLeg);
    }

    private FootGroundContact CreateValidationContact(bool left)
    {
        Transform foot = left ? leftFoot : rightFoot;
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        Transform probe = attachment != null ? attachment.BottomProbe : foot;
        Vector3 point = probe != null ? probe.position : (foot != null ? foot.position : transform.position);
        return new FootGroundContact(null, point, TitanGroundFrame.Up, 0f);
    }

    private void LogValidationResult(bool left, in TitanLegControlState state)
    {
        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        float physicalHipRoll = settings.HipRoll.ToPhysicalAngle(state.SolvedAngles.HipRoll);
        float physicalKneeRoll = settings.KneeRoll.ToPhysicalAngle(state.SolvedAngles.KneeRoll);
        Transform foot = left ? leftFoot : rightFoot;
        float soleAngle = foot != null
            ? Vector3.Angle(GetSoleDownWorld(left, foot.rotation), -TitanGroundFrame.Up)
            : 180f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[TitanRigRuntime] {(left ? "Left" : "Right")} leg IK validation: " +
            $"logical(HipYaw={state.SolvedAngles.HipYaw:0.00}, HipRoll={state.SolvedAngles.HipRoll:0.00}, KneeRoll={state.SolvedAngles.KneeRoll:0.00}), " +
            $"physical(HipRoll={physicalHipRoll:0.00}, KneeRoll={physicalKneeRoll:0.00}), " +
            $"solveError={state.SolveError:0.0000}, soleAngle={soleAngle:0.00}",
            this);
#endif
    }

    private void EnsureSolverSettingsInitialized()
    {
        if (leftLegSolverSettings.Iterations <= 0 || leftLegSolverSettings.HipYaw.LocalAxis.sqrMagnitude < 0.0001f)
        {
            leftLegSolverSettings = TitanLegSolverSettings.CreateDefault();
        }

        if (rightLegSolverSettings.Iterations <= 0 || rightLegSolverSettings.HipYaw.LocalAxis.sqrMagnitude < 0.0001f)
        {
            rightLegSolverSettings = TitanLegSolverSettings.CreateDefault();
        }
    }

    public void ApplyMovementRootPose(Vector3 worldPosition, Quaternion worldRotation, bool zeroVelocities)
    {
        EnsureMovementRigidbodyCached();

        if (movementRigidbody != null)
        {
            movementRigidbody.position = worldPosition;
            movementRigidbody.rotation = worldRotation;
            movementRigidbody.transform.SetPositionAndRotation(worldPosition, worldRotation);

            if (zeroVelocities && !movementRigidbody.isKinematic)
            {
                movementRigidbody.linearVelocity = Vector3.zero;
                movementRigidbody.angularVelocity = Vector3.zero;
            }

            return;
        }

        MovementRoot.SetPositionAndRotation(worldPosition, worldRotation);
    }

    public void SetRemotePhysicsOverride(bool enabled)
    {
        remotePhysicsOverride = enabled;
        ApplyCurrentPhysicsMode();
    }

    public void ApplyMovementRootBaseRotation()
    {
        EnsureMovementRigidbodyCached();

        Transform movementRoot = MovementRoot;
        ConsumeTorsoAlignedPelvisRotation();
        Quaternion ownerBaseRotation = TitanGroundFrame.GroundRotation(transformBaseRotation * Vector3.forward);
        Quaternion baseRotation = TitanGroundFrame.GroundRotation(movementRootBaseRotation * Vector3.forward);
        if (movementRigidbody != null)
        {
            Vector3 position = movementRigidbody.position;
            movementRigidbody.position = position;
            movementRigidbody.rotation = movementRigidbody.transform == transform ? ownerBaseRotation : baseRotation;
            movementRigidbody.transform.SetPositionAndRotation(position, movementRigidbody.rotation);

            if (!movementRigidbody.isKinematic)
            {
                movementRigidbody.angularVelocity = Vector3.zero;
            }
        }

        transform.SetPositionAndRotation(transform.position, ownerBaseRotation);

        if (movementRoot != transform)
        {
            movementRoot.SetPositionAndRotation(movementRoot.position, baseRotation);
        }
    }

    private void ConsumeTorsoAlignedPelvisRotation()
    {
        if (!pelvisAlignmentToTorsoRequested)
        {
            return;
        }

        pelvisAlignmentToTorsoRequested = false;

        Quaternion baseRootRotation = TitanGroundFrame.GroundRotation(movementRootBaseRotation * Vector3.forward);
        Quaternion torsoRotation;
        if (spine != null)
        {
            Quaternion parentRotation = spine.parent != null ? spine.parent.rotation : Quaternion.identity;
            Quaternion baseSpineWorldRotation = parentRotation * spineBaseRotation;
            Quaternion torsoDelta = spine.rotation * Quaternion.Inverse(baseSpineWorldRotation);
            torsoRotation = torsoDelta * baseRootRotation;
        }
        else
        {
            torsoRotation = Quaternion.AngleAxis(waistYaw, TitanGroundFrame.Up) * baseRootRotation;
        }

        movementRootBaseRotation = TitanGroundFrame.GroundRotation(torsoRotation * Vector3.forward);
        if (MovementRoot == transform)
        {
            transformBaseRotation = movementRootBaseRotation;
        }

        waistYaw = 0f;
        ApplySpine(0f, 0f, 0f);
    }

    public bool EnsureReady()
    {
        if (!basePoseInitialized || !HasAnyDrivenBone())
        {
            ResolveAndCacheIfNeeded(forceCache: false);
        }

        bool hasAnyDrivenBone = HasAnyDrivenBone();

        if (!hasAnyDrivenBone && !warnedMissingBones)
        {
            warnedMissingBones = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string animatorName = animator != null ? animator.name : "<none>";
            bool isHuman = animator != null && animator.isHuman;
            Debug.LogWarning($"[TitanRigManager] Could not resolve any controllable bones. " +
                             $"Animator={animatorName}, isHuman={isHuman}. " +
                             $"Assign bone overrides on TitanRigRuntime (prefab) or fix the model rig.", this);
#endif
        }

        if (hasAnyDrivenBone && !loggedResolvedBones)
        {
            loggedResolvedBones = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TitanRigManager] Resolved bones - LS:{NameOrNone(leftShoulder)} LE:{NameOrNone(leftElbow)} RS:{NameOrNone(rightShoulder)} RE:{NameOrNone(rightElbow)} LH:{NameOrNone(leftHip)} LK:{NameOrNone(leftKnee)} LF:{NameOrNone(leftFoot)} RH:{NameOrNone(rightHip)} RK:{NameOrNone(rightKnee)} RF:{NameOrNone(rightFoot)} SP:{NameOrNone(spine)} DR:{NameOrNone(drill)} CL:{NameOrNone(claw)}", this);
#endif
        }

        return hasAnyDrivenBone;
    }

    public void SetWaistYaw(float value)
    {
        waistYaw = value;
    }

    public TitanArmControlState GetArmState(bool left)
    {
        return left ? leftArm : rightArm;
    }

    public void SetArmState(bool left, TitanArmControlState state)
    {
        if (left)
        {
            leftArm = state;
            return;
        }

        rightArm = state;
    }

    public TitanLegControlState GetLegState(bool left)
    {
        EnsureLegStateInitialized(left);
        return left ? leftLeg : rightLeg;
    }

    public float GetFootGroundHeight(bool left)
    {
        EnsureLegStateInitialized(left);
        Vector3 up = TitanGroundFrame.Up;
        Vector3 target = left ? leftLeg.DesiredGroundTarget : rightLeg.DesiredGroundTarget;
        return Vector3.Dot(target, up);
    }

    public void ApplyTorsoPose()
    {
        if (!EnsureReady())
        {
            return;
        }

        ApplySpine(0f, waistYaw, 0f);
    }

    public void ApplyArmPose(bool left)
    {
        if (!EnsureReady())
        {
            return;
        }

        TitanArmControlState state = left ? leftArm : rightArm;
        if (left)
        {
            ApplyLeftArm(state.ShoulderPitch, state.ShoulderRoll, state.ElbowPitch);
            return;
        }

        ApplyRightArm(state.ShoulderPitch, state.ShoulderRoll, state.ElbowPitch);
    }

    public void ApplyLeftArm(float shoulderPitch, float shoulderRoll, float elbowPitch)
    {
        if (leftShoulder != null)
        {
            leftShoulder.localRotation = ComposeShoulderRotation(leftShoulderBaseRotation, shoulderPitch, shoulderRoll);
        }

        if (leftElbow != null)
        {
            leftElbow.localRotation = leftElbowBaseRotation * Quaternion.Euler(0f, elbowPitch, 0f);
        }
    }

    public void ApplyRightArm(float shoulderPitch, float shoulderRoll, float elbowPitch)
    {
        if (rightShoulder != null)
        {
            rightShoulder.localRotation = ComposeShoulderRotation(rightShoulderBaseRotation, shoulderPitch, shoulderRoll);
        }

        if (rightElbow != null)
        {
            rightElbow.localRotation = rightElbowBaseRotation * Quaternion.Euler(0f, elbowPitch, 0f);
        }
    }

    public void ApplySpine(float yaw, float pitch = 0f, float roll = 0f)
    {
        if (spine != null)
        {
            spine.localRotation = spineBaseRotation * Quaternion.Euler(pitch, yaw, roll);
        }
    }

    private static Quaternion ComposeShoulderRotation(Quaternion baseRotation, float shoulderPitch, float shoulderRoll)
    {
        Quaternion rollRotation = Quaternion.AngleAxis(shoulderRoll, Vector3.forward);
        Quaternion pitchRotation = Quaternion.AngleAxis(shoulderPitch, Vector3.right);
        return baseRotation * rollRotation * pitchRotation;
    }

    private void EnsureLegSystemInitialized()
    {
        EnsureReady();
        EnsureSolverSettingsInitialized();
        EnsureLegStateInitialized(true);
        EnsureLegStateInitialized(false);
        EnsureLegKinematicModels();

        if (legSystemInitialized)
        {
            return;
        }

        leftLeg.SolvedAngles = CreateInitialAngles(leftLegSolverSettings);
        rightLeg.SolvedAngles = CreateInitialAngles(rightLegSolverSettings);
        leftLeg.ForceRecovery = true;
        rightLeg.ForceRecovery = true;
        leftLeg.PostureCanonicalizationPending = true;
        rightLeg.PostureCanonicalizationPending = true;

        float leftHeight = Vector3.Dot(leftLeg.DesiredGroundTarget, TitanGroundFrame.Up);
        float rightHeight = Vector3.Dot(rightLeg.DesiredGroundTarget, TitanGroundFrame.Up);
        supportFoot = rightHeight < leftHeight ? TitanSupportFoot.Right : TitanSupportFoot.Left;
        supportAnchorValid = false;
        groundTargetsInitialized = false;
        leftPlantSurface = default;
        rightPlantSurface = default;
        lastValidGroundedPose = default;
        groundingState = TitanLegGroundingState.Airborne;
        ApplyCurrentPhysicsMode();
        legSystemInitialized = true;
    }

    private void EnsureLegKinematicModels()
    {
        Transform movementRoot = MovementRoot;
        if (!leftLegKinematicModel.Valid && leftHip != null && leftKnee != null && leftFoot != null)
        {
            leftLegKinematicModel = TitanConstrainedLegIkSolver.BuildKinematicModel(
                movementRoot,
                leftHip,
                leftKnee,
                leftFoot,
                leftHipBaseRotation,
                leftKneeBaseRotation,
                ++legKinematicModelVersion);
            leftLeg.ForceRecovery = true;
        }

        if (!rightLegKinematicModel.Valid && rightHip != null && rightKnee != null && rightFoot != null)
        {
            rightLegKinematicModel = TitanConstrainedLegIkSolver.BuildKinematicModel(
                movementRoot,
                rightHip,
                rightKnee,
                rightFoot,
                rightHipBaseRotation,
                rightKneeBaseRotation,
                ++legKinematicModelVersion);
            rightLeg.ForceRecovery = true;
        }
    }

    private void SyncUngroundedLegStateToCurrentPose(bool left)
    {
        ref TitanLegControlState state = ref GetMutableLegState(left);
        Transform foot = left ? leftFoot : rightFoot;
        if (foot == null)
        {
            return;
        }

        state.DesiredGroundTarget = foot.position;
        state.ReachableFootTarget = foot.position;
        state.PredictedFootPosition = foot.position;
        state.ActualFootPosition = foot.position;
        state.SolveError = 0f;
        state.TargetWasClamped = false;
        state.DesiredPositionError = 0f;
        state.ForceRecovery = true;
        state.PostureCanonicalizationPending = true;
        state.LastSolveReached = false;
        state.LastSolveTarget = foot.position;
        state.LastSolveRootPosition = MovementRoot.position;
        state.LastSolveRootRotation = MovementRoot.rotation;
        state.SolveCache = default;
        state.FootLift = 0f;
        state.FootLiftTarget = 0f;
        state.FootLiftSmoothVelocity = 0f;
        state.FootLiftFallVelocity = 0f;
        state.IsPlanted = false;
        state.PlantAnchorWorld = foot.position;
    }

    private Vector3 ComputeFootPivotOnGroundPlane(bool left, Vector3 groundPlanePoint)
    {
        Transform foot = left ? leftFoot : rightFoot;
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        if (foot == null)
        {
            return groundPlanePoint;
        }

        Vector3 up = TitanGroundFrame.Up;
        if (attachment == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[TitanRigRuntime] Missing {(left ? "left" : "right")} foot attachment; falling back to foot pivot as sole authority.", this);
#endif
            return Vector3.ProjectOnPlane(foot.position, up) + up * (Vector3.Dot(groundPlanePoint, up) + plantedSoleClearance);
        }

        return attachment.ComputePivotTargetForGroundPlane(foot.position, groundPlanePoint, up, plantedSoleClearance);
    }

    private void ResetLegToGroundBaseline(bool left, Vector3 groundTarget)
    {
        ref TitanLegControlState state = ref GetMutableLegState(left);
        Transform foot = left ? leftFoot : rightFoot;
        state.DesiredGroundTarget = groundTarget;
        state.FootLift = 0f;
        state.FootLiftTarget = 0f;
        state.FootLiftSmoothVelocity = 0f;
        state.FootLiftFallVelocity = 0f;
        state.ReachableFootTarget = groundTarget;
        state.PredictedFootPosition = groundTarget;
        state.IsPlanted = true;
        state.PlantAnchorWorld = groundTarget;
        state.ForceRecovery = true;
        state.PostureCanonicalizationPending = true;
        state.SolveCache = default;
        if (foot != null)
        {
            state.ActualFootPosition = foot.position;
            state.PredictedFootPosition = foot.position;
            state.SolveError = Vector3.Distance(foot.position, groundTarget);
            return;
        }

        state.ActualFootPosition = groundTarget;
        state.SolveError = 0f;
    }

    private TitanPlantSurfaceState CreatePlantSurfaceState(in FootGroundContact contact)
    {
        TitanPlantSurfaceState surface = new TitanPlantSurfaceState
        {
            Valid = contact.Collider != null,
            GroundCollider = contact.Collider,
            GroundPointWorld = contact.Point,
            ContactNormalWorld = contact.Normal,
        };
        if (contact.Collider != null)
        {
            surface.GroundPointColliderLocal = contact.Collider.transform.InverseTransformPoint(contact.Point);
        }

        return surface;
    }

    private void SetPlantSurface(bool left, in FootGroundContact contact)
    {
        if (left)
        {
            leftPlantSurface = CreatePlantSurfaceState(contact);
            return;
        }

        rightPlantSurface = CreatePlantSurfaceState(contact);
    }

    private void InvalidatePlantSurface(bool left)
    {
        if (left)
        {
            leftPlantSurface = default;
            return;
        }

        rightPlantSurface = default;
    }

    private Vector3 ResolvePlantSurfaceWorldPoint(in TitanPlantSurfaceState surface)
    {
        if (!surface.Valid)
        {
            return surface.GroundPointWorld;
        }

        return surface.GroundCollider != null
            ? surface.GroundCollider.transform.TransformPoint(surface.GroundPointColliderLocal)
            : surface.GroundPointWorld;
    }

    private void InitializeGroundedStance(
        bool leftHasContact,
        in FootGroundContact leftContact,
        bool rightHasContact,
        in FootGroundContact rightContact)
    {
        ApplyLandingFootRotations();
        TitanSupportFoot initialSupport = ChooseSupportFromContacts(leftHasContact, leftContact, rightHasContact, rightContact);
        bool supportIsLeft = initialSupport == TitanSupportFoot.Left;
        FootGroundContact supportContact = supportIsLeft ? leftContact : rightContact;

        supportFoot = initialSupport;
        supportAnchorWorld = ComputeFootPivotForGroundContact(supportIsLeft, supportContact);

        Vector3 leftGroundTarget = leftHasContact
            ? ComputeFootPivotForGroundContact(true, leftContact)
            : ComputeFootPivotOnGroundPlane(true, supportContact.Point);
        Vector3 rightGroundTarget = rightHasContact
            ? ComputeFootPivotForGroundContact(false, rightContact)
            : ComputeFootPivotOnGroundPlane(false, supportContact.Point);
        SanitizeInitialDoubleSupportTargets(supportIsLeft, ref leftGroundTarget, ref rightGroundTarget);

        ResetLegToGroundBaseline(true, leftGroundTarget);
        ResetLegToGroundBaseline(false, rightGroundTarget);
        SetPlantSurface(true, leftHasContact ? leftContact : supportContact);
        SetPlantSurface(false, rightHasContact ? rightContact : supportContact);

        if (supportIsLeft)
        {
            leftLeg.DesiredGroundTarget = supportAnchorWorld;
        }
        else
        {
            rightLeg.DesiredGroundTarget = supportAnchorWorld;
        }

        supportAnchorValid = true;
        groundTargetsInitialized = true;
        leftLeg.PostureCanonicalizationPending = true;
        rightLeg.PostureCanonicalizationPending = true;
        leftLeg.ForceRecovery = true;
        rightLeg.ForceRecovery = true;
        groundingState = TitanLegGroundingState.Grounded;
        groundingStableTimer = groundingStableTime;
        groundLossTimer = 0f;
        ClearStepState();
        ApplyCurrentPhysicsMode();
    }

    private void InvalidateGroundedStance()
    {
        supportAnchorValid = false;
        groundTargetsInitialized = false;
        groundingState = TitanLegGroundingState.Airborne;
        groundingStableTimer = 0f;
        landingCandidateValid = false;
        landingCandidateFoot = TitanSupportFoot.Left;
        landingCandidateCollider = null;
        leftLeg.IsPlanted = false;
        rightLeg.IsPlanted = false;
        activeStepFootGroundedEventArmed = false;
        leftPlantSurface = default;
        rightPlantSurface = default;
        lastValidGroundedPose = default;
        ClearStepState();
        ApplyCurrentPhysicsMode();
    }

    private bool IsSameLandingCandidate(TitanSupportFoot candidateFoot, Collider candidateCollider)
    {
        return landingCandidateValid
            && landingCandidateFoot == candidateFoot
            && landingCandidateCollider == candidateCollider;
    }

    private void EnsureLegStateInitialized(bool left)
    {
        TitanLegControlState state = left ? leftLeg : rightLeg;
        if (state.Initialized)
        {
            return;
        }

        Transform foot = left ? leftFoot : rightFoot;
        if (foot == null)
        {
            return;
        }

        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        state.DesiredGroundTarget = foot.position;
        state.FootLift = 0f;
        state.FootLiftTarget = 0f;
        state.FootLiftSmoothVelocity = 0f;
        state.FootLiftFallVelocity = 0f;
        state.SolvedAngles = CreateInitialAngles(settings);
        state.ReachableFootTarget = foot.position;
        state.PredictedFootPosition = foot.position;
        state.ActualFootPosition = foot.position;
        state.SolveError = 0f;
        state.TargetWasClamped = false;
        state.DesiredPositionError = 0f;
        state.ForceRecovery = true;
        state.PostureCanonicalizationPending = true;
        state.LastSolveReached = false;
        state.LastSolveTarget = foot.position;
        state.LastSolveRootPosition = MovementRoot.position;
        state.LastSolveRootRotation = MovementRoot.rotation;
        state.SolveCache = default;
        state.Initialized = true;
        if (left)
        {
            leftLeg = state;
            return;
        }

        rightLeg = state;
    }

    private static TitanLegIkAngles CreateInitialAngles(in TitanLegSolverSettings settings)
    {
        return new TitanLegIkAngles
        {
            HipYaw = settings.HipYaw.NeutralAngle,
            HipRoll = settings.HipRoll.MinAngle,
            KneeRoll = settings.KneeRoll.MinAngle,
        };
    }

    private void SetSupportFoot(TitanSupportFoot newSupport, in FootGroundContact contact)
    {
        supportFoot = newSupport;
        bool left = newSupport == TitanSupportFoot.Left;
        ref TitanLegControlState state = ref GetMutableLegState(left);
        supportAnchorWorld = state.IsPlanted ? state.PlantAnchorWorld : ComputeFootPivotForGroundContact(left, contact);
        if (!state.IsPlanted)
        {
            SetPlantSurface(left, contact);
        }
        supportAnchorValid = true;
        state.DesiredGroundTarget = supportAnchorWorld;
        state.FootLift = 0f;
        state.FootLiftTarget = 0f;
        state.FootLiftSmoothVelocity = 0f;
        state.FootLiftFallVelocity = 0f;
        state.IsPlanted = true;
        state.PlantAnchorWorld = supportAnchorWorld;
        state.ForceRecovery = true;
        state.PostureCanonicalizationPending = true;
        state.SolveCache = default;
    }

    private ref TitanLegControlState GetMutableLegState(bool left)
    {
        if (left)
        {
            return ref leftLeg;
        }

        return ref rightLeg;
    }

    private Vector3 ComputeFootPivotForGroundContact(bool left, in FootGroundContact contact)
    {
        Transform foot = left ? leftFoot : rightFoot;
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        if (foot == null)
        {
            return contact.Point;
        }

        if (attachment == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[TitanRigRuntime] Missing {(left ? "left" : "right")} foot attachment; falling back to foot pivot as sole authority.", this);
#endif
            Vector3 up = TitanGroundFrame.Up;
            return Vector3.ProjectOnPlane(foot.position, up) + up * (Vector3.Dot(contact.Point, up) + plantedSoleClearance);
        }

        return attachment.ComputePivotTargetForGroundPlane(foot.position, contact.Point, TitanGroundFrame.Up, plantedSoleClearance);
    }

    private void SanitizeInitialDoubleSupportTargets(bool supportIsLeft, ref Vector3 leftGroundTarget, ref Vector3 rightGroundTarget)
    {
        Vector3 supportTarget = supportIsLeft ? leftGroundTarget : rightGroundTarget;
        Vector3 oppositeTarget = supportIsLeft ? rightGroundTarget : leftGroundTarget;
        bool oppositeIsLeft = !supportIsLeft;
        if (!TryCreateWorkspace(supportIsLeft, supportTarget, out TitanLegRootWorkspace supportWorkspace)
            || !TryCreateWorkspace(oppositeIsLeft, oppositeTarget, out TitanLegRootWorkspace oppositeWorkspace))
        {
            return;
        }

        TitanStanceRootResult initial = TitanStanceRootSolver.SolveClosestSharedWorkspace(
            MovementRoot.position,
            TitanGroundFrame.Up,
            supportWorkspace,
            oppositeWorkspace);
        if (initial.Feasible)
        {
            return;
        }

        Vector3 up = TitanGroundFrame.Up;
        Vector3 supportCenter = supportWorkspace.Center;
        Vector3 oppositeCenter = oppositeWorkspace.Center;
        float combinedReach = Mathf.Max(0f, supportWorkspace.MaxReach + oppositeWorkspace.MaxReach - 0.001f);
        Vector3 centerDelta = oppositeCenter - supportCenter;
        float verticalDelta = Vector3.Dot(centerDelta, up);
        float planarLimitSquared = combinedReach * combinedReach - verticalDelta * verticalDelta;
        Vector3 adjustedCenter;
        if (planarLimitSquared <= 0f)
        {
            adjustedCenter = supportCenter + up * Mathf.Clamp(verticalDelta, -combinedReach, combinedReach);
        }
        else
        {
            Vector3 planarDelta = Vector3.ProjectOnPlane(centerDelta, up);
            float planarLimit = Mathf.Sqrt(planarLimitSquared);
            adjustedCenter = supportCenter
                + up * verticalDelta
                + Vector3.ClampMagnitude(planarDelta, planarLimit);
        }

        Vector3 adjustedTarget = adjustedCenter + oppositeWorkspace.HipOffsetFromRoot;
        float originalHeight = Vector3.Dot(oppositeTarget, up);
        adjustedTarget = Vector3.ProjectOnPlane(adjustedTarget, up) + up * originalHeight;

        if (supportIsLeft)
        {
            rightGroundTarget = adjustedTarget;
            return;
        }

        leftGroundTarget = adjustedTarget;
    }

    private Quaternion ComputeFootGroundFrame()
    {
        Vector3 up = TitanGroundFrame.Up;
        Vector3 forward = MovementRoot != null
            ? MovementRoot.forward
            : TitanGroundFrame.WorldForward;

        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = TitanGroundFrame.WorldForward;
        }

        return TitanGroundFrame.GroundRotation(forward.normalized);
    }

    private Quaternion ComputeFootTargetRotation(bool left)
    {
        Quaternion groundFrame = ComputeFootGroundFrame();
        Quaternion footOffset = left ? leftFootGroundRotationOffset : rightFootGroundRotationOffset;
        return AlignFootSoleToGround(left, groundFrame * footOffset);
    }

    private Quaternion AlignFootSoleToGround(bool left, Quaternion targetRotation)
    {
        Vector3 soleDownWorld = GetSoleDownWorld(left, targetRotation);
        if (soleDownWorld.sqrMagnitude < 0.0001f)
        {
            return targetRotation;
        }

        return Quaternion.FromToRotation(soleDownWorld.normalized, -TitanGroundFrame.Up) * targetRotation;
    }

    private Vector3 GetSoleDownWorld(bool left, Quaternion rotation)
    {
        Vector3 soleDownLocal = left ? leftFootSoleDownLocal : rightFootSoleDownLocal;
        return rotation * soleDownLocal;
    }

    public void TickLegSystem(
        in TitanLegInputCommand leftCommand,
        in TitanLegInputCommand rightCommand,
        float deltaTime)
    {
        using (TitanLegTickMarker.Auto())
        {
            if (!EnsureReady())
            {
                return;
            }

            EnsureLegSystemInitialized();
            RecordLegPipelineTick();
            leftSolveCountThisFixedFrame = 0;
            rightSolveCountThisFixedFrame = 0;
            rootGeometrySolveCountThisFixedFrame = 0;
            exhaustiveRootFallbackCountThisFixedFrame = 0;
            fullPreviewLegSolveCountThisFixedFrame = 0;
            ikSeedAttemptCountThisFixedFrame = 0;
            trackingSeedAttemptCountThisFixedFrame = 0;
            recoverySeedAttemptCountThisFixedFrame = 0;
            canonicalSeedAttemptCountThisFixedFrame = 0;
            ikIterationCountThisFixedFrame = 0;
            trackingIterationCountThisFixedFrame = 0;
            recoveryIterationCountThisFixedFrame = 0;
            boneTransformWriteCountThisFixedFrame = 0;
            previewTransformWriteCountThisFixedFrame = 0;
            geometryBinarySearchIterationCountThisFixedFrame = 0;
            activeStepConstrainedSupportPreviewCountThisFixedFrame = 0;
            activeStepSolveThisTick = default;
            ConsumedMouseDeltaThisTick = default;
            RequestedFootWorldDeltaThisTick = default;
            AcceptedFootWorldDeltaThisTick = default;
            FootInputAcceptanceRatioThisTick = 0f;
            FootTargetWorkspaceClampedThisTick = false;
            RequiredRootCorrectionThisTick = default;
            FinalSwingTargetErrorThisTick = 0f;
            RootFallbackUsedThisTick = false;

            bool leftHasContact = TryGetFootGroundContact(true, out leftGroundContact);
            bool rightHasContact = TryGetFootGroundContact(false, out rightGroundContact);
            UpdateLegContactState(true, leftHasContact, leftGroundContact);
            UpdateLegContactState(false, rightHasContact, rightGroundContact);

            if (!groundTargetsInitialized)
            {
                SyncUngroundedLegStateToCurrentPose(true);
                SyncUngroundedLegStateToCurrentPose(false);
            }

            if (!leftHasContact && !rightHasContact && !ShouldMaintainAnchoredStanceWithoutContact())
            {
                UpdateAirborneState(deltaTime);
                ApplyFootTargetRotation(true);
                ApplyFootTargetRotation(false);
                return;
            }

            if (!groundTargetsInitialized && !TryAdvanceLandingState(leftHasContact, leftGroundContact, rightHasContact, rightGroundContact, deltaTime))
            {
                ApplyFootTargetRotation(true);
                ApplyFootTargetRotation(false);
                return;
            }

            UpdateGroundedContactState(leftHasContact, rightHasContact, deltaTime);
            if (!supportAnchorValid || !groundTargetsInitialized)
            {
                ApplyFootTargetRotation(true);
                ApplyFootTargetRotation(false);
                return;
            }

            IntegrateLiftTarget(ref leftLeg, leftCommand.LiftInput, deltaTime);
            IntegrateLiftTarget(ref rightLeg, rightCommand.LiftInput, deltaTime);

            if (!stepActive)
            {
                TitanSupportFoot resolvedSupport = ResolveSupportFoot();
                if (resolvedSupport != supportFoot)
                {
                    SetSupportFoot(resolvedSupport, resolvedSupport == TitanSupportFoot.Left ? leftGroundContact : rightGroundContact);
                }
            }

            TitanSupportFoot swingFoot = GetOppositeSupportFoot(supportFoot);
            ref TitanLegControlState swingState = ref GetMutableLegState(swingFoot == TitanSupportFoot.Left);
            if (swingState.IsPlanted && swingState.FootLiftTarget > 0f)
            {
                BeginStep(swingFoot, swingState.DesiredGroundTarget);
                swingState.IsPlanted = false;
            }

            if (supportFoot == TitanSupportFoot.Left)
            {
                leftLeg.DesiredGroundTarget = leftLeg.PlantAnchorWorld;
                supportAnchorWorld = leftLeg.PlantAnchorWorld;

                if (!rightLeg.IsPlanted)
                {
                    UpdateAppliedLiftForSupportState(deltaTime);
                    TitanSwingTargetUpdate targetUpdate = ApplySwingHorizontalInput(false, ref rightLeg, rightCommand);
                    targetUpdate.Changed = targetUpdate.Changed || rightCommand.LiftInput > 0f;
                    CorrectDescendingLandingTargetBeforeSolve(false, ref rightLeg, rightGroundContact);
                    if (ClampSwingLiftToStrictFootPairRange(false, ref rightLeg))
                    {
                        targetUpdate.WorkspaceClamped = true;
                        FootTargetWorkspaceClampedThisTick = true;
                    }
                    UpdateActiveStepFootGroundedMotion(rightLeg);
                    if (stepActive)
                    {
                        activeStepSolveThisTick = CalculateActiveStepSolveResult(false, deltaTime, targetUpdate);
                    }
                }
            }
            else
            {
                rightLeg.DesiredGroundTarget = rightLeg.PlantAnchorWorld;
                supportAnchorWorld = rightLeg.PlantAnchorWorld;

                if (!leftLeg.IsPlanted)
                {
                    UpdateAppliedLiftForSupportState(deltaTime);
                    TitanSwingTargetUpdate targetUpdate = ApplySwingHorizontalInput(true, ref leftLeg, leftCommand);
                    targetUpdate.Changed = targetUpdate.Changed || leftCommand.LiftInput > 0f;
                    CorrectDescendingLandingTargetBeforeSolve(true, ref leftLeg, leftGroundContact);
                    if (ClampSwingLiftToStrictFootPairRange(true, ref leftLeg))
                    {
                        targetUpdate.WorkspaceClamped = true;
                        FootTargetWorkspaceClampedThisTick = true;
                    }
                    UpdateActiveStepFootGroundedMotion(leftLeg);
                    if (stepActive)
                    {
                        activeStepSolveThisTick = CalculateActiveStepSolveResult(true, deltaTime, targetUpdate);
                    }
                }
            }

            AssertSupportAnchorInvariant();
            if (leftLeg.IsPlanted && rightLeg.IsPlanted)
            {
                UpdateAppliedLiftForSupportState(deltaTime);
            }

            Vector3 desiredRoot;
            if (stepActive)
            {
                bool swingIsLeftForRoot = swingFoot == TitanSupportFoot.Left;
                if (!IsActiveStepSolveCurrent(swingIsLeftForRoot))
                {
                    activeStepSolveThisTick = CalculateActiveStepSolveResult(swingIsLeftForRoot, deltaTime, default);
                }

                desiredRoot = activeStepSolveThisTick.Valid ? activeStepSolveThisTick.FinalRootThisTick : MovementRoot.position;
            }
            else
            {
                desiredRoot = CalculateGroundedRootTarget(deltaTime, allowDoubleSupportRecovery: true);
            }

            if (stepActive && activeStepSolveThisTick.Valid)
            {
                RecordGroundedRootWriteIfAirborne();
                MovementRoot.position = activeStepSolveThisTick.FinalRootThisTick;
            }
            else
            {
                ApplyGroundedRootPosition(desiredRoot, deltaTime);
            }

            Vector3 up = TitanGroundFrame.Up;
            Vector3 leftTarget = supportFoot == TitanSupportFoot.Left
                ? supportAnchorWorld
                : leftLeg.DesiredGroundTarget + up * leftLeg.FootLift;
            Vector3 rightTarget = supportFoot == TitanSupportFoot.Right
                ? supportAnchorWorld
                : rightLeg.DesiredGroundTarget + up * rightLeg.FootLift;

            if (supportFoot == TitanSupportFoot.Left)
            {
                SolveLegOnce(true, leftTarget);
                SolveLegOnce(false, rightTarget);
            }
            else
            {
                SolveLegOnce(false, rightTarget);
                SolveLegOnce(true, leftTarget);
            }

            bool finalSwingIsLeft = swingFoot == TitanSupportFoot.Left;
            TitanLegControlState finalSwingState = finalSwingIsLeft ? leftLeg : rightLeg;
            Vector3 finalSwingTarget = finalSwingState.DesiredGroundTarget + up * finalSwingState.FootLift;
            FinalSwingTargetErrorThisTick = Vector3.Distance(finalSwingState.ActualFootPosition, finalSwingTarget);

            ApplyFootTargetRotation(true);
            ApplyFootTargetRotation(false);
            using (TitanLegPhysicsSyncMarker.Auto())
            {
                Physics.SyncTransforms();
            }
            bool swingIsLeftAfterSolve = swingFoot == TitanSupportFoot.Left;
            bool postSolveHasContact;
            FootGroundContact postSolveContact;
            using (TitanLegTouchdownContactMarker.Auto())
            {
                postSolveHasContact = TryGetFootGroundContactRobust(swingIsLeftAfterSolve, out postSolveContact);
            }
            if (swingIsLeftAfterSolve)
            {
                leftGroundContact = postSolveContact;
            }
            else
            {
                rightGroundContact = postSolveContact;
            }

            UpdateLegContactState(swingIsLeftAfterSolve, postSolveHasContact, postSolveContact);
            TryCompleteTouchdown(swingFoot, deltaTime);
            ValidatePlantedFeetAfterFinalSolve();
        }
    }

    private static TitanSupportFoot GetOppositeSupportFoot(TitanSupportFoot foot)
    {
        return foot == TitanSupportFoot.Left ? TitanSupportFoot.Right : TitanSupportFoot.Left;
    }

    private void BeginStep(TitanSupportFoot swingFoot, Vector3 unusedSwingTargetStartWorld)
    {
        activeStepSolveThisTick = default;
        stepActive = true;
        activeSwingFoot = swingFoot;
        activeStepFootGroundedEventArmed = true;
        TitanLegControlState swingState = GetLegState(swingFoot == TitanSupportFoot.Left);
        activeStepFootGroundedStartTarget = swingState.DesiredGroundTarget;
        activeStepMaxFootLift = Mathf.Max(0f, swingState.FootLift);
        activeStepMaxGroundTargetMove = 0f;
        touchdownRecoveryTimer = 0f;
        InvalidatePlantSurface(swingFoot == TitanSupportFoot.Left);
        stepRootReferenceWorld = TryCalculateHighestDoubleSupportRoot(out Vector3 settledRoot)
            ? settledRoot
            : MovementRoot.position;
        bool supportIsLeft = supportFoot == TitanSupportFoot.Left;
        TitanLegControlState supportState = supportIsLeft ? leftLeg : rightLeg;
        stepSupportPlantAnchor = supportState.PlantAnchorWorld;
    }

    private void ClearStepState()
    {
        activeStepSolveThisTick = default;
        stepActive = false;
        activeSwingFoot = TitanSupportFoot.Left;
        activeStepFootGroundedEventArmed = false;
        activeStepFootGroundedStartTarget = default;
        activeStepMaxFootLift = 0f;
        activeStepMaxGroundTargetMove = 0f;
        touchdownRecoveryTimer = 0f;
        stepRootReferenceWorld = default;
        stepSupportPlantAnchor = default;
    }

    private Vector3 CalculateGroundedRootTarget(float deltaTime, bool allowDoubleSupportRecovery)
    {
        Transform movementRoot = MovementRoot;
        Vector3 up = TitanGroundFrame.Up;
        if (stepActive)
        {
            return activeStepSolveThisTick.Valid ? activeStepSolveThisTick.FinalRootThisTick : movementRoot.position;
        }

        if (leftLeg.IsPlanted && rightLeg.IsPlanted)
        {
            if (TryCalculateHighestDoubleSupportRoot(out Vector3 rootTarget))
            {
                return rootTarget;
            }

            if (allowDoubleSupportRecovery && TryBeginUnreachableDoubleSupportRecovery())
            {
                return movementRoot.position;
            }

            if (!allowDoubleSupportRecovery)
            {
                return movementRoot.position;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[TitanRigRuntime] Double-support stance anchors are outside the shared leg workspace.", this);
#endif
        }

        return movementRoot.position;
    }

    private bool TryBeginUnreachableDoubleSupportRecovery()
    {
        if (stepActive || !supportAnchorValid || !groundTargetsInitialized || !leftLeg.IsPlanted || !rightLeg.IsPlanted)
        {
            return false;
        }

        bool releaseLeft = supportFoot != TitanSupportFoot.Left;
        TitanSupportFoot releaseFoot = releaseLeft ? TitanSupportFoot.Left : TitanSupportFoot.Right;
        ref TitanLegControlState released = ref GetMutableLegState(releaseLeft);
        BeginStep(releaseFoot, released.DesiredGroundTarget);
        released.IsPlanted = false;
        released.FootLift = 0f;
        released.FootLiftTarget = 0f;
        released.FootLiftSmoothVelocity = 0f;
        released.FootLiftFallVelocity = 0f;
        released.TargetWasClamped = false;
        released.LastSolveReached = false;
        released.ForceRecovery = true;
        released.PostureCanonicalizationPending = true;
        released.SolveCache = default;

        TitanLegControlState support = GetLegState(!releaseLeft);
        supportAnchorWorld = support.PlantAnchorWorld;
        return true;
    }

    private bool TryCreateWorkspace(bool left, Vector3 footTarget, out TitanLegRootWorkspace workspace)
    {
        EnsureLegKinematicModels();
        TitanLegKinematicModel model = left ? leftLegKinematicModel : rightLegKinematicModel;
        if (!model.Valid)
        {
            workspace = default;
            return false;
        }

        Transform movementRoot = MovementRoot;
        Matrix4x4 rootWorld = Matrix4x4.TRS(movementRoot.position, movementRoot.rotation, movementRoot.lossyScale);
        Vector3 hipWorld = rootWorld.MultiplyPoint3x4(model.HipOffsetRootLocal);
        workspace = new TitanLegRootWorkspace(
            hipWorld - movementRoot.position,
            footTarget,
            CalculateLegMaxReach(left));
        return true;
    }

    private float CalculateLegMaxReach(bool left)
    {
        EnsureLegKinematicModels();
        TitanLegKinematicModel model = left ? leftLegKinematicModel : rightLegKinematicModel;
        if (!model.Valid)
        {
            return 0f;
        }

        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        float physicalKneeMinRadians = Mathf.Abs(settings.KneeRoll.ToPhysicalAngle(settings.KneeRoll.MinAngle)) * Mathf.Deg2Rad;
        float maxReachSquared = model.UpperLength * model.UpperLength
            + model.LowerLength * model.LowerLength
            + 2f * model.UpperLength * model.LowerLength * Mathf.Cos(physicalKneeMinRadians);
        return Mathf.Max(0.001f, Mathf.Sqrt(Mathf.Max(0f, maxReachSquared)) - Mathf.Max(0f, settings.ReachMargin) - Mathf.Max(0f, stanceReachSafetyMargin));
    }

    private float GetActualFootReachTolerance(in TitanLegSolverSettings settings)
    {
        return Mathf.Max(plantedFootTargetTolerance, settings.PositionTolerance * 2f);
    }

    private void ApplyGroundedRootPosition(Vector3 desiredPosition, float deltaTime)
    {
        EnsureMovementRigidbodyCached();
        Transform movementRoot = MovementRoot;
        Vector3 nextPosition = Vector3.MoveTowards(
            movementRoot.position,
            desiredPosition,
            groundedRootMoveSpeed * deltaTime);
        if (leftLeg.IsPlanted && rightLeg.IsPlanted
            && !PreviewBothLegsCanReach(true, leftLeg.PlantAnchorWorld, false, rightLeg.PlantAnchorWorld, nextPosition))
        {
            nextPosition = movementRoot.position;
        }

        if (movementRigidbody != null)
        {
            RecordGroundedRootWriteIfAirborne();
            movementRigidbody.position = nextPosition;
            movementRigidbody.transform.position = nextPosition;
            ClearMovementRigidbodyVelocityIfDynamic();
            return;
        }

        RecordGroundedRootWriteIfAirborne();
        movementRoot.position = nextPosition;
    }

    private bool IsStableGroundedMotorState()
    {
        return groundingState == TitanLegGroundingState.Grounded
            && supportAnchorValid
            && groundTargetsInitialized;
    }

    private void RecordGroundedRootWriteIfAirborne()
    {
        if (!IsStableGroundedMotorState())
        {
            groundedRootWriteCountWhileAirborne++;
        }
    }

    private void TryCompleteTouchdown(TitanSupportFoot swingFoot, float deltaTime)
    {
        bool swingIsLeft = swingFoot == TitanSupportFoot.Left;
        ref TitanLegControlState swingState = ref GetMutableLegState(swingIsLeft);
        FootGroundContact swingContact = swingIsLeft ? leftGroundContact : rightGroundContact;
        Vector3 up = TitanGroundFrame.Up;
        FootAttachmentController attachment = swingIsLeft ? leftFootAttachment : rightFootAttachment;
        Vector3 contactPivot = swingState.HasGroundContact && swingContact.Collider != null
            ? ComputeFootPivotForGroundContact(swingIsLeft, swingContact)
            : swingState.DesiredGroundTarget;
        Vector3 plantAnchor = Vector3.ProjectOnPlane(swingState.DesiredGroundTarget, up) + up * Vector3.Dot(contactPivot, up);
        float minSoleGap = attachment != null && swingState.HasGroundContact
            ? attachment.GetMinimumSignedSoleGap(swingContact.Point, up)
            : float.NegativeInfinity;
        float maxSoleGap = attachment != null && swingState.HasGroundContact
            ? attachment.GetMaximumSignedSoleGap(swingContact.Point, up)
            : float.PositiveInfinity;
        Transform foot = swingIsLeft ? leftFoot : rightFoot;
        float actualTargetError = foot != null ? Vector3.Distance(foot.position, plantAnchor) : float.PositiveInfinity;
        TitanLegSolverSettings settings = swingIsLeft ? leftLegSolverSettings : rightLegSolverSettings;
        float actualTargetTolerance = GetActualFootReachTolerance(settings);
        bool basicTouchdownCandidate = stepActive
            && activeSwingFoot == swingFoot
            && swingState.FootLiftTarget == 0f
            && swingState.FootLift <= Mathf.Max(liftSnapEpsilon, touchdownLiftTolerance)
            && swingState.HasGroundContact
            && float.IsFinite(actualTargetError)
            && float.IsFinite(minSoleGap)
            && float.IsFinite(maxSoleGap)
            && minSoleGap >= -touchdownPenetrationTolerance
            && maxSoleGap >= -touchdownPenetrationTolerance;
        bool strictTouchdown = basicTouchdownCandidate
            && swingState.LastSolveReached
            && !swingState.TargetWasClamped
            && actualTargetError <= actualTargetTolerance
            && minSoleGap <= touchdownMaximumSoleGap;
        if (!strictTouchdown && !CanRecoverTouchdown(basicTouchdownCandidate, actualTargetError, minSoleGap, deltaTime))
        {
            return;
        }

        swingState.DesiredGroundTarget = plantAnchor;
        swingState.PlantAnchorWorld = plantAnchor;
        SetPlantSurface(swingIsLeft, swingContact);
        swingState.FootLift = 0f;
        swingState.FootLiftTarget = 0f;
        swingState.FootLiftSmoothVelocity = 0f;
        swingState.FootLiftFallVelocity = 0f;
        swingState.IsPlanted = true;
        swingState.ForceRecovery = true;
        swingState.PostureCanonicalizationPending = true;
        GetMutableLegState(!swingIsLeft).PostureCanonicalizationPending = true;
        pelvisAlignmentToTorsoRequested = true;
        UpdateActiveStepFootGroundedMotion(swingState);
        bool shouldEmitFootGrounded = activeStepFootGroundedEventArmed
            && activeSwingFoot == swingFoot
            && HasActiveStepMovedEnoughForFootGroundedFeedback();
        ClearStepState();
        if (shouldEmitFootGrounded)
        {
            FootGrounded?.Invoke(swingIsLeft);
        }
    }

    private void UpdateActiveStepFootGroundedMotion(in TitanLegControlState swingState)
    {
        if (!activeStepFootGroundedEventArmed)
        {
            return;
        }

        activeStepMaxFootLift = Mathf.Max(activeStepMaxFootLift, swingState.FootLift, swingState.FootLiftTarget);
        activeStepMaxGroundTargetMove = Mathf.Max(
            activeStepMaxGroundTargetMove,
            Vector3.Distance(swingState.DesiredGroundTarget, activeStepFootGroundedStartTarget));
    }

    private bool HasActiveStepMovedEnoughForFootGroundedFeedback()
    {
        return activeStepMaxFootLift >= footGroundedFeedbackMinimumLift
            || activeStepMaxGroundTargetMove >= footGroundedFeedbackMinimumMove;
    }

    private bool CanRecoverTouchdown(bool basicTouchdownCandidate, float actualTargetError, float minSoleGap, float deltaTime)
    {
        if (!basicTouchdownCandidate
            || actualTargetError > touchdownRecoveryTargetTolerance
            || minSoleGap > touchdownRecoveryTargetTolerance)
        {
            touchdownRecoveryTimer = 0f;
            return false;
        }

        touchdownRecoveryTimer += Mathf.Max(0f, deltaTime);
        return touchdownRecoveryTimer >= touchdownRecoveryDelay;
    }

    private void ValidatePlantedFeetAfterFinalSolve()
    {
        if (!IsStableGroundedMotorState())
        {
            return;
        }

        TitanPlantedFootValidation leftResult = default;
        TitanPlantedFootValidation rightResult = default;
        bool leftValid = !leftLeg.IsPlanted || ValidatePlantedFoot(true, leftPlantSurface, out leftResult);
        bool rightValid = !rightLeg.IsPlanted || ValidatePlantedFoot(false, rightPlantSurface, out rightResult);
        if (leftLeg.IsPlanted)
        {
            RecordPlantedValidationDiagnostics(true, leftResult);
        }

        if (rightLeg.IsPlanted)
        {
            RecordPlantedValidationDiagnostics(false, rightResult);
        }
        if (leftValid && rightValid)
        {
            UpdateLastValidGroundedPose();
            return;
        }

        bool severeLeft = leftLeg.IsPlanted && !leftValid && (leftResult.MinimumSignedSoleGap < -0.005f || leftResult.TargetError > 0.02f);
        bool severeRight = rightLeg.IsPlanted && !rightValid && (rightResult.MinimumSignedSoleGap < -0.005f || rightResult.TargetError > 0.02f);
        if ((severeLeft || severeRight) && TryRestoreLastValidGroundedPose())
        {
            return;
        }

        if (leftLeg.IsPlanted && !leftValid)
        {
            leftLeg.ForceRecovery = true;
            leftLeg.SolveCache = default;
            activeStepSolveThisTick = default;
        }

        if (rightLeg.IsPlanted && !rightValid)
        {
            rightLeg.ForceRecovery = true;
            rightLeg.SolveCache = default;
            activeStepSolveThisTick = default;
        }
    }

    private bool TryRestoreLastValidGroundedPose()
    {
        if (!IsStableGroundedMotorState()
            || (!leftLeg.IsPlanted && !rightLeg.IsPlanted)
            || !lastValidGroundedPose.Valid)
        {
            return false;
        }

        MovementRoot.SetPositionAndRotation(lastValidGroundedPose.RootPosition, lastValidGroundedPose.RootRotation);
        TitanConstrainedLegIkSolver.ApplyPose(leftHip, leftKnee, leftHipBaseRotation, leftKneeBaseRotation, leftLegSolverSettings, lastValidGroundedPose.LeftAngles);
        TitanConstrainedLegIkSolver.ApplyPose(rightHip, rightKnee, rightHipBaseRotation, rightKneeBaseRotation, rightLegSolverSettings, lastValidGroundedPose.RightAngles);
        leftLeg.SolvedAngles = lastValidGroundedPose.LeftAngles;
        rightLeg.SolvedAngles = lastValidGroundedPose.RightAngles;
        leftLeg.PlantAnchorWorld = lastValidGroundedPose.LeftPlantAnchor;
        rightLeg.PlantAnchorWorld = lastValidGroundedPose.RightPlantAnchor;
        leftLeg.SolveCache = default;
        rightLeg.SolveCache = default;
        activeStepSolveThisTick = default;
        ApplyFootTargetRotation(true);
        ApplyFootTargetRotation(false);
        using (TitanLegPhysicsSyncMarker.Auto())
        {
            Physics.SyncTransforms();
        }

        leftLeg.ActualFootPosition = leftFoot != null ? leftFoot.position : leftLeg.ActualFootPosition;
        rightLeg.ActualFootPosition = rightFoot != null ? rightFoot.position : rightLeg.ActualFootPosition;
        emergencyGroundPoseRestoreCount++;
        return true;
    }

    private bool ValidatePlantedFoot(bool left, in TitanPlantSurfaceState surface, out TitanPlantedFootValidation result)
    {
        result = default;
        TitanLegControlState state = left ? leftLeg : rightLeg;
        Transform foot = left ? leftFoot : rightFoot;
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        if (!state.IsPlanted || foot == null || attachment == null || !surface.Valid)
        {
            return false;
        }

        Vector3 planePoint = ResolvePlantSurfaceWorldPoint(surface);
        result.TargetError = Vector3.Distance(foot.position, state.PlantAnchorWorld);
        result.MinimumSignedSoleGap = attachment.GetMinimumSignedSoleGap(planePoint, TitanGroundFrame.Up);
        if (surface.GroundCollider != null && attachment.TryGetMaximumSolePenetration(surface.GroundCollider, out float penetration, out _))
        {
            result.MaximumColliderPenetration = penetration;
        }

        float actualTargetTolerance = GetActualFootReachTolerance(settings);
        result.RootPreviewReached = PreviewLegCanReach(left, state.PlantAnchorWorld, MovementRoot.position);
        result.ActualTransformReached = result.TargetError <= actualTargetTolerance;
        result.Valid = state.LastSolveReached
            && !state.TargetWasClamped
            && result.RootPreviewReached
            && result.ActualTransformReached
            && result.MinimumSignedSoleGap >= -plantedPenetrationTolerance;
        return result.Valid;
    }

    private void UpdateLastValidGroundedPose()
    {
        if (!IsStableGroundedMotorState())
        {
            return;
        }

        if (!leftLeg.IsPlanted && !rightLeg.IsPlanted)
        {
            return;
        }

        lastValidGroundedPose = new TitanValidGroundedPose
        {
            Valid = true,
            RootPosition = MovementRoot.position,
            RootRotation = MovementRoot.rotation,
            LeftAngles = leftLeg.SolvedAngles,
            RightAngles = rightLeg.SolvedAngles,
            LeftPlantAnchor = leftLeg.PlantAnchorWorld,
            RightPlantAnchor = rightLeg.PlantAnchorWorld,
        };
    }

    private void RecordPlantedValidationDiagnostics(bool left, in TitanPlantedFootValidation result)
    {
        if (left)
        {
            LeftActualFootTargetError = result.TargetError;
            LeftMinimumSignedSoleGap = result.MinimumSignedSoleGap;
            LeftMaximumColliderPenetration = result.MaximumColliderPenetration;
            LeftPlantedInvariantValid = result.Valid;
            LeftRootPreviewReached = result.RootPreviewReached;
            LeftActualTransformReached = result.ActualTransformReached;
        }
        else
        {
            RightActualFootTargetError = result.TargetError;
            RightMinimumSignedSoleGap = result.MinimumSignedSoleGap;
            RightMaximumColliderPenetration = result.MaximumColliderPenetration;
            RightPlantedInvariantValid = result.Valid;
            RightRootPreviewReached = result.RootPreviewReached;
            RightActualTransformReached = result.ActualTransformReached;
        }

        if (result.Valid)
        {
            return;
        }

        PlantedInvariantFailureCount++;
        if (PlantedInvariantFailureCount == 1)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TitanLegControlState state = left ? leftLeg : rightLeg;
            Debug.LogWarning($"[TitanRigRuntime] First planted-foot invariant failure side={(left ? "Left" : "Right")} stepActive={stepActive} support={supportFoot} swing={activeSwingFoot} plant={state.PlantAnchorWorld} desired={state.DesiredGroundTarget} actual={state.ActualFootPosition} predicted={state.PredictedFootPosition} targetError={result.TargetError:0.000000} minSoleGap={result.MinimumSignedSoleGap:0.000000} maxPenetration={result.MaximumColliderPenetration:0.000000} reached={state.LastSolveReached} clamped={state.TargetWasClamped} angles=({state.SolvedAngles.HipYaw:0.000},{state.SolvedAngles.HipRoll:0.000},{state.SolvedAngles.KneeRoll:0.000}) root=({MovementRoot.position}, {MovementRoot.rotation.eulerAngles}) preview={result.RootPreviewReached}", this);
#endif
        }
    }

    private void RecordLegPipelineTick()
    {
        if (lastLegRenderFrame != Time.frameCount)
        {
            lastLegRenderFrame = Time.frameCount;
            legTicksThisRenderedFrame = 0;
        }

        legTicksThisRenderedFrame++;

        double currentFixedTime = Time.fixedTimeAsDouble;
        if (System.Math.Abs(currentFixedTime - lastLegPipelineFixedTime) < 0.0000001)
        {
            legPipelineTickCountThisFixedFrame++;
        }
        else
        {
            lastLegPipelineFixedTime = currentFixedTime;
            legPipelineTickCountThisFixedFrame = 1;
        }

    }

    private bool TryGetFootGroundContact(bool left, out FootGroundContact contact)
    {
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        if (attachment == null)
        {
            contact = default;
            return false;
        }

        return attachment.TryGetGroundContact(TitanGroundFrame.Up, out contact);
    }

    private bool TryGetFootGroundContactRobust(bool left, out FootGroundContact contact)
    {
        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        if (attachment == null)
        {
            contact = default;
            return false;
        }

        TitanPlantSurfaceState surface = left ? leftPlantSurface : rightPlantSurface;
        bool mayUseExpectedSurface = surface.Valid
            && supportAnchorValid
            && groundTargetsInitialized
            && (groundingState == TitanLegGroundingState.Grounded || stepActive);
        return attachment.TryGetGroundContactRobust(
            TitanGroundFrame.Up,
            mayUseExpectedSurface,
            ResolvePlantSurfaceWorldPoint(surface),
            surface.GroundCollider,
            out contact);
    }

    private void UpdateLegContactState(bool left, bool hasContact, in FootGroundContact contact)
    {
        ref TitanLegControlState state = ref GetMutableLegState(left);
        state.HasGroundContact = hasContact;
        state.GroundContactPoint = hasContact ? contact.Point : default;
        state.GroundContactNormal = hasContact ? contact.Normal : default;
    }

    private void UpdateAirborneState(float deltaTime)
    {
        groundingStableTimer = 0f;
        groundLossTimer += deltaTime;
        if (groundLossTimer >= groundLossGraceTime)
        {
            InvalidateGroundedStance();
        }
    }

    private bool TryAdvanceLandingState(
        bool leftHasContact,
        in FootGroundContact leftContact,
        bool rightHasContact,
        in FootGroundContact rightContact,
        float deltaTime)
    {
        groundingState = TitanLegGroundingState.Landing;
        ApplyCurrentPhysicsMode();
        groundLossTimer = 0f;

        TitanSupportFoot candidateFoot = ChooseSupportFromContacts(leftHasContact, leftContact, rightHasContact, rightContact);
        FootGroundContact candidateContact = candidateFoot == TitanSupportFoot.Left ? leftContact : rightContact;
        if (!IsSameLandingCandidate(candidateFoot, candidateContact.Collider))
        {
            landingCandidateValid = true;
            landingCandidateFoot = candidateFoot;
            landingCandidateCollider = candidateContact.Collider;
            groundingStableTimer = 0f;
        }

        groundingStableTimer += deltaTime;
        if (groundingStableTimer < groundingStableTime)
        {
            return false;
        }

        InitializeGroundedStance(leftHasContact, leftContact, rightHasContact, rightContact);
        landingCandidateValid = false;
        landingCandidateCollider = null;
        return true;
    }

    private void UpdateGroundedContactState(bool leftHasContact, bool rightHasContact, float deltaTime)
    {
        bool supportHasContact = supportFoot == TitanSupportFoot.Left ? leftHasContact : rightHasContact;
        if (supportHasContact || ShouldMaintainAnchoredStanceWithoutContact())
        {
            groundingState = TitanLegGroundingState.Grounded;
            ApplyCurrentPhysicsMode();
            groundingStableTimer = groundingStableTime;
            groundLossTimer = 0f;
            return;
        }

        groundLossTimer += deltaTime;
        if (groundLossTimer > groundLossGraceTime)
        {
            InvalidateGroundedStance();
        }
    }

    private bool ShouldMaintainAnchoredStanceWithoutContact()
    {
        if (!supportAnchorValid || !groundTargetsInitialized || (!stepActive && !IsDoubleSupport))
        {
            return false;
        }

        Vector3 up = TitanGroundFrame.Up;
        Vector3 target = CalculateGroundedRootTarget(0f, allowDoubleSupportRecovery: false);
        return Vector3.Dot(MovementRoot.position, up) <= Vector3.Dot(target, up) + 0.05f;
    }

    private TitanSupportFoot ChooseSupportFromContacts(
        bool leftHasContact,
        in FootGroundContact leftContact,
        bool rightHasContact,
        in FootGroundContact rightContact)
    {
        if (leftHasContact && !rightHasContact)
        {
            return TitanSupportFoot.Left;
        }

        if (rightHasContact && !leftHasContact)
        {
            return TitanSupportFoot.Right;
        }

        float leftHeight = Vector3.Dot(ComputeFootPivotForGroundContact(true, leftContact), TitanGroundFrame.Up);
        float rightHeight = Vector3.Dot(ComputeFootPivotForGroundContact(false, rightContact), TitanGroundFrame.Up);
        return rightHeight < leftHeight - supportSwitchHysteresis ? TitanSupportFoot.Right : TitanSupportFoot.Left;
    }

    private void ApplyLandingFootRotations()
    {
        ApplyFootTargetRotation(true);
        ApplyFootTargetRotation(false);
    }

    public void IntegrateLiftTargetForTests(bool left, float liftInput, float deltaTime)
    {
        EnsureSolverSettingsInitialized();
        EnsureLegStateInitialized(left);
        ref TitanLegControlState state = ref GetMutableLegState(left);
        IntegrateLiftTarget(ref state, liftInput, deltaTime);
    }

    private void IntegrateLiftTarget(ref TitanLegControlState state, float liftInput, float deltaTime)
    {
        if (liftInput > 0f)
        {
            state.FootLiftFallVelocity = 0f;
            state.FootLiftTarget = Mathf.Clamp(state.FootLiftTarget + liftInput * footLiftRiseSpeed * deltaTime, 0f, maxFootLift);
            return;
        }

        state.FootLiftFallVelocity = Mathf.Min(footLiftMaxFallSpeed, state.FootLiftFallVelocity + footLiftFallAcceleration * deltaTime);
        state.FootLiftTarget = Mathf.Max(0f, state.FootLiftTarget - state.FootLiftFallVelocity * deltaTime);
        if (state.FootLiftTarget <= liftSnapEpsilon)
        {
            state.FootLiftTarget = 0f;
            state.FootLiftFallVelocity = 0f;
        }
    }

    private TitanSupportFoot ResolveSupportFoot()
    {
        Vector3 up = TitanGroundFrame.Up;
        float leftCandidateHeight = Vector3.Dot(leftLeg.DesiredGroundTarget + up * leftLeg.FootLiftTarget, up);
        float rightCandidateHeight = Vector3.Dot(rightLeg.DesiredGroundTarget + up * rightLeg.FootLiftTarget, up);
        return TitanLegSupportResolver.Resolve(
            supportFoot,
            leftCandidateHeight,
            rightCandidateHeight,
            leftLeg.FootLift,
            rightLeg.FootLift,
            leftLeg.HasGroundContact && groundingState == TitanLegGroundingState.Grounded,
            rightLeg.HasGroundContact && groundingState == TitanLegGroundingState.Grounded,
            supportSwitchHysteresis,
            supportContactTolerance);
    }

    private void UpdateAppliedLiftForSupportState(float deltaTime)
    {
        if (supportFoot == TitanSupportFoot.Left)
        {
            UpdateSupportAppliedLift(ref leftLeg);
            UpdateSwingAppliedLift(ref rightLeg, deltaTime);
            return;
        }

        UpdateSupportAppliedLift(ref rightLeg);
        UpdateSwingAppliedLift(ref leftLeg, deltaTime);
    }

    private static void UpdateSupportAppliedLift(ref TitanLegControlState state)
    {
        state.FootLift = 0f;
        state.FootLiftSmoothVelocity = 0f;
    }

    private void UpdateSwingAppliedLift(ref TitanLegControlState state, float deltaTime)
    {
        state.FootLift = Mathf.SmoothDamp(
            state.FootLift,
            state.FootLiftTarget,
            ref state.FootLiftSmoothVelocity,
            footLiftSmoothTime,
            Mathf.Infinity,
            deltaTime);

        if (state.FootLiftTarget <= liftSnapEpsilon)
        {
            state.FootLiftTarget = 0f;
        }

        if (state.FootLift < liftSnapEpsilon && state.FootLiftTarget <= 0f)
        {
            state.FootLift = 0f;
            state.FootLiftSmoothVelocity = 0f;
        }
    }

    private TitanSwingTargetUpdate ApplySwingHorizontalInput(bool swingIsLeft, ref TitanLegControlState state, in TitanLegInputCommand command)
    {
        TitanSwingTargetUpdate result = default;
        if (!CanApplySwingHorizontalInput(swingIsLeft, state))
        {
            return result;
        }

        if (command.HorizontalDelta.sqrMagnitude <= HorizontalInputEpsilonSqr)
        {
            return result;
        }

        Vector3 up = TitanGroundFrame.Up;
        Vector3 pelvisForward = Vector3.ProjectOnPlane(MovementRoot.forward, up);
        if (pelvisForward.sqrMagnitude < 0.0001f)
            pelvisForward = TitanGroundFrame.WorldForward;

        pelvisForward.Normalize();
        Vector3 pelvisRight = Vector3.Cross(up, pelvisForward).normalized;
        Vector3 requestedWorldDelta = (pelvisRight * command.HorizontalDelta.x
            + pelvisForward * command.HorizontalDelta.y) * footMoveSensitivity;
        Vector3 previous = state.DesiredGroundTarget;
        float groundHeight = Vector3.Dot(previous, up);
        Vector3 requested = Vector3.ProjectOnPlane(previous + requestedWorldDelta, up) + up * groundHeight;
        result.PreviousTarget = previous;
        result.RequestedTarget = requested;
        result.RequestedWorldDelta = requestedWorldDelta;
        ConsumedMouseDeltaThisTick = command.HorizontalDelta;
        RequestedFootWorldDeltaThisTick = requestedWorldDelta;
        if ((requested - previous).sqrMagnitude <= HorizontalInputEpsilonSqr)
        {
            return result;
        }

        TitanStrictSwingTargetResult strictTarget = ClampSwingGroundTargetToStrictFootPairRange(
            swingIsLeft,
            previous,
            requested,
            state.FootLift);
        Vector3 accepted = strictTarget.Target;
        if ((accepted - previous).sqrMagnitude <= HorizontalInputEpsilonSqr)
        {
            result.AcceptedTarget = previous;
            result.WorkspaceClamped = strictTarget.Clamped;
            AcceptedFootWorldDeltaThisTick = default;
            FootInputAcceptanceRatioThisTick = 0f;
            FootTargetWorkspaceClampedThisTick = strictTarget.Clamped;
            return result;
        }

        state.DesiredGroundTarget = accepted;
        result.AcceptedTarget = accepted;
        result.Changed = true;
        result.WorkspaceClamped = strictTarget.Clamped;
        activeStepSolveThisTick = default;
        AcceptedFootWorldDeltaThisTick = accepted - previous;
        float requestedDistance = Vector3.Distance(requested, previous);
        FootInputAcceptanceRatioThisTick = requestedDistance > StrictSwingTargetEpsilon
            ? Mathf.Clamp01(Vector3.Distance(accepted, previous) / requestedDistance)
            : 1f;
        FootTargetWorkspaceClampedThisTick = strictTarget.Clamped;
        return result;
    }

    private TitanStrictSwingTargetResult ClampSwingGroundTargetToStrictFootPairRange(
        bool swingIsLeft,
        Vector3 previousGroundTarget,
        Vector3 requestedGroundTarget,
        float currentLift)
    {
        Vector3 up = TitanGroundFrame.Up;
        Vector3 requestedSwingTarget = requestedGroundTarget + up * currentLift;
        if (IsSwingTargetInsideStrictFootPairRange(swingIsLeft, requestedSwingTarget))
        {
            return new TitanStrictSwingTargetResult(requestedGroundTarget, false);
        }

        Vector3 previousSwingTarget = previousGroundTarget + up * currentLift;
        if (!TryProjectSwingTargetToStrictFootPairRange(swingIsLeft, requestedSwingTarget, out Vector3 clampedSwingTarget))
        {
            return new TitanStrictSwingTargetResult(previousGroundTarget, true);
        }

        float requestedLiftHeight = Vector3.Dot(requestedSwingTarget, up);
        if (Mathf.Abs(Vector3.Dot(clampedSwingTarget, up) - requestedLiftHeight) > StrictSwingTargetEpsilon)
        {
            return new TitanStrictSwingTargetResult(previousGroundTarget, true);
        }

        float groundHeight = Vector3.Dot(requestedGroundTarget, up);
        Vector3 clampedGroundTarget = Vector3.ProjectOnPlane(clampedSwingTarget, up) + up * groundHeight;
        if ((clampedGroundTarget - previousGroundTarget).sqrMagnitude <= HorizontalInputEpsilonSqr
            && IsSwingTargetInsideStrictFootPairRange(swingIsLeft, previousSwingTarget))
        {
            return new TitanStrictSwingTargetResult(previousGroundTarget, true);
        }

        return new TitanStrictSwingTargetResult(clampedGroundTarget, true);
    }

    private bool TryProjectSwingTargetToStrictFootPairRange(bool swingIsLeft, Vector3 requestedSwingTarget, out Vector3 clampedSwingTarget)
    {
        bool supportIsLeft = !swingIsLeft;
        TitanLegControlState supportState = GetLegState(supportIsLeft);
        Vector3 supportTarget = supportState.PlantAnchorWorld;
        if (!TryCreateWorkspace(supportIsLeft, supportTarget, out TitanLegRootWorkspace supportWorkspace)
            || !TryCreateWorkspace(swingIsLeft, requestedSwingTarget, out TitanLegRootWorkspace requestedSwingWorkspace))
        {
            clampedSwingTarget = requestedSwingTarget;
            return false;
        }

        Vector3 up = TitanGroundFrame.Up;
        Vector3 supportCenter = supportWorkspace.Center;
        Vector3 requestedCenter = requestedSwingWorkspace.Center;
        Vector3 supportToRequested = requestedCenter - supportCenter;
        float maxCenterDistance = Mathf.Max(
            0f,
            supportWorkspace.MaxReach + requestedSwingWorkspace.MaxReach - StrictSwingTargetEpsilon);

        float verticalDelta = Vector3.Dot(supportToRequested, up);
        float planarLimitSquared = maxCenterDistance * maxCenterDistance - verticalDelta * verticalDelta;
        Vector3 clampedCenter;
        if (planarLimitSquared <= 0f)
        {
            clampedCenter = supportCenter + up * Mathf.Clamp(verticalDelta, -maxCenterDistance, maxCenterDistance);
        }
        else
        {
            Vector3 planarDelta = Vector3.ProjectOnPlane(supportToRequested, up);
            float planarLimit = Mathf.Sqrt(planarLimitSquared);
            clampedCenter = supportCenter
                + up * verticalDelta
                + Vector3.ClampMagnitude(planarDelta, planarLimit);
        }

        clampedSwingTarget = clampedCenter + requestedSwingWorkspace.HipOffsetFromRoot;
        return IsSwingTargetInsideStrictFootPairRange(swingIsLeft, clampedSwingTarget);
    }

    private bool ClampSwingLiftToStrictFootPairRange(bool swingIsLeft, ref TitanLegControlState state)
    {
        Vector3 liftedTarget = state.DesiredGroundTarget + TitanGroundFrame.Up * state.FootLift;
        if (IsSwingTargetInsideStrictFootPairRange(swingIsLeft, liftedTarget))
        {
            return false;
        }

        Vector3 groundTarget = state.DesiredGroundTarget;
        if (!IsSwingTargetInsideStrictFootPairRange(swingIsLeft, groundTarget))
        {
            return false;
        }

        float low = 0f;
        float high = Mathf.Max(0f, state.FootLift);
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            Vector3 candidate = state.DesiredGroundTarget + TitanGroundFrame.Up * mid;
            if (IsSwingTargetInsideStrictFootPairRange(swingIsLeft, candidate))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        state.FootLift = low;
        state.FootLiftTarget = Mathf.Min(state.FootLiftTarget, low);
        state.FootLiftSmoothVelocity = 0f;
        state.FootLiftFallVelocity = 0f;
        activeStepSolveThisTick = default;
        return true;
    }

    private bool IsSwingTargetInsideStrictFootPairRange(bool swingIsLeft, Vector3 swingTarget)
    {
        bool supportIsLeft = !swingIsLeft;
        TitanLegControlState supportState = GetLegState(supportIsLeft);
        return TryCreateWorkspace(supportIsLeft, supportState.PlantAnchorWorld, out TitanLegRootWorkspace supportWorkspace)
            && TryCreateWorkspace(swingIsLeft, swingTarget, out TitanLegRootWorkspace swingWorkspace)
            && TitanStanceRootSolver.SolveClosestSharedWorkspace(
                MovementRoot.position,
                TitanGroundFrame.Up,
                supportWorkspace,
                swingWorkspace).Feasible;
    }

    private void CorrectDescendingLandingTargetBeforeSolve(bool left, ref TitanLegControlState state, in FootGroundContact contact)
    {
        if (!stepActive || state.IsPlanted || state.FootLiftTarget != 0f || state.FootLift <= 0f || contact.Collider == null)
        {
            return;
        }

        FootAttachmentController attachment = left ? leftFootAttachment : rightFootAttachment;
        if (attachment == null)
        {
            return;
        }

        Vector3 up = TitanGroundFrame.Up;
        Vector3 corrected = attachment.ComputePivotTargetForGroundPlane(
            state.DesiredGroundTarget,
            contact.Point,
            up,
            plantedSoleClearance);
        state.DesiredGroundTarget = Vector3.ProjectOnPlane(state.DesiredGroundTarget, up) + up * Vector3.Dot(corrected, up);
    }

    private bool CanApplySwingHorizontalInput(bool swingIsLeft, in TitanLegControlState state)
    {
        TitanSupportFoot expected = swingIsLeft
            ? TitanSupportFoot.Left
            : TitanSupportFoot.Right;

        return stepActive
            && activeSwingFoot == expected
            && !state.IsPlanted
            && (state.FootLiftTarget > 0f || state.FootLift > liftSnapEpsilon);
    }

    private bool IsActiveStepSolveCurrent(bool swingIsLeft)
    {
        if (!activeStepSolveThisTick.Valid)
        {
            return false;
        }

        TitanLegControlState swingState = GetLegState(swingIsLeft);
        Vector3 currentSwingTarget = swingState.DesiredGroundTarget + TitanGroundFrame.Up * swingState.FootLift;
        return (activeStepSolveThisTick.AcceptedGroundTarget - swingState.DesiredGroundTarget).sqrMagnitude <= RootQueryEpsilonSqr
            && (activeStepSolveThisTick.CurrentSwingTarget - currentSwingTarget).sqrMagnitude <= RootQueryEpsilonSqr;
    }

    private TitanActiveStepSolveResult CalculateActiveStepSolveResult(bool swingIsLeft, float deltaTime, in TitanSwingTargetUpdate targetUpdate)
    {
        TitanActiveStepSolveResult result = default;
        TitanLegControlState swingState = GetLegState(swingIsLeft);
        Vector3 currentSwingTarget = swingState.DesiredGroundTarget + TitanGroundFrame.Up * swingState.FootLift;
        result.AcceptedGroundTarget = swingState.DesiredGroundTarget;
        result.CurrentSwingTarget = currentSwingTarget;
        result.HorizontalTargetChanged = targetUpdate.Changed;
        result.TargetWasWorkspaceClamped = targetUpdate.WorkspaceClamped;

        bool geometrySolved = TrySolveActiveStepRootGeometry(
                MovementRoot.position,
                swingIsLeft,
                currentSwingTarget,
                out Vector3 requiredRoot,
                out _,
                out _);
        if (!geometrySolved)
        {
            if (!TryFindConstrainedStepRootFallback(MovementRoot.position, swingIsLeft, currentSwingTarget, out requiredRoot))
            {
                return result;
            }

            result.UsedConstraintFallback = true;
        }
        bool requiredRootChanged = (requiredRoot - MovementRoot.position).sqrMagnitude > RootQueryEpsilonSqr;
        if (geometrySolved
            && ((requiredRootChanged && !PreviewActiveStepRootCanReach(swingIsLeft, currentSwingTarget, requiredRoot))
            || ShouldUseConstrainedRootFallback(true, requiredRoot, swingIsLeft, currentSwingTarget)))
        {
            if (TryFindConstrainedStepRootFallback(MovementRoot.position, swingIsLeft, currentSwingTarget, out Vector3 constrainedRoot))
            {
                requiredRoot = constrainedRoot;
                result.UsedConstraintFallback = true;
            }
        }

        Vector3 preferredRoot = requiredRoot;
        if (!TrySolveActiveStepRootGeometry(
                stepRootReferenceWorld,
                swingIsLeft,
                currentSwingTarget,
                out preferredRoot,
                out _,
                out _))
        {
            preferredRoot = requiredRoot;
        }

        Vector3 finalRoot = targetUpdate.Changed
            ? requiredRoot
            : Vector3.MoveTowards(requiredRoot, preferredRoot, groundedRootMoveSpeed * deltaTime);
        if ((finalRoot - MovementRoot.position).sqrMagnitude > RootQueryEpsilonSqr
            && !PreviewActiveStepRootCanReach(swingIsLeft, currentSwingTarget, finalRoot))
        {
            finalRoot = requiredRoot;
        }

        result.Valid = true;
        result.RequiredRoot = requiredRoot;
        result.PreferredRoot = preferredRoot;
        result.FinalRootThisTick = finalRoot;
        RequiredRootCorrectionThisTick = requiredRoot - MovementRoot.position;
        RootFallbackUsedThisTick = result.UsedConstraintFallback;
        return result;
    }

    private bool PreviewActiveStepRootCanReach(bool swingIsLeft, Vector3 swingTarget, Vector3 rootPosition)
    {
        bool supportIsLeft = !swingIsLeft;
        activeStepConstrainedSupportPreviewCountThisFixedFrame++;
        bool supportReached = PreviewLegCanReach(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, rootPosition);
        if (supportIsLeft)
        {
            LeftRootPreviewReached = supportReached;
        }
        else
        {
            RightRootPreviewReached = supportReached;
        }
        return supportReached && PreviewLegCanReach(swingIsLeft, swingTarget, rootPosition);
    }

    private bool IsSwingTargetGloballyFeasible(bool swingIsLeft, Vector3 groundTarget, float currentFootLift)
    {
        Vector3 currentSwingTarget = groundTarget + TitanGroundFrame.Up * currentFootLift;
        return TrySolveActiveStepRootGeometry(MovementRoot.position, swingIsLeft, currentSwingTarget, out _, out _, out _)
            && TrySolveActiveStepRootGeometry(stepRootReferenceWorld, swingIsLeft, groundTarget, out _, out _, out _);
    }

    private bool TrySolveActiveStepRootGeometry(
        Vector3 referenceRoot,
        bool swingIsLeft,
        Vector3 swingTarget,
        out Vector3 rootTarget,
        out TitanLegRootWorkspace supportWorkspace,
        out TitanLegRootWorkspace swingWorkspace)
    {
        using (TitanLegActiveRootGeometryMarker.Auto())
        {
            rootGeometrySolveCountThisFixedFrame++;
            bool supportIsLeft = !swingIsLeft;
            supportWorkspace = default;
            swingWorkspace = default;
            if (!TryCreateWorkspace(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, out supportWorkspace)
                || !TryCreateWorkspace(swingIsLeft, swingTarget, out swingWorkspace))
            {
                rootTarget = MovementRoot.position;
                return false;
            }

            if (IsInsideWorkspace(referenceRoot, supportWorkspace, 0f)
                && IsInsideWorkspace(referenceRoot, swingWorkspace, 0f))
            {
                rootTarget = referenceRoot;
                return true;
            }

            TitanStanceRootResult preserveHeight = TitanStanceRootSolver.SolveClosestSharedWorkspacePreserveHeight(
                referenceRoot,
                TitanGroundFrame.Up,
                supportWorkspace,
                swingWorkspace);
            if (preserveHeight.Feasible)
            {
                rootTarget = preserveHeight.RootPosition;
                return true;
            }

            TitanStanceRootResult closest = TitanStanceRootSolver.SolveClosestSharedWorkspace(
                referenceRoot,
                TitanGroundFrame.Up,
                supportWorkspace,
                swingWorkspace);
            rootTarget = closest.Feasible ? closest.RootPosition : MovementRoot.position;
            return closest.Feasible;
        }
    }

    private static bool IsInsideWorkspace(Vector3 root, in TitanLegRootWorkspace workspace, float margin)
    {
        float radius = Mathf.Max(0f, workspace.MaxReach - margin);
        return (root - workspace.Center).sqrMagnitude <= radius * radius;
    }

    private bool ShouldUseConstrainedRootFallback(
        bool geometryFeasible,
        Vector3 geometryRoot,
        bool swingIsLeft,
        Vector3 swingTarget)
    {
        if (!geometryFeasible || !float.IsFinite(geometryRoot.x) || !float.IsFinite(geometryRoot.y) || !float.IsFinite(geometryRoot.z))
        {
            return true;
        }

        TitanLegControlState support = GetLegState(!swingIsLeft);
        TitanLegControlState swing = GetLegState(swingIsLeft);
        bool supportIsLeft = !swingIsLeft;
        bool supportFailureForSameQuery = IsPreviousSolveFailureForSameQuery(supportIsLeft, support, support.PlantAnchorWorld, geometryRoot);
        bool swingFailureForSameQuery = IsPreviousSolveFailureForSameQuery(swingIsLeft, swing, swingTarget, geometryRoot);
        return supportFailureForSameQuery || swingFailureForSameQuery;
    }

    private bool IsPreviousSolveFailureForSameQuery(
        bool left,
        in TitanLegControlState state,
        Vector3 target,
        Vector3 rootPosition)
    {
        if ((target - state.LastSolveTarget).sqrMagnitude > RootQueryEpsilonSqr
            || (rootPosition - state.LastSolveRootPosition).sqrMagnitude > RootQueryEpsilonSqr
            || Quaternion.Angle(MovementRoot.rotation, state.LastSolveRootRotation) > 0.001f)
        {
            return false;
        }

        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        return state.TargetWasClamped
            || state.DesiredPositionError > GetActualFootReachTolerance(settings);
    }

    private bool TryFindConstrainedStepRootFallback(Vector3 referenceRoot, bool swingIsLeft, Vector3 swingTarget, out Vector3 rootTarget)
    {
        using (TitanLegRootConstraintFallbackMarker.Auto())
        {
            bool supportIsLeft = !swingIsLeft;
            if (!TryCreateWorkspace(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, out TitanLegRootWorkspace support)
                || !TryCreateWorkspace(swingIsLeft, swingTarget, out TitanLegRootWorkspace swing))
            {
                rootTarget = MovementRoot.position;
                return false;
            }

            Vector3 up = TitanGroundFrame.Up;
            rootTarget = MovementRoot.position;
            int count = 0;
            AddSameHeightDiskIntersectionSamples(referenceRoot, up, support, swing, ref count);
            if (TryAcceptRootCandidate(TitanStanceRootSolver.SolveClosestSharedWorkspacePreserveHeight(referenceRoot, up, support, swing), supportIsLeft, swingIsLeft, swingTarget, out rootTarget))
            {
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = stepRootCandidateBuffer[i];
                if (PreviewBothLegsCanReach(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, swingIsLeft, swingTarget, candidate))
                {
                    rootTarget = candidate;
                    return true;
                }
            }

            if (TryAcceptRootCandidate(TitanStanceRootSolver.SolveClosestSharedWorkspace(referenceRoot, up, support, swing), supportIsLeft, swingIsLeft, swingTarget, out rootTarget)
                || TryAcceptRootCandidate(new TitanStanceRootResult(ProjectPointIntoWorkspace(referenceRoot, support), true), supportIsLeft, swingIsLeft, swingTarget, out rootTarget)
                || TryAcceptRootCandidate(new TitanStanceRootResult(ProjectPointIntoWorkspace(referenceRoot, swing), true), supportIsLeft, swingIsLeft, swingTarget, out rootTarget))
            {
                return true;
            }

            count = 0;
            AddSphereIntersectionSamples(referenceRoot, up, support, swing, ref count);
            if (count > 0)
            {
                exhaustiveRootFallbackCountThisFixedFrame++;
            }
            SortRootCandidatesByDistance(referenceRoot, count);
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = stepRootCandidateBuffer[i];
                if (PreviewBothLegsCanReach(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, swingIsLeft, swingTarget, candidate))
                {
                    rootTarget = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    private bool TryAcceptRootCandidate(TitanStanceRootResult result, bool supportIsLeft, bool swingIsLeft, Vector3 swingTarget, out Vector3 rootTarget)
    {
        rootTarget = result.RootPosition;
        return result.Feasible
            && PreviewBothLegsCanReach(supportIsLeft, GetLegState(supportIsLeft).PlantAnchorWorld, swingIsLeft, swingTarget, result.RootPosition);
    }

    private void SortRootCandidatesByDistance(Vector3 referenceRoot, int count)
    {
        for (int i = 1; i < count; i++)
        {
            Vector3 candidate = stepRootCandidateBuffer[i];
            float candidateDistance = (candidate - referenceRoot).sqrMagnitude;
            int j = i - 1;
            while (j >= 0 && (stepRootCandidateBuffer[j] - referenceRoot).sqrMagnitude > candidateDistance)
            {
                stepRootCandidateBuffer[j + 1] = stepRootCandidateBuffer[j];
                j--;
            }

            stepRootCandidateBuffer[j + 1] = candidate;
        }
    }

    private bool TryCalculateHighestDoubleSupportRoot(out Vector3 rootTarget)
    {
        rootTarget = MovementRoot.position;
        if (!TryCreateWorkspace(true, leftLeg.PlantAnchorWorld, out TitanLegRootWorkspace left)
            || !TryCreateWorkspace(false, rightLeg.PlantAnchorWorld, out TitanLegRootWorkspace right))
        {
            return false;
        }

        TitanStanceRootResult result = TitanStanceRootSolver.SolveHighestDoubleSupport(MovementRoot.position, TitanGroundFrame.Up, left, right);
        if (!result.Feasible)
        {
            if (PreviewBothLegsCanReach(true, leftLeg.PlantAnchorWorld, false, rightLeg.PlantAnchorWorld, MovementRoot.position))
            {
                rootTarget = MovementRoot.position;
                return true;
            }

            Vector3 fixedRootPlanar = Vector3.ProjectOnPlane(MovementRoot.position, TitanGroundFrame.Up);
            TitanStanceRootResult fixedPlanar = TitanStanceRootSolver.SolveFixedPlanarHighest(
                fixedRootPlanar,
                MovementRoot.position,
                TitanGroundFrame.Up,
                left,
                right);
            if (!fixedPlanar.Feasible)
            {
                return false;
            }

            result = fixedPlanar;
        }

        doubleSupportConstrainedTargetCacheMissCount++;
        if (PreviewBothLegsCanReach(true, leftLeg.PlantAnchorWorld, false, rightLeg.PlantAnchorWorld, result.RootPosition))
        {
            rootTarget = result.RootPosition;
            return true;
        }

        Vector3 low = lastValidGroundedPose.Valid ? lastValidGroundedPose.RootPosition : MovementRoot.position;
        if (!PreviewBothLegsCanReach(true, leftLeg.PlantAnchorWorld, false, rightLeg.PlantAnchorWorld, low))
        {
            return false;
        }

        Vector3 high = result.RootPosition;
        for (int i = 0; i < 8; i++)
        {
            Vector3 mid = Vector3.LerpUnclamped(low, high, 0.5f);
            if (PreviewBothLegsCanReach(true, leftLeg.PlantAnchorWorld, false, rightLeg.PlantAnchorWorld, mid))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        rootTarget = low;
        return true;
    }

    private void AddRootCandidate(TitanStanceRootResult result, ref int count)
    {
        if (!result.Feasible || count >= stepRootCandidateBuffer.Length)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (Vector3.Distance(stepRootCandidateBuffer[i], result.RootPosition) <= 0.0001f)
            {
                return;
            }
        }

        stepRootCandidateBuffer[count++] = result.RootPosition;
    }

    private static Vector3 ProjectPointIntoWorkspace(Vector3 point, TitanLegRootWorkspace workspace)
    {
        Vector3 center = workspace.Center;
        float radius = Mathf.Max(0f, workspace.MaxReach);
        Vector3 offset = point - center;
        float distance = offset.magnitude;
        if (distance <= radius + 0.00001f || distance <= 0.00001f)
        {
            return point;
        }

        return center + offset / distance * radius;
    }

    private void AddSphereIntersectionSamples(
        Vector3 referenceRoot,
        Vector3 up,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing,
        ref int count)
    {
        Vector3 centerA = support.Center;
        Vector3 centerB = swing.Center;
        float radiusA = Mathf.Max(0f, support.MaxReach);
        float radiusB = Mathf.Max(0f, swing.MaxReach);
        Vector3 delta = centerB - centerA;
        float distance = delta.magnitude;
        if (distance <= 0.00001f
            || distance > radiusA + radiusB + 0.00001f
            || distance + Mathf.Min(radiusA, radiusB) < Mathf.Max(radiusA, radiusB) - 0.00001f)
        {
            return;
        }

        Vector3 axis = delta / distance;
        float x = (radiusA * radiusA - radiusB * radiusB + distance * distance) / (2f * distance);
        Vector3 circleCenter = centerA + axis * x;
        float radiusSquared = radiusA * radiusA - x * x;
        if (radiusSquared < -0.00001f)
        {
            return;
        }

        float circleRadius = Mathf.Sqrt(Mathf.Max(0f, radiusSquared));
        Vector3 tangentA = Vector3.ProjectOnPlane(referenceRoot - circleCenter, axis);
        if (tangentA.sqrMagnitude <= 0.00001f)
        {
            tangentA = Vector3.ProjectOnPlane(up, axis);
        }

        if (tangentA.sqrMagnitude <= 0.00001f)
        {
            tangentA = Vector3.ProjectOnPlane(Vector3.right, axis);
        }

        if (tangentA.sqrMagnitude <= 0.00001f)
        {
            return;
        }

        tangentA.Normalize();
        Vector3 tangentB = Vector3.Cross(axis, tangentA).normalized;
        const int SampleCount = 24;
        for (int i = 0; i < SampleCount; i++)
        {
            float angle = i * Mathf.PI * 2f / SampleCount;
            Vector3 candidate = circleCenter + (tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle)) * circleRadius;
            AddRootCandidate(new TitanStanceRootResult(candidate, true), ref count);
        }
    }

    private void AddSameHeightDiskIntersectionSamples(
        Vector3 referenceRoot,
        Vector3 up,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing,
        ref int count)
    {
        float referenceHeight = Vector3.Dot(referenceRoot, up);
        if (!TryBuildRuntimeDisk(support, up, referenceHeight, out Vector3 centerA, out float radiusA)
            || !TryBuildRuntimeDisk(swing, up, referenceHeight, out Vector3 centerB, out float radiusB))
        {
            return;
        }

        Vector3 delta = centerB - centerA;
        float distance = delta.magnitude;
        if (distance <= 0.00001f || distance > radiusA + radiusB + 0.00001f)
        {
            return;
        }

        Vector3 axis = delta / distance;
        float x = (radiusA * radiusA - radiusB * radiusB + distance * distance) / (2f * distance);
        float hSquared = radiusA * radiusA - x * x;
        if (hSquared < -0.00001f)
        {
            return;
        }

        Vector3 basePoint = centerA + axis * x;
        Vector3 perpendicular = Vector3.Cross(up, axis);
        if (perpendicular.sqrMagnitude <= 0.00001f)
        {
            return;
        }

        perpendicular.Normalize();
        float h = Mathf.Sqrt(Mathf.Max(0f, hSquared));
        AddRootCandidate(new TitanStanceRootResult(basePoint + perpendicular * h + up * referenceHeight, true), ref count);
        AddRootCandidate(new TitanStanceRootResult(basePoint - perpendicular * h + up * referenceHeight, true), ref count);
    }

    private static bool TryBuildRuntimeDisk(TitanLegRootWorkspace workspace, Vector3 up, float referenceHeight, out Vector3 center, out float radius)
    {
        float verticalDelta = referenceHeight - Vector3.Dot(workspace.Center, up);
        float maxReach = Mathf.Max(0f, workspace.MaxReach);
        float radiusSquared = maxReach * maxReach - verticalDelta * verticalDelta;
        if (radiusSquared < -0.00001f)
        {
            center = default;
            radius = 0f;
            return false;
        }

        center = Vector3.ProjectOnPlane(workspace.Center, up);
        radius = Mathf.Sqrt(Mathf.Max(0f, radiusSquared));
        return true;
    }

    private bool PreferRootCandidate(Vector3 candidate, Vector3 best, Vector3 referenceRoot, Vector3 up)
    {
        float referenceHeight = Vector3.Dot(referenceRoot, up);
        bool candidatePreservesHeight = Mathf.Abs(Vector3.Dot(candidate, up) - referenceHeight) <= 0.002f;
        bool bestPreservesHeight = Mathf.Abs(Vector3.Dot(best, up) - referenceHeight) <= 0.002f;
        if (candidatePreservesHeight != bestPreservesHeight)
        {
            return candidatePreservesHeight;
        }

        if (candidatePreservesHeight)
        {
            float candidatePlanar = Vector3.ProjectOnPlane(candidate - referenceRoot, up).sqrMagnitude;
            float bestPlanar = Vector3.ProjectOnPlane(best - referenceRoot, up).sqrMagnitude;
            if (candidatePlanar < bestPlanar - 0.000001f)
            {
                return true;
            }

            if (candidatePlanar > bestPlanar + 0.000001f)
            {
                return false;
            }
        }

        float candidateDistance = (candidate - referenceRoot).sqrMagnitude;
        float bestDistance = (best - referenceRoot).sqrMagnitude;
        if (candidateDistance < bestDistance - 0.000001f)
        {
            return true;
        }

        if (candidateDistance > bestDistance + 0.000001f)
        {
            return false;
        }

        return Vector3.Dot(candidate, up) > Vector3.Dot(best, up);
    }

    private bool PreviewBothLegsCanReach(bool firstIsLeft, Vector3 firstTarget, bool secondIsLeft, Vector3 secondTarget, Vector3 rootPosition)
    {
        return PreviewLegCanReach(firstIsLeft, firstTarget, rootPosition)
            && PreviewLegCanReach(secondIsLeft, secondTarget, rootPosition);
    }

    private bool PreviewLegCanReach(bool left, Vector3 target, Vector3 rootPosition)
    {
        EnsureLegKinematicModels();
        TitanLegKinematicModel model = left ? leftLegKinematicModel : rightLegKinematicModel;
        if (!model.Valid)
        {
            return false;
        }

        TitanLegIkAngles previewAngles = GetLegState(left).SolvedAngles;
        TitanLegSolveCache previewCache = default;
        Transform movementRoot = MovementRoot;
        TitanRootPose rootPose = new TitanRootPose
        {
            Position = rootPosition,
            Rotation = movementRoot.rotation,
            Scale = movementRoot.lossyScale,
        };
        TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
        TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
            model,
            rootPose,
            target,
            settings,
            TitanLegIkSolveMode.Tracking,
            previousReached: true,
            previousTargetClamped: false,
            ref previewCache,
            ref previewAngles);
        fullPreviewLegSolveCountThisFixedFrame++;
        if (!result.Reached)
        {
            result = TitanConstrainedLegIkSolver.Solve(
                model,
                rootPose,
                target,
                settings,
                TitanLegIkSolveMode.Recovery,
                previousReached: false,
                previousTargetClamped: result.TargetWasClamped,
                ref previewCache,
                ref previewAngles);
            fullPreviewLegSolveCountThisFixedFrame++;
        }

        float previewTolerance = Mathf.Max(settings.PositionTolerance * 2f, 0.003f);
        return !result.TargetWasClamped && result.DesiredPositionError <= previewTolerance;
    }

    private void AssertSupportAnchorInvariant()
    {
        bool supportIsLeft = supportFoot == TitanSupportFoot.Left;
        TitanLegControlState supportState = supportIsLeft ? leftLeg : rightLeg;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Assert(Vector3.Distance(supportState.DesiredGroundTarget, supportState.PlantAnchorWorld) <= 0.0001f, "Support DesiredGroundTarget drifted away from PlantAnchorWorld.", this);
        if (stepActive)
        {
            Debug.Assert(Vector3.Distance(supportState.PlantAnchorWorld, stepSupportPlantAnchor) <= 0.0001f, "Support PlantAnchorWorld changed during an active step.", this);
        }
#endif
    }

    private void SolveLegOnce(bool left, Vector3 desiredTarget)
    {
        using (TitanLegFinalIkMarker.Auto())
        {
            Transform hip = left ? leftHip : rightHip;
            Transform knee = left ? leftKnee : rightKnee;
            Transform foot = left ? leftFoot : rightFoot;
            if (hip == null || knee == null || foot == null)
            {
                return;
            }

            EnsureLegKinematicModels();
            TitanLegKinematicModel model = left ? leftLegKinematicModel : rightLegKinematicModel;
            if (!model.Valid)
            {
                return;
            }

            Quaternion hipBaseRotation = left ? leftHipBaseRotation : rightHipBaseRotation;
            Quaternion kneeBaseRotation = left ? leftKneeBaseRotation : rightKneeBaseRotation;
            TitanLegSolverSettings settings = left ? leftLegSolverSettings : rightLegSolverSettings;
            ref TitanLegControlState state = ref GetMutableLegState(left);
            bool activelyDrivenSwing = stepActive
                && activeSwingFoot == (left ? TitanSupportFoot.Left : TitanSupportFoot.Right)
                && !state.IsPlanted
                && (desiredTarget - state.LastSolveTarget).sqrMagnitude > HorizontalInputEpsilonSqr;
            if (activelyDrivenSwing)
            {
                settings.PositionTolerance = Mathf.Min(settings.PositionTolerance, Mathf.Max(0.000001f, activeSwingInputSolveTolerance));
            }

            TitanLegIkAngles angles = state.SolvedAngles;
            Transform movementRoot = MovementRoot;
            TitanRootPose rootPose = new TitanRootPose
            {
                Position = movementRoot.position,
                Rotation = movementRoot.rotation,
                Scale = movementRoot.lossyScale,
            };
            bool targetDiscontinuity = state.LastSolveReached
                && (desiredTarget - state.LastSolveTarget).sqrMagnitude > legTargetDiscontinuityThreshold * legTargetDiscontinuityThreshold;
            TitanLegIkSolveMode mode = state.PostureCanonicalizationPending
                ? TitanLegIkSolveMode.CanonicalizePosture
                : state.ForceRecovery || targetDiscontinuity || !state.LastSolveReached || state.TargetWasClamped
                    ? TitanLegIkSolveMode.Recovery
                    : TitanLegIkSolveMode.Tracking;
            TitanLegIkResult result = TitanConstrainedLegIkSolver.Solve(
                model,
                rootPose,
                desiredTarget,
                settings,
                mode,
                state.LastSolveReached,
                state.TargetWasClamped,
                ref state.SolveCache,
                ref angles);
            if (mode == TitanLegIkSolveMode.Tracking && !result.Reached)
            {
                result = TitanConstrainedLegIkSolver.Solve(
                    model,
                    rootPose,
                    desiredTarget,
                    settings,
                    TitanLegIkSolveMode.Recovery,
                    previousReached: false,
                    previousTargetClamped: result.TargetWasClamped,
                    ref state.SolveCache,
                    ref angles);
            }

            TitanConstrainedLegIkSolver.ApplyPose(hip, knee, hipBaseRotation, kneeBaseRotation, settings, result.Angles);
            ikSeedAttemptCountThisFixedFrame += TitanConstrainedLegIkSolver.LastSeedAttemptCount;
            trackingSeedAttemptCountThisFixedFrame += TitanConstrainedLegIkSolver.LastTrackingSeedAttemptCount;
            recoverySeedAttemptCountThisFixedFrame += TitanConstrainedLegIkSolver.LastRecoverySeedAttemptCount;
            canonicalSeedAttemptCountThisFixedFrame += TitanConstrainedLegIkSolver.LastCanonicalSeedAttemptCount;
            ikIterationCountThisFixedFrame += TitanConstrainedLegIkSolver.LastIterationCount;
            trackingIterationCountThisFixedFrame += TitanConstrainedLegIkSolver.LastTrackingIterationCount;
            recoveryIterationCountThisFixedFrame += TitanConstrainedLegIkSolver.LastRecoveryIterationCount;
            boneTransformWriteCountThisFixedFrame += TitanConstrainedLegIkSolver.LastBoneTransformWriteCount;

            state.SolvedAngles = result.Angles;
            state.ReachableFootTarget = result.ReachableTarget;
            state.PredictedFootPosition = result.ActualPosition;
            state.ActualFootPosition = foot.position;
            state.SolveError = result.PositionError;
            state.TargetWasClamped = result.TargetWasClamped;
            state.DesiredPositionError = Vector3.Distance(foot.position, desiredTarget);
            state.LastSolveReached = result.Reached
                && !result.TargetWasClamped
                && state.DesiredPositionError <= GetActualFootReachTolerance(settings);
            state.LastSolveTarget = desiredTarget;
            state.LastSolveRootPosition = rootPose.Position;
            state.LastSolveRootRotation = rootPose.Rotation;
            state.ForceRecovery = !state.LastSolveReached || result.TargetWasClamped;
            float fkMismatch = Vector3.Distance(result.ActualPosition, foot.position);
            if (left)
            {
                LeftMathFkTransformMismatch = fkMismatch;
            }
            else
            {
                RightMathFkTransformMismatch = fkMismatch;
            }
            if (mode == TitanLegIkSolveMode.CanonicalizePosture && state.LastSolveReached)
            {
                state.PostureCanonicalizationPending = false;
            }

            if (left)
            {
                leftSolveCountThisFixedFrame++;
            }
            else
            {
                rightSolveCountThisFixedFrame++;
            }

            WarnIfSolveErrorIsHigh(left, state.SolveError);
        }
    }

    private void ApplyFootTargetRotation(bool left)
    {
        Transform foot = left ? leftFoot : rightFoot;
        if (foot == null)
        {
            return;
        }

        foot.rotation = ComputeFootTargetRotation(left);
    }

    private void WarnIfSolveErrorIsHigh(bool left, float solveError)
    {
        if (solveError <= solveWarningThreshold || Time.unscaledTime < nextSolveWarningTime)
        {
            return;
        }

        nextSolveWarningTime = Time.unscaledTime + 1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[TitanRigRuntime] {(left ? "Left" : "Right")} leg IK solve error {solveError:0.000} exceeds threshold {solveWarningThreshold:0.000}.", this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawLegDebug)
        {
            return;
        }

        DrawLegGizmos(true, leftLeg, leftHip, leftKnee, leftFoot);
        DrawLegGizmos(false, rightLeg, rightHip, rightKnee, rightFoot);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(supportAnchorWorld, 0.08f);
    }

    private static void DrawLegGizmos(bool left, TitanLegControlState state, Transform hip, Transform knee, Transform foot)
    {
        Gizmos.color = left ? Color.cyan : Color.magenta;
        Gizmos.DrawWireSphere(state.DesiredGroundTarget, 0.06f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(state.ReachableFootTarget, 0.05f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(state.ActualFootPosition, 0.05f);

        if (hip == null || knee == null || foot == null)
        {
            return;
        }

        Gizmos.color = Color.white;
        Gizmos.DrawLine(hip.position, knee.position);
        Gizmos.DrawLine(knee.position, foot.position);
    }

    private void EnsureMovementRigidbodyCached()
    {
        if (movementRigidbody != null)
        {
            return;
        }

        Transform movementRoot = MovementRoot;
        if (movementRoot == null)
        {
            return;
        }

        movementRigidbody = movementRoot.GetComponent<Rigidbody>();
        if (movementRigidbody == null)
        {
            movementRigidbody = movementRoot.GetComponentInParent<Rigidbody>();
        }
    }

    private void ConfigureMovementRigidbodyForTitanControl()
    {
        ApplyCurrentPhysicsMode();
    }

    private void ApplyCurrentPhysicsMode()
    {
        EnsureMovementRigidbodyCached();
        if (movementRigidbody == null)
        {
            return;
        }

        bool localGroundedMotor = IsStableGroundedMotorState();
        bool shouldBeKinematic = remotePhysicsOverride || localGroundedMotor;
        if (shouldBeKinematic && !movementRigidbody.isKinematic)
        {
            ClearMovementRigidbodyVelocityIfDynamic();
        }

        movementRigidbody.isKinematic = shouldBeKinematic;
        movementRigidbody.useGravity = !shouldBeKinematic;

        if (!remotePhysicsOverride && groundingState != TitanLegGroundingState.Grounded)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Assert(!movementRigidbody.isKinematic, "Airborne and Landing Titans must remain dynamic.");
            Debug.Assert(movementRigidbody.useGravity, "Airborne and Landing Titans must keep gravity enabled.");
#endif
        }
    }

    private void ClearMovementRigidbodyVelocityIfDynamic()
    {
        if (movementRigidbody == null || movementRigidbody.isKinematic)
        {
            return;
        }

        movementRigidbody.linearVelocity = Vector3.zero;
        movementRigidbody.angularVelocity = Vector3.zero;
    }

    private bool IsGroundedMotorActive()
    {
        return IsStableGroundedMotorState();
    }

    public bool TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot)
    {
        snapshot = default;
        if (!EnsureReady())
        {
            return false;
        }

        Transform movementRoot = MovementRoot;
        snapshot.RootPosition = movementRoot.position;
        snapshot.RootRotation = movementRoot.rotation;

        snapshot.HasLeftShoulder = leftShoulder != null;
        if (snapshot.HasLeftShoulder)
        {
            snapshot.LeftShoulderRotation = leftShoulder.localRotation;
        }

        snapshot.HasLeftElbow = leftElbow != null;
        if (snapshot.HasLeftElbow)
        {
            snapshot.LeftElbowRotation = leftElbow.localRotation;
        }

        snapshot.HasRightShoulder = rightShoulder != null;
        if (snapshot.HasRightShoulder)
        {
            snapshot.RightShoulderRotation = rightShoulder.localRotation;
        }

        snapshot.HasRightElbow = rightElbow != null;
        if (snapshot.HasRightElbow)
        {
            snapshot.RightElbowRotation = rightElbow.localRotation;
        }

        snapshot.HasLeftHip = leftHip != null;
        if (snapshot.HasLeftHip)
        {
            snapshot.LeftHipRotation = leftHip.localRotation;
        }

        snapshot.HasLeftKnee = leftKnee != null;
        if (snapshot.HasLeftKnee)
        {
            snapshot.LeftKneeRotation = leftKnee.localRotation;
        }

        snapshot.HasLeftFoot = leftFoot != null;
        if (snapshot.HasLeftFoot)
        {
            snapshot.LeftFootPosition = leftFoot.localPosition;
            snapshot.LeftFootRotation = leftFoot.localRotation;
        }

        snapshot.HasRightHip = rightHip != null;
        if (snapshot.HasRightHip)
        {
            snapshot.RightHipRotation = rightHip.localRotation;
        }

        snapshot.HasRightKnee = rightKnee != null;
        if (snapshot.HasRightKnee)
        {
            snapshot.RightKneeRotation = rightKnee.localRotation;
        }

        snapshot.HasRightFoot = rightFoot != null;
        if (snapshot.HasRightFoot)
        {
            snapshot.RightFootPosition = rightFoot.localPosition;
            snapshot.RightFootRotation = rightFoot.localRotation;
        }

        snapshot.HasSpine = spine != null;
        if (snapshot.HasSpine)
        {
            snapshot.SpineRotation = spine.localRotation;
        }

        return true;
    }

    public void ApplyPoseSnapshot(in TitanRigPoseSnapshot snapshot)
    {
        if (!EnsureReady())
        {
            return;
        }

        ApplyMovementRootPose(snapshot.RootPosition, snapshot.RootRotation, zeroVelocities: true);

        if (snapshot.HasLeftShoulder && leftShoulder != null)
        {
            leftShoulder.localRotation = snapshot.LeftShoulderRotation;
        }

        if (snapshot.HasLeftElbow && leftElbow != null)
        {
            leftElbow.localRotation = snapshot.LeftElbowRotation;
        }

        if (snapshot.HasRightShoulder && rightShoulder != null)
        {
            rightShoulder.localRotation = snapshot.RightShoulderRotation;
        }

        if (snapshot.HasRightElbow && rightElbow != null)
        {
            rightElbow.localRotation = snapshot.RightElbowRotation;
        }

        if (snapshot.HasLeftHip && leftHip != null)
        {
            leftHip.localRotation = snapshot.LeftHipRotation;
        }

        if (snapshot.HasLeftKnee && leftKnee != null)
        {
            leftKnee.localRotation = snapshot.LeftKneeRotation;
        }

        if (snapshot.HasLeftFoot && leftFoot != null)
        {
            leftFoot.localPosition = snapshot.LeftFootPosition;
            leftFoot.localRotation = snapshot.LeftFootRotation;
        }

        if (snapshot.HasRightHip && rightHip != null)
        {
            rightHip.localRotation = snapshot.RightHipRotation;
        }

        if (snapshot.HasRightKnee && rightKnee != null)
        {
            rightKnee.localRotation = snapshot.RightKneeRotation;
        }

        if (snapshot.HasRightFoot && rightFoot != null)
        {
            rightFoot.localPosition = snapshot.RightFootPosition;
            rightFoot.localRotation = snapshot.RightFootRotation;
        }

        if (snapshot.HasSpine && spine != null)
        {
            spine.localRotation = snapshot.SpineRotation;
        }
    }

    private void ResolveAndCacheIfNeeded(bool forceCache)
    {
        int before = ComputeBoneSignature();
        ResolveBones();
        int after = ComputeBoneSignature();

        if (forceCache || !basePoseInitialized || before != after)
        {
            CacheBaseRotations();
            basePoseInitialized = true;
        }
    }

    private void ResolveBones()
    {
        mechaRoot ??= transform;
        leftFootAttachment ??= GetComponent<TitanLeftFootAttachmentController>();
        rightFootAttachment ??= GetComponent<TitanRightFootAttachmentController>();

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        Transform searchRoot = animator != null ? animator.transform : transform;

        if (animator != null && animator.isHuman)
        {
            leftShoulder ??= animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftElbow ??= animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightShoulder ??= animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightElbow ??= animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHip ??= animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftKnee ??= animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightHip ??= animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightKnee ??= animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);
            spine ??= animator.GetBoneTransform(HumanBodyBones.Chest);
            spine ??= animator.GetBoneTransform(HumanBodyBones.UpperChest);
            spine ??= animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        leftShoulder ??= FindChildByNames(searchRoot,
            "Character1_LeftShoulder", "Character1_LeftArm", "LeftShoulder", "LeftArm",
            "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorigLeftShoulder", "mixamorigLeftArm",
            "J_Bip_L_UpperArm", "LeftUpperArm", "UpperArm_L", "L_UpperArm", "Bip001 L Clavicle", "Bip001 L UpperArm");

        leftElbow ??= FindChildByNames(searchRoot,
            "Character1_LeftForeArm", "Character1_LeftLowerArm", "LeftForeArm", "LeftLowerArm", "L_Arm_Lower",
            "mixamorig:LeftForeArm", "mixamorigLeftForeArm", "J_Bip_L_LowerArm", "LeftLowerArm",
            "LowerArm_L", "L_ForeArm", "Bip001 L Forearm");

        rightShoulder ??= FindChildByNames(searchRoot,
            "Character1_RightShoulder", "Character1_RightArm", "RightShoulder", "RightArm",
            "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorigRightShoulder", "mixamorigRightArm",
            "J_Bip_R_UpperArm", "RightUpperArm", "UpperArm_R", "R_UpperArm", "Bip001 R Clavicle", "Bip001 R UpperArm");

        rightElbow ??= FindChildByNames(searchRoot,
            "Character1_RightForeArm", "Character1_RightLowerArm", "RightForeArm", "RightLowerArm", "R_Arm_Lower",
            "mixamorig:RightForeArm", "mixamorigRightForeArm", "J_Bip_R_LowerArm", "RightLowerArm",
            "LowerArm_R", "R_ForeArm", "Bip001 R Forearm");

        leftHip ??= FindChildByNames(searchRoot,
            "Character1_LeftUpLeg", "LeftUpLeg", "mixamorig:LeftUpLeg", "mixamorigLeftUpLeg",
            "J_Bip_L_UpperLeg", "LeftUpperLeg", "UpperLeg_L", "L_Thigh", "Bip001 L Thigh");

        leftKnee ??= FindChildByNames(searchRoot,
            "Character1_LeftLeg", "LeftLeg", "mixamorig:LeftLeg", "mixamorigLeftLeg",
            "J_Bip_L_LowerLeg", "LeftLowerLeg", "LowerLeg_L", "L_Calf", "Bip001 L Calf");

        leftFoot ??= FindChildByNames(searchRoot,
            "LeftFoot", "mixamorig:LeftFoot",
            "mixamorigLeftFoot", "Bip001 L Foot", "L_Foot");

        rightHip ??= FindChildByNames(searchRoot,
            "Character1_RightUpLeg", "RightUpLeg", "mixamorig:RightUpLeg", "mixamorigRightUpLeg",
            "J_Bip_R_UpperLeg", "RightUpperLeg", "UpperLeg_R", "R_Thigh", "Bip001 R Thigh");

        rightKnee ??= FindChildByNames(searchRoot,
            "Character1_RightLeg", "RightLeg", "mixamorig:RightLeg", "mixamorigRightLeg",
            "J_Bip_R_LowerLeg", "RightLowerLeg", "LowerLeg_R", "R_Calf", "Bip001 R Calf");

        rightFoot ??= FindChildByNames(searchRoot,
            "RightFoot", "mixamorig:RightFoot",
            "mixamorigRightFoot", "Bip001 R Foot", "R_Foot");

        spine ??= FindChildByNames(searchRoot,
            "Character1_Chest", "Character1_UpperChest", "Character1_Spine",
            "UpperChest", "Chest", "Spine",
            "mixamorig:UpperChest", "mixamorig:Chest", "mixamorig:Spine",
            "mixamorigUpperChest", "mixamorigChest", "mixamorigSpine",
            "J_Bip_C_Chest", "J_Bip_C_Spine", "Bip001 Spine1", "Bip001 Spine");

        drill ??= FindChildByNames(searchRoot, "Drill_bone");
        claw ??= FindChildByNames(searchRoot, "R_Clamp_Center");

        leftShoulder ??= FindByKeywords(searchRoot, true, "shoulder", "upperarm", "arm", "clavicle");
        leftElbow ??= FindByKeywords(leftShoulder != null ? leftShoulder : searchRoot, true, "lowerarm", "forearm", "elbow");
        rightShoulder ??= FindByKeywords(searchRoot, false, "shoulder", "upperarm", "arm", "clavicle");
        rightElbow ??= FindByKeywords(rightShoulder != null ? rightShoulder : searchRoot, false, "lowerarm", "forearm", "elbow");

        leftHip ??= FindByKeywords(searchRoot, true, "upleg", "upperleg", "thigh", "leg");
        leftKnee ??= FindByKeywords(leftHip != null ? leftHip : searchRoot, true, "lowerleg", "calf", "shin", "leg");
        rightHip ??= FindByKeywords(searchRoot, false, "upleg", "upperleg", "thigh", "leg");
        rightKnee ??= FindByKeywords(rightHip != null ? rightHip : searchRoot, false, "lowerleg", "calf", "shin", "leg");
        spine ??= FindByCenterKeywords(searchRoot, "upperchest", "chest", "spine", "torso", "waist");

        // Last resort: infer bones from skinned mesh bone transforms using spatial heuristics.
        if (!HasAnyDrivenBone())
        {
            ResolveFromSkinnedBoneHeuristics(searchRoot);
        }
    }

    private void ResolveFromSkinnedBoneHeuristics(Transform searchRoot)
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        // Collect candidates.
        List<Transform> candidates = new List<Transform>(256);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer r = renderers[i];
            if (r == null)
                continue;

            if (mechaRoot == null && r.rootBone != null)
                mechaRoot = r.rootBone;

            Transform[] bones = r.bones;
            if (bones == null)
                continue;

            for (int b = 0; b < bones.Length; b++)
            {
                Transform t = bones[b];
                if (t != null)
                    candidates.Add(t);
            }
        }

        if (candidates.Count == 0)
            return;

        Transform space = searchRoot != null ? searchRoot : transform;

        // Compute local bounds in candidate space.
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 p = space.InverseTransformPoint(candidates[i].position);
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        float upperY = Mathf.Lerp(minY, maxY, 0.72f);
        float midY = Mathf.Lerp(minY, maxY, 0.50f);
        float lowY = Mathf.Lerp(minY, maxY, 0.30f);

        // Shoulders: far left/right at upper Torso.
        leftShoulder ??= PickExtremeX(space, candidates, upperY, true);
        rightShoulder ??= PickExtremeX(space, candidates, upperY, false);

        // Hips: far left/right at lower Torso.
        leftHip ??= PickExtremeX(space, candidates, lowY, true, below: true);
        rightHip ??= PickExtremeX(space, candidates, lowY, false, below: true);

        // Spine: near center x at mid-high.
        spine ??= PickCenterXHighest(space, candidates, midY);

        // Elbows: between shoulder and mid, closest to shoulder.
        leftElbow ??= PickClosestWithinBand(space, candidates, leftShoulder, midY, upperY, preferLeft: true);
        rightElbow ??= PickClosestWithinBand(space, candidates, rightShoulder, midY, upperY, preferLeft: false);

        // Knees: between low and mid, closest to hip.
        leftKnee ??= PickClosestWithinBand(space, candidates, leftHip, lowY, midY, preferLeft: true);
        rightKnee ??= PickClosestWithinBand(space, candidates, rightHip, lowY, midY, preferLeft: false);
    }

    private static Transform PickExtremeX(Transform space, List<Transform> candidates, float yThreshold, bool left, bool below = false)
    {
        Transform best = null;
        float bestX = left ? float.PositiveInfinity : float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform t = candidates[i];
            Vector3 p = space.InverseTransformPoint(t.position);
            bool yOk = below ? p.y <= yThreshold : p.y >= yThreshold;
            if (!yOk)
                continue;

            if (left)
            {
                if (p.x < bestX)
                {
                    bestX = p.x;
                    best = t;
                }
            }
            else
            {
                if (p.x > bestX)
                {
                    bestX = p.x;
                    best = t;
                }
            }
        }

        return best;
    }

    private static Transform PickCenterXHighest(Transform space, List<Transform> candidates, float minY)
    {
        Transform best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform t = candidates[i];
            Vector3 p = space.InverseTransformPoint(t.position);
            if (p.y < minY)
                continue;

            float score = (-Mathf.Abs(p.x) * 2f) + p.y;
            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return best;
    }

    private static Transform PickClosestWithinBand(Transform space, List<Transform> candidates, Transform anchor, float minY, float maxY, bool preferLeft)
    {
        if (anchor == null)
            return null;

        Vector3 anchorP = space.InverseTransformPoint(anchor.position);

        Transform best = null;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform t = candidates[i];
            if (t == anchor)
                continue;

            Vector3 p = space.InverseTransformPoint(t.position);
            if (p.y < minY || p.y > maxY)
                continue;

            if (preferLeft && p.x > anchorP.x)
                continue;
            if (!preferLeft && p.x < anchorP.x)
                continue;

            float dist = Vector3.SqrMagnitude(p - anchorP);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }

        return best;
    }

    private bool HasAnyDrivenBone()
    {
        return
            leftShoulder != null ||
            leftElbow != null ||
            rightShoulder != null ||
            rightElbow != null ||
            leftHip != null ||
            leftKnee != null ||
            rightHip != null ||
            rightKnee != null ||
            spine != null;
    }

    private static Transform FindByCenterKeywords(Transform root, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            string lower = current.name.ToLowerInvariant();

            if (IsRejectedBoneName(lower))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                if (lower.Contains(keywords[k]))
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static Transform FindByKeywords(Transform root, bool isLeft, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];
            string lower = current.name.ToLowerInvariant();

            if (IsRejectedBoneName(lower))
            {
                continue;
            }

            if (!IsExpectedSide(lower, isLeft))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                if (lower.Contains(keywords[k]))
                {
                    return current;
                }
            }
        }

        return null;
    }

    private static bool IsExpectedSide(string lower, bool isLeft)
    {
        bool hasLeft =
            lower.Contains("left") ||
            lower.Contains("_l") ||
            lower.Contains("l_") ||
            lower.Contains(".l") ||
            lower.Contains(" l ");

        bool hasRight =
            lower.Contains("right") ||
            lower.Contains("_r") ||
            lower.Contains("r_") ||
            lower.Contains(".r") ||
            lower.Contains(" r ");

        if (isLeft)
        {
            return hasLeft && !hasRight;
        }

        return hasRight && !hasLeft;
    }

    private static bool IsRejectedBoneName(string lowerName)
    {
        return
            lowerName.Contains("nub") ||
            lowerName.Contains("finger") ||
            lowerName.Contains("thumb") ||
            lowerName.Contains("index") ||
            lowerName.Contains("middle") ||
            lowerName.Contains("ring") ||
            lowerName.Contains("pinky") ||
            lowerName.Contains("toe") ||
            lowerName.Contains("head") ||
            lowerName.Contains("neck") ||
            lowerName.Contains("jaw") ||
            lowerName.Contains("eye");
    }

    private static Transform FindChildByNames(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeepChildExact(root, names[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDeepChildExact(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform result = FindDeepChildExact(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private int ComputeBoneSignature()
    {
        unchecked
        {
            int signature = 17;
            signature = (signature * 23) + GetId(leftShoulder);
            signature = (signature * 23) + GetId(leftElbow);
            signature = (signature * 23) + GetId(rightShoulder);
            signature = (signature * 23) + GetId(rightElbow);
            signature = (signature * 23) + GetId(leftHip);
            signature = (signature * 23) + GetId(leftKnee);
            signature = (signature * 23) + GetId(leftFoot);
            signature = (signature * 23) + GetId(rightHip);
            signature = (signature * 23) + GetId(rightKnee);
            signature = (signature * 23) + GetId(rightFoot);
            signature = (signature * 23) + GetId(spine);
            signature = (signature * 23) + GetId(drill);
            signature = (signature * 23) + GetId(claw);
            return signature;
        }
    }

    private static int GetId(Transform value)
    {
        return value != null ? value.GetEntityId().GetHashCode() : 0;
    }

    private static string NameOrNone(Transform value)
    {
        return value != null ? value.name : "None";
    }

    private void CacheBaseRotations()
    {
        transformBaseRotation = transform.rotation;
        movementRootBaseRotation = MovementRoot.rotation;

        if (leftShoulder != null)
        {
            leftShoulderBaseRotation = leftShoulder.localRotation;
        }

        if (leftElbow != null)
        {
            leftElbowBaseRotation = leftElbow.localRotation;
        }

        if (rightShoulder != null)
        {
            rightShoulderBaseRotation = rightShoulder.localRotation;
        }

        if (rightElbow != null)
        {
            rightElbowBaseRotation = rightElbow.localRotation;
        }

        if (leftHip != null)
        {
            leftHipBaseRotation = leftHip.localRotation;
        }

        if (leftKnee != null)
        {
            leftKneeBaseRotation = leftKnee.localRotation;
        }

        if (leftFoot != null)
        {
            leftFootBaseRotation = leftFoot.localRotation;
            leftFootSoleDownLocal = ComputeFootSoleDownLocal(leftFoot, leftFootAttachment);
            leftFootGroundRotationOffset = Quaternion.Inverse(ComputeFootGroundFrame()) * leftFoot.rotation;
        }

        if (rightHip != null)
        {
            rightHipBaseRotation = rightHip.localRotation;
        }

        if (rightKnee != null)
        {
            rightKneeBaseRotation = rightKnee.localRotation;
        }

        if (rightFoot != null)
        {
            rightFootBaseRotation = rightFoot.localRotation;
            rightFootSoleDownLocal = ComputeFootSoleDownLocal(rightFoot, rightFootAttachment);
            rightFootGroundRotationOffset = Quaternion.Inverse(ComputeFootGroundFrame()) * rightFoot.rotation;
        }

        if (spine != null)
        {
            spineBaseRotation = spine.localRotation;
        }
    }

    private static Vector3 ComputeFootSoleDownLocal(Transform foot, FootAttachmentController attachment)
    {
        Transform bottomProbe = attachment != null ? attachment.BottomProbe : null;
        if (bottomProbe != null && bottomProbe != foot)
        {
            Vector3 footToBottom = bottomProbe.position - foot.position;
            if (footToBottom.sqrMagnitude > 0.0001f)
            {
                return Quaternion.Inverse(foot.rotation) * footToBottom.normalized;
            }
        }

        return Quaternion.Inverse(foot.rotation) * -TitanGroundFrame.Up;
    }
}
