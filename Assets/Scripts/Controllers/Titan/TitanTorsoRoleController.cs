using UnityEngine;
using Unity.VisualScripting;

public class TitanTorsoRoleController : TitanBaseController
{
    private const float DrillGaugeCost = 100f;
    private const float ShieldGaugeCost = 100f;
    private const float DrillActiveDurationSeconds = 1f;
    private const float ShieldActiveDurationSeconds = 1f;
    private const float ClawLaunchGaugeCost = 100f;
    private const float DrillHitRadius = 0.3f;
    private const float DrillHitIntervalSeconds = 0.25f;

    [Header("Torso Input")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float strafeSpeed = 1.75f;
    [SerializeField] private float turnSpeed = 75f;
    [SerializeField] private float torsoSmoothing = 8f;

    [Header("Waist Rotation")]
    [SerializeField] private float waistTurnSpeed = 120f;
    [SerializeField] private Vector2 waistYawLimit = new Vector2(-180f, 180f);

    [Header("Physics")]
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private float groundProbeHeight = 0.35f;
    [SerializeField] private float groundProbeDistance = 0.7f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float movementAcceleration = 8f;
    [SerializeField] private float maxPlanarSpeed = 4.5f;
    [SerializeField] private float turnAcceleration = 85f;

    [Header("Balance")]
    [SerializeField] private float balanceTorque = 160f;
    [SerializeField] private float oneLegInstabilityMultiplier = 2.2f;
    [SerializeField] private float uprightRecoverySpeed = 5f;
    [SerializeField] private float gravityRollAcceleration = 120f;
    [SerializeField] private float rollDamping = 6f;
    [SerializeField] private float maxRollAngle = 65f;
    [SerializeField] private float maxBalanceTorque = 240f;
    [SerializeField] private float singleFootUnstableAngle = 28f;
    [SerializeField] private float unstableAngularDamping = 10f;
    [SerializeField] private float maxUnstableAngularSpeed = 1.0f;
    [SerializeField] private float fallenAngularDamping = 8f;
    [SerializeField] private float maxFallenAngularSpeed = 1.5f;
    [SerializeField] private float footSupportRadius = 0.28f;
    [SerializeField] private float supportAreaPadding = 0.08f;

    [Header("Body Part Mass")]
    [SerializeField] private float torsoAndHeadMass = 2f;
    [SerializeField] private float upperArmMass = 1f;
    [SerializeField] private float lowerArmMass = 2f;
    [SerializeField] private float thighMass = 1f;
    [SerializeField] private float calfMass = 1f;
    [SerializeField] private float footMass = 1f;

    [Header("Aerodynamic Drag Rotation")]
    [SerializeField] private Transform centerOfPressure;
    [SerializeField] private Vector3 centerOfPressureLocalOffset = new Vector3(0f, 1.1f, -0.6f);
    [SerializeField] private float dragTorqueScale = 0.03f;
    [SerializeField] private float maxDragTorque = 160f;
    [SerializeField] private float minDragSpeed = 0.75f;

    [Header("Foot Anchors")]
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;

    private Vector3 planarVelocity;
    private Rigidbody movementRigidbody;
    private float verticalVelocity;
    private float forwardInput;
    private float strafeInput;
    private float turnInput;
    private bool inputEnabled = true;
    private bool anchorPhysicsOverride;
    private TitanController titanController;
    private TitanStat titanStat;
    private float nextDrillHitTime;
    private float drillActiveTimeRemaining;
    private float shieldActiveTimeRemaining;
    private uint lastHandledDrillPressCounter;
    private uint lastHandledShieldPressCounter;
    private uint lastHandledClawPressCounter;

    public override Define.TitanRole Role => Define.TitanRole.Torso;

    protected override void Awake()
    {
        base.Awake();
        titanController = gameObject.GetOrAddComponent<TitanController>();
        titanStat = gameObject.GetOrAddComponent<TitanStat>();
        Transform movementRoot = Managers.TitanRig.MovementRoot;
        movementRigidbody = movementRoot.GetComponent<Rigidbody>();
        if (movementRigidbody == null)
        {
            movementRigidbody = movementRoot.GetComponentInParent<Rigidbody>();
        }

        ResolveFeet(movementRoot);
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!inputEnabled)
        {
            forwardInput = 0f;
            strafeInput = 0f;
            turnInput = 0f;
        }
    }

    public void SetAnchorPhysicsOverride(bool enabled)
    {
        anchorPhysicsOverride = enabled;
        if (!anchorPhysicsOverride)
        {
            return;
        }

        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        forwardInput = 0f;
        strafeInput = 0f;
        turnInput = 0f;

        if (movementRigidbody != null)
        {
            movementRigidbody.linearVelocity = Vector3.zero;
            movementRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        if (!inputEnabled || !Managers.TitanRig.EnsureReady())
        {
            return;
        }

        forwardInput = input.TorsoForward;
        strafeInput = input.TorsoStrafe;
        turnInput = input.TorsoTurn;
        UpdateWaistRotation(input.TorsoWaist, deltaTime);
        UpdateSpecialAbilities(input, deltaTime);
    }

    private void UpdateSpecialAbilities(in TitanAggregatedInput input, float deltaTime)
    {
        titanStat.RecoverGauge(deltaTime);

        if (TryConsumePress(input.TorsoDrillPressCounter, ref lastHandledDrillPressCounter) && titanStat.TrySpendGauge(DrillGaugeCost))
            drillActiveTimeRemaining = DrillActiveDurationSeconds;

        if (TryConsumePress(input.TorsoShieldPressCounter, ref lastHandledShieldPressCounter) && titanStat.TrySpendGauge(ShieldGaugeCost))
            shieldActiveTimeRemaining = ShieldActiveDurationSeconds;

        if (drillActiveTimeRemaining > 0f)
            drillActiveTimeRemaining = Mathf.Max(0f, drillActiveTimeRemaining - deltaTime);

        if (shieldActiveTimeRemaining > 0f)
            shieldActiveTimeRemaining = Mathf.Max(0f, shieldActiveTimeRemaining - deltaTime);

        bool drillActive = drillActiveTimeRemaining > 0f;
        bool shieldActive = shieldActiveTimeRemaining > 0f;

        titanController.LeftDrillActive = drillActive;
        titanController.Guard = shieldActive;

        if (drillActive)
            TryApplyDrillAttack();

        if (TryConsumePress(input.TorsoClawPressCounter, ref lastHandledClawPressCounter) && titanController.CanLaunchRightClaw && titanStat.TrySpendGauge(ClawLaunchGaugeCost))
            titanController.NotifyRightClawLaunched();
    }

    private static bool TryConsumePress(uint pressCounter, ref uint lastHandledPressCounter)
    {
        if (pressCounter == 0 || pressCounter == lastHandledPressCounter)
            return false;

        lastHandledPressCounter = pressCounter;
        return true;
    }

    private void TryApplyDrillAttack()
    {
        if (Time.time < nextDrillHitTime)
            return;

        Vector3 drillPosition = ResolveDrillPosition();
        BossController[] bosses = Object.FindObjectsByType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController boss = bosses[i];
            if (boss == null || !boss.IsWithinHitRadius(drillPosition, DrillHitRadius))
                continue;

            boss.ReceiveAttack(titanStat);
            nextDrillHitTime = Time.time + DrillHitIntervalSeconds;
            return;
        }
    }

    private static Vector3 ResolveDrillPosition()
    {
        Transform anchor = Managers.TitanRig.LeftElbow;
        if (anchor != null)
            return anchor.position;

        Transform movementRoot = Managers.TitanRig.MovementRoot;
        return movementRoot.position + movementRoot.forward * 0.5f;
    }

    public void TickPhysics(float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            return;
        }

        Transform movementRoot = Managers.TitanRig.MovementRoot;
        if (movementRigidbody == null)
        {
            movementRigidbody = movementRoot.GetComponent<Rigidbody>();
            if (movementRigidbody == null)
            {
                movementRigidbody = movementRoot.GetComponentInParent<Rigidbody>();
            }
        }

        ResolveFeet(movementRoot);

        if (anchorPhysicsOverride)
        {
            if (movementRigidbody != null)
            {
                movementRigidbody.linearVelocity = Vector3.zero;
                movementRigidbody.angularVelocity = Vector3.zero;
            }

            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(movementRoot.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(movementRoot.right, Vector3.up).normalized;
        Vector3 desiredPlanarVelocity =
            (flatForward * (forwardInput * moveSpeed)) +
            (flatRight * (strafeInput * strafeSpeed));

        planarVelocity = Vector3.Lerp(planarVelocity, desiredPlanarVelocity, 1f - Mathf.Exp(-torsoSmoothing * deltaTime));

        bool leftGrounded = IsFootGrounded(leftFoot);
        bool rightGrounded = IsFootGrounded(rightFoot);
        bool grounded = leftGrounded || rightGrounded;

        if (!grounded)
        {
            grounded = IsTorsoGrounded(movementRoot);
        }

        if (movementRigidbody != null)
        {
            ApplyRigidbodyPhysics(movementRoot, leftGrounded, rightGrounded, grounded, deltaTime);
            return;
        }

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * gravityScale * deltaTime;
        }

        float yawStep = turnInput * turnSpeed * deltaTime;
        float verticalStep = verticalVelocity * deltaTime;
        Vector3 movement = (planarVelocity * deltaTime) + (Vector3.up * verticalStep);
        movementRoot.position += movement;
        movementRoot.rotation = Quaternion.AngleAxis(yawStep, Vector3.up) * movementRoot.rotation;
    }

    private void ApplyRigidbodyPhysics(Transform movementRoot, bool leftGrounded, bool rightGrounded, bool grounded, float deltaTime)
    {
        if (movementRigidbody == null)
        {
            return;
        }

        Vector3 velocity = movementRigidbody.linearVelocity;
        Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 planarDelta = planarVelocity - currentPlanarVelocity;
        Vector3 planarAcceleration = planarDelta * movementAcceleration;
        movementRigidbody.AddForce(planarAcceleration, ForceMode.Acceleration);

        Vector3 clampedPlanarVelocity = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(movementRigidbody.linearVelocity, Vector3.up), maxPlanarSpeed);
        movementRigidbody.linearVelocity = clampedPlanarVelocity + (Vector3.up * movementRigidbody.linearVelocity.y);

        float yawAcceleration = turnInput * turnAcceleration;
        movementRigidbody.AddTorque(Vector3.up * yawAcceleration, ForceMode.Acceleration);

        if (gravityScale > 1f)
        {
            movementRigidbody.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
        }

        bool fallen = IsFallen(movementRoot);
        bool singleFootUnstable = IsSingleFootUnstable(movementRoot, leftGrounded, rightGrounded);
        ApplyBalanceTorque(movementRoot, leftGrounded, rightGrounded, grounded, fallen, singleFootUnstable, deltaTime);

        if (!fallen && !singleFootUnstable)
            ApplyDragDirectionTorque(movementRoot);
    }

    private void ApplyBalanceTorque(Transform movementRoot, bool leftGrounded, bool rightGrounded, bool grounded, bool fallen, bool singleFootUnstable, float deltaTime)
    {
        if (movementRigidbody == null)
        {
            return;
        }

        if (fallen)
        {
            DampenFallenSpin(deltaTime);
            return;
        }

        if (singleFootUnstable)
        {
            DampenUnstableSpin(deltaTime);
            return;
        }

        Vector3 centerOfMass = CalculateBodyPartCenterOfMass(movementRoot);
        SupportInfo support = CalculateSupportInfo(movementRoot, centerOfMass, leftGrounded, rightGrounded);

        Vector3 planarOffset = Vector3.ProjectOnPlane(centerOfMass - support.Center, Vector3.up);
        float oneLegFactor = (leftGrounded ^ rightGrounded) ? oneLegInstabilityMultiplier : 1f;
        float comRightBias = Vector3.Dot(planarOffset, movementRoot.right);
        float signedFall = 0f;

        if (leftGrounded && !rightGrounded)
        {
            signedFall = -1f;
        }
        else if (rightGrounded && !leftGrounded)
        {
            signedFall = 1f;
        }
        else if (!leftGrounded && !rightGrounded && Mathf.Abs(comRightBias) > 0.001f)
        {
            signedFall = Mathf.Sign(comRightBias);
        }

        float gravityInfluence = gravityRollAcceleration * oneLegFactor;
        float comInfluence = Mathf.Clamp(comRightBias, -1f, 1f) * balanceTorque;
        float rollRate = Vector3.Dot(movementRigidbody.angularVelocity, movementRoot.forward);
        float dampingInfluence = -rollRate * rollDamping;
        float rollAngle = ComputeSignedRollAngle(movementRoot);
        float recoverInfluence = 0f;

        if (leftGrounded && rightGrounded)
        {
            recoverInfluence = -rollAngle * uprightRecoverySpeed;
        }

        if (Mathf.Abs(rollAngle) > maxRollAngle)
        {
            float overflow = Mathf.Abs(rollAngle) - maxRollAngle;
            recoverInfluence += -Mathf.Sign(rollAngle) * overflow * uprightRecoverySpeed;
        }

        float angularAcceleration = (signedFall * gravityInfluence) + comInfluence + dampingInfluence + recoverInfluence;

        if (support.IsOutside)
        {
            Vector3 fallDirection = Vector3.ProjectOnPlane(centerOfMass - support.ClosestPoint, Vector3.up).normalized;
            if (fallDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 fallAxis = Vector3.Cross(Vector3.up, fallDirection).normalized;
                float fallAcceleration = Mathf.Clamp(support.OutsideDistance * gravityRollAcceleration * oneLegFactor, 0f, maxBalanceTorque);
                movementRigidbody.AddTorque(fallAxis * fallAcceleration, ForceMode.Acceleration);
            }
        }

        if (!grounded)
        {
            angularAcceleration *= 0.6f;
        }

        angularAcceleration = Mathf.Clamp(angularAcceleration, -maxBalanceTorque, maxBalanceTorque);
        movementRigidbody.AddTorque(movementRoot.forward * angularAcceleration, ForceMode.Acceleration);
    }

    private bool IsFallen(Transform movementRoot)
    {
        return Vector3.Angle(movementRoot.up, Vector3.up) >= maxRollAngle;
    }

    private bool IsSingleFootUnstable(Transform movementRoot, bool leftGrounded, bool rightGrounded)
    {
        if (!(leftGrounded ^ rightGrounded))
            return false;

        return Vector3.Angle(movementRoot.up, Vector3.up) >= singleFootUnstableAngle;
    }

    private void DampenFallenSpin(float deltaTime)
    {
        DampenAngularVelocity(deltaTime, fallenAngularDamping, maxFallenAngularSpeed);
    }

    private void DampenUnstableSpin(float deltaTime)
    {
        DampenAngularVelocity(deltaTime, unstableAngularDamping, maxUnstableAngularSpeed);
    }

    private void DampenAngularVelocity(float deltaTime, float damping, float maxAngularSpeed)
    {
        Vector3 angularVelocity = movementRigidbody.angularVelocity;
        float dampingBlend = 1f - Mathf.Exp(-Mathf.Max(0f, damping) * Mathf.Max(0f, deltaTime));
        angularVelocity = Vector3.Lerp(angularVelocity, Vector3.zero, dampingBlend);
        movementRigidbody.angularVelocity = Vector3.ClampMagnitude(angularVelocity, Mathf.Max(0f, maxAngularSpeed));
    }

    private Vector3 CalculateBodyPartCenterOfMass(Transform movementRoot)
    {
        Vector3 weightedPosition = Vector3.zero;
        float totalMass = 0f;

        AddMassPoint(ref weightedPosition, ref totalMass, GetTorsoMassPosition(movementRoot), torsoAndHeadMass);
        AddLimbMass(ref weightedPosition, ref totalMass, Managers.TitanRig.LeftShoulder, Managers.TitanRig.LeftElbow, upperArmMass, lowerArmMass);
        AddLimbMass(ref weightedPosition, ref totalMass, Managers.TitanRig.RightShoulder, Managers.TitanRig.RightElbow, upperArmMass, lowerArmMass);
        AddLegMass(ref weightedPosition, ref totalMass, Managers.TitanRig.LeftHip, Managers.TitanRig.LeftKnee, Managers.TitanRig.LeftFoot);
        AddLegMass(ref weightedPosition, ref totalMass, Managers.TitanRig.RightHip, Managers.TitanRig.RightKnee, Managers.TitanRig.RightFoot);

        return totalMass > 0.0001f ? weightedPosition / totalMass : movementRigidbody.worldCenterOfMass;
    }

    private Vector3 GetTorsoMassPosition(Transform movementRoot)
    {
        Transform spine = Managers.TitanRig.Spine;
        if (spine != null)
            return spine.position;

        return movementRoot.position + (Vector3.up * 1.1f);
    }

    private static void AddLimbMass(ref Vector3 weightedPosition, ref float totalMass, Transform upperJoint, Transform lowerJoint, float upperMass, float lowerMass)
    {
        if (upperJoint == null || lowerJoint == null)
            return;

        Vector3 upperCenter = Vector3.Lerp(upperJoint.position, lowerJoint.position, 0.5f);
        Vector3 lowerDirection = (lowerJoint.position - upperJoint.position).normalized;
        Vector3 lowerCenter = lowerJoint.position + lowerDirection * Vector3.Distance(upperJoint.position, lowerJoint.position) * 0.5f;

        AddMassPoint(ref weightedPosition, ref totalMass, upperCenter, upperMass);
        AddMassPoint(ref weightedPosition, ref totalMass, lowerCenter, lowerMass);
    }

    private void AddLegMass(ref Vector3 weightedPosition, ref float totalMass, Transform hip, Transform knee, Transform foot)
    {
        if (hip != null && knee != null)
            AddMassPoint(ref weightedPosition, ref totalMass, Vector3.Lerp(hip.position, knee.position, 0.5f), thighMass);

        if (knee != null && foot != null)
            AddMassPoint(ref weightedPosition, ref totalMass, Vector3.Lerp(knee.position, foot.position, 0.5f), calfMass);

        if (foot != null)
            AddMassPoint(ref weightedPosition, ref totalMass, foot.position, footMass);
    }

    private static void AddMassPoint(ref Vector3 weightedPosition, ref float totalMass, Vector3 position, float mass)
    {
        float clampedMass = Mathf.Max(0f, mass);
        if (clampedMass <= 0f)
            return;

        weightedPosition += position * clampedMass;
        totalMass += clampedMass;
    }

    private SupportInfo CalculateSupportInfo(Transform movementRoot, Vector3 centerOfMass, bool leftGrounded, bool rightGrounded)
    {
        Vector3 center = movementRigidbody.position;
        Vector3 closest = center;
        bool hasLeft = leftGrounded && leftFoot != null;
        bool hasRight = rightGrounded && rightFoot != null;

        if (hasLeft && hasRight)
        {
            center = (leftFoot.position + rightFoot.position) * 0.5f;
            closest = ClampToSupportBounds(movementRoot, centerOfMass, leftFoot.position, rightFoot.position, out float outsideDistance);
            return new SupportInfo(center, closest, outsideDistance > 0f, outsideDistance);
        }

        if (hasLeft || hasRight)
        {
            center = hasLeft ? leftFoot.position : rightFoot.position;
            closest = ClampToSingleFootSupport(centerOfMass, center, out float outsideDistance);
            return new SupportInfo(center, closest, outsideDistance > 0f, outsideDistance);
        }

        return new SupportInfo(center, closest, false, 0f);
    }

    private Vector3 ClampToSupportBounds(Transform movementRoot, Vector3 centerOfMass, Vector3 leftSupport, Vector3 rightSupport, out float outsideDistance)
    {
        Vector3 localCom = movementRoot.InverseTransformPoint(centerOfMass);
        Vector3 localLeft = movementRoot.InverseTransformPoint(leftSupport);
        Vector3 localRight = movementRoot.InverseTransformPoint(rightSupport);
        float padding = Mathf.Max(0f, supportAreaPadding);
        float radius = Mathf.Max(0f, footSupportRadius) + padding;

        float minX = Mathf.Min(localLeft.x, localRight.x) - radius;
        float maxX = Mathf.Max(localLeft.x, localRight.x) + radius;
        float minZ = Mathf.Min(localLeft.z, localRight.z) - radius;
        float maxZ = Mathf.Max(localLeft.z, localRight.z) + radius;
        float clampedX = Mathf.Clamp(localCom.x, minX, maxX);
        float clampedZ = Mathf.Clamp(localCom.z, minZ, maxZ);
        Vector3 localClosest = new(clampedX, localCom.y, clampedZ);
        Vector3 closest = movementRoot.TransformPoint(localClosest);
        outsideDistance = Vector3.ProjectOnPlane(centerOfMass - closest, Vector3.up).magnitude;
        return closest;
    }

    private Vector3 ClampToSingleFootSupport(Vector3 centerOfMass, Vector3 supportCenter, out float outsideDistance)
    {
        Vector3 planarOffset = Vector3.ProjectOnPlane(centerOfMass - supportCenter, Vector3.up);
        float radius = Mathf.Max(0f, footSupportRadius) + Mathf.Max(0f, supportAreaPadding);
        if (planarOffset.magnitude <= radius)
        {
            outsideDistance = 0f;
            return centerOfMass;
        }

        Vector3 closest = supportCenter + planarOffset.normalized * radius;
        outsideDistance = Vector3.ProjectOnPlane(centerOfMass - closest, Vector3.up).magnitude;
        return closest;
    }

    private readonly struct SupportInfo
    {
        public readonly Vector3 Center;
        public readonly Vector3 ClosestPoint;
        public readonly bool IsOutside;
        public readonly float OutsideDistance;

        public SupportInfo(Vector3 center, Vector3 closestPoint, bool isOutside, float outsideDistance)
        {
            Center = center;
            ClosestPoint = closestPoint;
            IsOutside = isOutside;
            OutsideDistance = outsideDistance;
        }
    }

    private void ApplyDragDirectionTorque(Transform movementRoot)
    {
        if (movementRigidbody == null)
        {
            return;
        }

        Vector3 velocity = movementRigidbody.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < minDragSpeed)
        {
            return;
        }

        Vector3 airflowDirection = -velocity / speed;
        Vector3 pressurePoint = centerOfPressure != null
            ? centerOfPressure.position
            : movementRoot.TransformPoint(centerOfPressureLocalOffset);
        Vector3 lever = pressurePoint - movementRigidbody.worldCenterOfMass;
        float dragForceMagnitude = speed * speed * dragTorqueScale;
        Vector3 dragForce = airflowDirection * dragForceMagnitude;
        Vector3 dragTorque = Vector3.Cross(lever, dragForce);
        dragTorque = Vector3.ClampMagnitude(dragTorque, maxDragTorque);
        movementRigidbody.AddTorque(dragTorque, ForceMode.Acceleration);
    }

    private static float ComputeSignedRollAngle(Transform movementRoot)
    {
        Vector3 projectedUp = Vector3.ProjectOnPlane(movementRoot.up, movementRoot.forward);
        Vector3 projectedWorldUp = Vector3.ProjectOnPlane(Vector3.up, movementRoot.forward);
        if (projectedUp.sqrMagnitude < 0.0001f || projectedWorldUp.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Vector3.SignedAngle(projectedWorldUp, projectedUp, movementRoot.forward);
    }

    private bool IsFootGrounded(Transform foot)
    {
        if (foot == null)
        {
            return false;
        }

        Vector3 origin = foot.position + (Vector3.up * groundProbeHeight);
        return Physics.Raycast(origin, Vector3.down, groundProbeDistance + groundProbeHeight, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private void ResolveFeet(Transform movementRoot)
    {
        if (leftFoot != null && rightFoot != null)
        {
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.isHuman)
        {
            leftFoot ??= animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot ??= animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        Transform root = movementRoot != null ? movementRoot : transform;
        leftFoot ??= FindByName(root, "leftfoot", "l_foot", "foot_l", "mixamorig:leftfoot", "bip001 l foot");
        rightFoot ??= FindByName(root, "rightfoot", "r_foot", "foot_r", "mixamorig:rightfoot", "bip001 r foot");

        leftFoot ??= FindBySideKeywords(root, true, "foot", "ankle", "toe");
        rightFoot ??= FindBySideKeywords(root, false, "foot", "ankle", "toe");
    }

    private static Transform FindByName(Transform root, params string[] candidates)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string lower = all[i].name.ToLowerInvariant();
            for (int c = 0; c < candidates.Length; c++)
            {
                if (lower.Contains(candidates[c]))
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    private bool IsTorsoGrounded(Transform movementRoot)
    {
        Vector3 origin = movementRoot.position + (Vector3.up * groundProbeHeight);
        float distance = groundProbeHeight + groundProbeDistance;
        return Physics.Raycast(origin, Vector3.down, distance, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private static Transform FindBySideKeywords(Transform root, bool isLeft, params string[] keywords)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string lower = all[i].name.ToLowerInvariant();
            if (!IsExpectedSide(lower, isLeft))
            {
                continue;
            }

            for (int k = 0; k < keywords.Length; k++)
            {
                if (lower.Contains(keywords[k]))
                {
                    return all[i];
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

    private void UpdateWaistRotation(float waistInput, float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady() || Managers.TitanRig.Spine == null)
        {
            return;
        }

        float nextWaistYaw = Mathf.Clamp(
            Managers.TitanRig.WaistYaw + (waistInput * waistTurnSpeed * deltaTime),
            waistYawLimit.x,
            waistYawLimit.y);

        Managers.TitanRig.SetWaistYaw(nextWaistYaw);
        Managers.TitanRig.ApplyTorsoPose();
    }
}
