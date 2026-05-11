using UnityEngine;

public sealed class TitanClawWireController : MonoBehaviour
{
    private const string ClawPrefabName = "Claw";

    private const float ClawHitRadius = 0.18f;

    [Header("Claw Physics")]
    [SerializeField] private float clawMass = 8f;
    [SerializeField] private float clawDrag = 1f;
    [SerializeField] private float clawAngularDrag = 3f;
    [SerializeField] private float launchSpeed = 30f;
    [SerializeField] private float postHitHorizontalDamping = 0.05f;

    [Header("Wire")]
    [SerializeField] private float maxChainLength = 3f;
    [SerializeField] private float chainExtendSpeed = 30f;
    [SerializeField] private float chainRetractSpeed = 1f;
    [SerializeField] private float retractPullForce = 40f;
    [SerializeField] private float recoverDistance = 0.2f;
    [SerializeField] private int lineSegmentCount = 16;
    [SerializeField] private float slackSagMultiplier = 0.4f;
    [SerializeField] private float blockedPileupMultiplier = 0.8f;
    [SerializeField] private float wireWidth = 0.035f;
    [SerializeField] private Texture2D wireTileTexture;
    [SerializeField] private float wireTileWorldLength = 0.35f;

    private const string DefaultWireTextureResourcePath = "Arts/Titan/Texture/Chain";

    private TitanClawWirePhase _phase;
    private float _phaseTime;
    private float _currentChainLength;
    private float _slackLength;
    private Transform _clawMount;
    private Transform _clawOriginalParent;
    private Vector3 _clawOriginalLocalPosition;
    private Quaternion _clawOriginalLocalRotation;
    private Vector3 _clawOriginalLocalScale;
    private bool _hasClawOriginalPose;
    private Renderer[] _mountedClawRenderers;
    private bool[] _mountedClawRendererStates;
    private Collider[] _mountedClawColliders;
    private bool[] _mountedClawColliderStates;
    private GameObject _spawnedClaw;
    private Transform _spawnedClawTransform;
    private Rigidbody _spawnedClawRigidbody;
    private LineRenderer _wireRenderer;
    private Material _wireMaterial;
    private TitanStat _attackerStat;
    private bool _hitBossThisLaunch;
    private Vector3 _launchStartPosition;
    private Quaternion _launchStartRotation;
    private Vector3 _launchDirection;
    private Vector3 _retractStartPosition;
    private Quaternion _retractStartRotation;

    public TitanClawWirePhase Phase => _phase;
    public float CurrentLength => _currentChainLength;
    public bool CanLaunch => _phase == TitanClawWirePhase.Idle;

    public bool TryLaunch(Stat attacker)
    {
        if (!CanLaunch)
            return false;

        EnsureClawMount();
        if (_clawMount == null)
            return false;

        SetPhase(TitanClawWirePhase.Launching, "TryLaunch");
        _phaseTime = 0f;
        _currentChainLength = 0f;
        _slackLength = 0f;
        _attackerStat = attacker as TitanStat;
        _hitBossThisLaunch = false;
        CacheLaunchPose();
        HideMountedClaw();
        SpawnClawPrefab(_launchStartPosition, _launchStartRotation);
        EnsureSpawnedClawPhysics(launchImmediately: true);
        UpdateVisuals();
        return true;
    }

    public void TickServer(float deltaTime)
    {
        if (_phase == TitanClawWirePhase.Idle)
        {
            _currentChainLength = 0f;
            _slackLength = 0f;
            UpdateVisuals();
            return;
        }

        float dt = Mathf.Max(0f, deltaTime);
        _phaseTime += dt;

        EnsureSpawnedClawPhysics(launchImmediately: false);
        UpdateChainLength(dt);

        if (_phase == TitanClawWirePhase.Launching)
            SimulateLaunchingClaw(dt);
        else if (_phase == TitanClawWirePhase.HitBlocked)
            SimulateHitBlocked(dt);
        else if (_phase == TitanClawWirePhase.Retracting)
            SimulateRetractingClaw(dt);

        UpdateVisuals();
    }

    public TitanClawWireSnapshot GetSnapshot()
    {
        return new TitanClawWireSnapshot
        {
            Phase = _phase,
            CurrentLength = _currentChainLength,
            ClawPosition = ResolveClawPosition(),
            ClawRotation = ResolveClawRotation(),
        };
    }

    public void ApplySnapshot(TitanClawWireSnapshot snapshot)
    {
        SetPhase(snapshot.Phase, "ApplySnapshot");
        _currentChainLength = Mathf.Clamp(snapshot.CurrentLength, 0f, maxChainLength);
        _phaseTime = 0f;
        _hitBossThisLaunch = _phase == TitanClawWirePhase.Idle || _hitBossThisLaunch;
        _slackLength = 0f;

        if (_phase == TitanClawWirePhase.Idle)
        {
            RestoreMountedClaw();
            DestroySpawnedClaw();
        }
        else
        {
            EnsureClawMount();
            if (_clawMount != null)
                HideMountedClaw();

            SpawnClawPrefab(snapshot.ClawPosition, snapshot.ClawRotation);
            EnsureSpawnedClawPhysics(launchImmediately: false);
        }

        SetSpawnedClawPose(snapshot.ClawPosition, snapshot.ClawRotation);
        UpdateVisuals();
    }

    private void SimulateLaunchingClaw(float dt)
    {
        if (_spawnedClawTransform == null)
            SpawnClawPrefab(_launchStartPosition, _launchStartRotation);

        EnsureSpawnedClawPhysics(launchImmediately: false);

        Vector3 anchor = ResolveWireAnchorPosition();
        Vector3 previousPosition = ResolveClawPosition();
        Vector3 nextPosition = ResolveClawPosition();

        ApplyDistanceConstraintDuringLaunch(anchor);
        nextPosition = ResolveClawPosition();
        TryApplyClawAttack(previousPosition, nextPosition);

        // Launch ends once chain fully extends; Retracting will first remove slack, then pull.
        if (_currentChainLength >= maxChainLength)
            EnterRetracting();
    }

    private void EnterRetracting()
    {
        SetPhase(TitanClawWirePhase.Retracting, "EnterRetracting");
        _phaseTime = 0f;
        _retractStartPosition = ResolveClawPosition();
        _retractStartRotation = ResolveClawRotation();
    }

    private void SimulateHitBlocked(float dt)
    {
        Vector3 anchor = ResolveWireAnchorPosition();
        UpdateSlackLength(anchor);

        if (_spawnedClawRigidbody != null)
        {
            // Keep horizontal movement almost dead while still allowing gravity-driven falling.
            Vector3 v = GetRigidbodyVelocity(_spawnedClawRigidbody);
            v.x *= postHitHorizontalDamping;
            v.z *= postHitHorizontalDamping;
            SetRigidbodyVelocity(_spawnedClawRigidbody, v);
        }

        // Once the chain finishes extending, start retracting (slack will be consumed first).
        if (_currentChainLength >= maxChainLength)
            EnterRetracting();
    }

    private void SimulateRetractingClaw(float dt)
    {
        Vector3 anchor = ResolveWireAnchorPosition();
        UpdateSlackLength(anchor);

        if (_spawnedClawRigidbody != null)
        {
            // First, retract chain length (removes slack visually). Only pull the claw once slack is nearly gone.
            if (_slackLength <= 0.02f)
            {
                ApplyDistanceConstraintTaut(anchor);

                Vector3 clawPos = _spawnedClawRigidbody.position;
                Vector3 toAnchor = anchor - clawPos;
                float dist = toAnchor.magnitude;
                if (dist > 0.0001f)
                {
                    Vector3 dir = toAnchor / dist;
                    _spawnedClawRigidbody.AddForce(dir * retractPullForce, ForceMode.Acceleration);
                }
            }
        }

        if (_spawnedClawTransform != null && Vector3.Distance(anchor, _spawnedClawTransform.position) <= recoverDistance)
        {
            SetPhase(TitanClawWirePhase.Idle, "RetractComplete");
            _phaseTime = 0f;
            _currentChainLength = 0f;
            _slackLength = 0f;
            RestoreMountedClaw();
            DestroySpawnedClaw();
        }
    }

    private void TryApplyClawAttack(Vector3 from, Vector3 to)
    {
        if (_hitBossThisLaunch || _phase != TitanClawWirePhase.Launching || _attackerStat == null)
            return;

        if (TryFindBossHit(from, to, out BossController hitBoss))
        {
            hitBoss.ReceiveClawAttach(_attackerStat);
            _hitBossThisLaunch = true;
        }
    }

    private static bool TryFindBossHit(Vector3 from, Vector3 to, out BossController hitBoss)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance > 0.0001f)
        {
            Ray ray = new(from, delta / distance);
            RaycastHit[] hits = Physics.SphereCastAll(ray, ClawHitRadius, distance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;

                BossController boss = hitCollider.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    hitBoss = boss;
                    return true;
                }
            }
        }

        Vector3 clawPosition = to;
        BossController[] bosses = Object.FindObjectsByType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController boss = bosses[i];
            if (boss == null || !boss.IsWithinHitRadius(clawPosition, ClawHitRadius))
                continue;

            hitBoss = boss;
            return true;
        }

        hitBoss = null;
        return false;
    }

    private void UpdateVisuals()
    {
        EnsureWireRenderer();

        bool visible = _phase != TitanClawWirePhase.Idle;
        _wireRenderer.enabled = visible;
        if (!visible)
            return;

        RenderWireCurve();
    }

    private void RenderWireCurve()
    {
        Vector3 anchor = ResolveWireAnchorPosition();
        Vector3 claw = ResolveClawPosition();
        UpdateSlackLength(anchor);

        int segments = Mathf.Max(2, lineSegmentCount);
        _wireRenderer.positionCount = segments;

        float distance = Vector3.Distance(anchor, claw);
        float slack = Mathf.Max(0f, _currentChainLength - distance);

        float baseSag = 0.05f;
        float sagAmount = baseSag + (slack * slackSagMultiplier);
        Vector3 midpoint = (anchor + claw) * 0.5f;
        Vector3 control = midpoint + (Vector3.down * sagAmount);

        bool blockedPileup = _phase == TitanClawWirePhase.HitBlocked && slack > 0.001f;
        float pileupStartT = 0.6f;
        float pileupExponent = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(blockedPileupMultiplier));

        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float t = segments == 1 ? 1f : i / (float)(segments - 1);
            float warpedT = t;

            if (blockedPileup && t >= pileupStartT)
            {
                float u = Mathf.InverseLerp(pileupStartT, 1f, t);
                float uWarped = Mathf.Pow(u, pileupExponent);
                warpedT = Mathf.Lerp(pileupStartT, 1f, uWarped);
            }

            Vector3 p = EvaluateQuadraticBezier(anchor, control, claw, warpedT);

            if (blockedPileup)
            {
                float nearEnd = Mathf.InverseLerp(pileupStartT, 1f, t);
                float extraSag = slack * blockedPileupMultiplier * (nearEnd * nearEnd);
                p += Vector3.down * extraSag;
            }

            points[i] = p;
        }

        float curveLength = 0f;

        for (int i = 0; i < segments; i++)
        {
            Vector3 p = points[segments - 1 - i];
            _wireRenderer.SetPosition(i, p);

            if (i > 0)
            {
                Vector3 prev = points[segments - i];
                curveLength += Vector3.Distance(prev, p);
            }
        }

        float tileLen = Mathf.Max(0.01f, wireTileWorldLength);
        float repeats = Mathf.Max(0.001f, curveLength / tileLen);

        _wireRenderer.textureMode = LineTextureMode.RepeatPerSegment;
        _wireRenderer.textureScale = new Vector2(repeats, 1f);

        if (_wireMaterial != null && _wireMaterial.mainTexture != null)
        {
            float endU = repeats;
            float offsetX = Mathf.Ceil(endU) - endU;
            _wireMaterial.mainTextureOffset = new Vector2(offsetX, 0f);
        }
    }

    private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private void EnsureClawMount()
    {
        if (_clawMount != null)
            return;

        _clawMount = Managers.TitanRig.Claw;
        if (_clawMount == null)
            return;

        _clawOriginalParent = _clawMount.parent;
        _clawOriginalLocalPosition = _clawMount.localPosition;
        _clawOriginalLocalRotation = _clawMount.localRotation;
        _clawOriginalLocalScale = _clawMount.localScale;
        CacheMountedClawVisibilityTargets();
        _hasClawOriginalPose = true;
    }

    private void CacheMountedClawVisibilityTargets()
    {
        _mountedClawRenderers = _clawMount.GetComponentsInChildren<Renderer>(true);
        _mountedClawRendererStates = new bool[_mountedClawRenderers.Length];
        for (int i = 0; i < _mountedClawRenderers.Length; i++)
            _mountedClawRendererStates[i] = _mountedClawRenderers[i].enabled;

        _mountedClawColliders = _clawMount.GetComponentsInChildren<Collider>(true);
        _mountedClawColliderStates = new bool[_mountedClawColliders.Length];
        for (int i = 0; i < _mountedClawColliders.Length; i++)
            _mountedClawColliderStates[i] = _mountedClawColliders[i].enabled;
    }

    private void CacheLaunchPose()
    {
        _launchStartPosition = _clawMount.position;
        _launchStartRotation = _clawMount.rotation;
        _launchDirection = ResolveLaunchDirection(_clawMount);
    }

    private void HideMountedClaw()
    {
        if (_clawMount == null)
            return;

        for (int i = 0; i < _mountedClawRenderers.Length; i++)
            _mountedClawRenderers[i].enabled = false;

        for (int i = 0; i < _mountedClawColliders.Length; i++)
            _mountedClawColliders[i].enabled = false;

        _clawMount.localScale = Vector3.zero;
    }

    private void RestoreMountedClaw()
    {
        if (!_hasClawOriginalPose || _clawMount == null)
            return;

        _clawMount.localPosition = _clawOriginalLocalPosition;
        _clawMount.localRotation = _clawOriginalLocalRotation;
        _clawMount.localScale = _clawOriginalLocalScale;

        for (int i = 0; i < _mountedClawRenderers.Length; i++)
            _mountedClawRenderers[i].enabled = _mountedClawRendererStates[i];

        for (int i = 0; i < _mountedClawColliders.Length; i++)
            _mountedClawColliders[i].enabled = _mountedClawColliderStates[i];
    }

    private void SpawnClawPrefab(Vector3 position, Quaternion rotation)
    {
        if (_spawnedClaw != null)
        {
            SetSpawnedClawPose(position, rotation);
            return;
        }

        _spawnedClaw = Managers.Resource.Instantiate(ClawPrefabName);
        if (_spawnedClaw == null)
            return;

        _spawnedClawTransform = _spawnedClaw.transform;
        _spawnedClawRigidbody = _spawnedClaw.GetComponent<Rigidbody>();
        if (_spawnedClawRigidbody == null)
            _spawnedClawRigidbody = _spawnedClaw.AddComponent<Rigidbody>();

        ApplyHeavyClawPhysicsMaterial(_spawnedClaw);

        ClawCollisionReporter reporter = _spawnedClaw.GetComponent<ClawCollisionReporter>();
        if (reporter == null)
            reporter = _spawnedClaw.AddComponent<ClawCollisionReporter>();
        reporter.Initialize(this);

        SetSpawnedClawPose(position, rotation);
    }

    private static void ApplyHeavyClawPhysicsMaterial(GameObject claw)
    {
        if (claw == null)
            return;

        Collider[] colliders = claw.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
            return;

#if UNITY_6000_0_OR_NEWER
        PhysicsMaterial mat = new("Claw_Heavy")
        {
            dynamicFriction = 1f,
            staticFriction = 1f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };
#else
        PhysicMaterial mat = new("Claw_Heavy")
        {
            dynamicFriction = 1f,
            staticFriction = 1f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounceCombine = PhysicMaterialCombine.Minimum,
        };
#endif

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].material = mat;
        }
    }

    private void DestroySpawnedClaw()
    {
        if (_spawnedClaw != null)
            Managers.Resource.Destory(_spawnedClaw);

        _spawnedClaw = null;
        _spawnedClawTransform = null;
        _spawnedClawRigidbody = null;
    }

    private void SetSpawnedClawPose(Vector3 position, Quaternion rotation)
    {
        if (_spawnedClawTransform == null)
            return;

        _spawnedClawTransform.SetPositionAndRotation(position, rotation);
    }

    private void EnsureWireRenderer()
    {
        if (_wireRenderer != null)
            return;

        GameObject wire = new("RightClaw_WireRenderer");
        wire.transform.SetParent(transform, worldPositionStays: true);
        _wireRenderer = wire.AddComponent<LineRenderer>();
        _wireRenderer.positionCount = Mathf.Max(2, lineSegmentCount);
        _wireRenderer.useWorldSpace = true;
        _wireRenderer.startWidth = wireWidth;
        _wireRenderer.endWidth = wireWidth;
        _wireRenderer.numCapVertices = 4;
        _wireRenderer.textureMode = LineTextureMode.RepeatPerSegment;

        if (wireTileTexture == null)
            wireTileTexture = Resources.Load<Texture2D>(DefaultWireTextureResourcePath);

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _wireMaterial = new Material(shader);
        _wireMaterial.color = Color.white;
        if (wireTileTexture != null)
        {
            wireTileTexture.wrapMode = TextureWrapMode.Repeat;
            _wireMaterial.mainTexture = wireTileTexture;
            _wireMaterial.mainTextureScale = Vector2.one;
        }

        _wireRenderer.material = _wireMaterial;
    }

    private Vector3 ResolveWireAnchorPosition()
    {
        if (_hasClawOriginalPose && _clawOriginalParent != null)
            return _clawOriginalParent.TransformPoint(_clawOriginalLocalPosition);

        Transform anchor = ResolveAnchor();
        return anchor != null ? anchor.position : _launchStartPosition;
    }

    private Vector3 ResolveClawPosition()
    {
        return _spawnedClawTransform != null ? _spawnedClawTransform.position : _launchStartPosition;
    }

    private Quaternion ResolveClawRotation()
    {
        return _spawnedClawTransform != null ? _spawnedClawTransform.rotation : _launchStartRotation;
    }

    private static Transform ResolveAnchor()
    {
        Transform anchor = Managers.TitanRig.RightElbow;
        return anchor != null ? anchor : Managers.TitanRig.MovementRoot;
    }

    private static Vector3 ResolveLaunchDirection(Transform source)
    {
        Vector3 direction = -source.right;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -Managers.TitanRig.MovementRoot.right;

        if (direction.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return direction.normalized;
    }

    private void EnsureSpawnedClawPhysics(bool launchImmediately)
    {
        if (_spawnedClaw == null)
            return;

        if (_spawnedClawRigidbody == null)
            _spawnedClawRigidbody = _spawnedClaw.GetComponent<Rigidbody>();
        if (_spawnedClawRigidbody == null)
            _spawnedClawRigidbody = _spawnedClaw.AddComponent<Rigidbody>();

        Rigidbody rb = _spawnedClawRigidbody;
        rb.mass = clawMass;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = clawDrag;
        rb.angularDamping = clawAngularDrag;
#else
        rb.drag = clawDrag;
        rb.angularDrag = clawAngularDrag;
#endif
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (launchImmediately)
        {
            Vector3 dir = _launchDirection.sqrMagnitude > 0.0001f ? _launchDirection : ResolveLaunchDirection(_clawMount);
            SetRigidbodyVelocity(rb, dir * launchSpeed);
        }
    }

    private void UpdateChainLength(float dt)
    {
        if (_phase == TitanClawWirePhase.Launching || _phase == TitanClawWirePhase.HitBlocked)
        {
            _currentChainLength = Mathf.Min(maxChainLength, _currentChainLength + (chainExtendSpeed * dt));
        }
        else if (_phase == TitanClawWirePhase.Retracting)
        {
            _currentChainLength = Mathf.Max(0f, _currentChainLength - (chainRetractSpeed * dt));

            // Chain length cannot become shorter than the straight-line distance (taut chain).
            Vector3 anchor = ResolveWireAnchorPosition();
            float distance = Vector3.Distance(anchor, ResolveClawPosition());
            if (_currentChainLength < distance)
                _currentChainLength = distance;
        }
    }

    private void UpdateSlackLength(Vector3 anchor)
    {
        Vector3 claw = ResolveClawPosition();
        float distance = Vector3.Distance(anchor, claw);
        _slackLength = Mathf.Max(0f, _currentChainLength - distance);
    }

    private void ApplyDistanceConstraintDuringLaunch(Vector3 anchor)
    {
        if (_spawnedClawRigidbody == null)
            return;

        Rigidbody rb = _spawnedClawRigidbody;
        Vector3 clawPos = rb.position;
        Vector3 toClaw = clawPos - anchor;
        float distance = toClaw.magnitude;
        if (distance < 0.0001f)
        {
            UpdateSlackLength(anchor);
            return;
        }

        float allowed = Mathf.Max(0f, _currentChainLength);
        if (distance > allowed)
        {
            // Only enforce outward constraint during Launching so we don't shove the claw into colliders when blocked.
            Vector3 dir = toClaw / distance;
            rb.position = anchor + (dir * allowed);

            Vector3 v = GetRigidbodyVelocity(rb);
            float outward = Vector3.Dot(v, dir);
            if (outward > 0f)
                v -= dir * outward;
            SetRigidbodyVelocity(rb, v);
        }

        UpdateSlackLength(anchor);
    }

    private void ApplyDistanceConstraintTaut(Vector3 anchor)
    {
        if (_spawnedClawRigidbody == null)
            return;

        Rigidbody rb = _spawnedClawRigidbody;
        Vector3 clawPos = rb.position;
        Vector3 toClaw = clawPos - anchor;
        float distance = toClaw.magnitude;
        if (distance < 0.0001f)
            return;

        float allowed = Mathf.Max(0f, _currentChainLength);
        if (distance > allowed)
        {
            Vector3 dir = toClaw / distance;
            rb.position = anchor + (dir * allowed);

            Vector3 v = GetRigidbodyVelocity(rb);
            float outward = Vector3.Dot(v, dir);
            if (outward > 0f)
                v -= dir * outward;
            SetRigidbodyVelocity(rb, v);
        }
    }

    internal void NotifyClawHitNonBoss(Collision collision)
    {
        if (_phase != TitanClawWirePhase.Launching)
            return;

        // Ignore boss collisions: Boss attack is handled via spherecast flow.
        if (collision != null)
        {
            Collider col = collision.collider;
            if (col != null && col.GetComponentInParent<BossController>() != null)
                return;
        }

        SetPhase(TitanClawWirePhase.HitBlocked, "HitNonBoss");

        if (_spawnedClawRigidbody == null)
            return;

        Vector3 v = GetRigidbodyVelocity(_spawnedClawRigidbody);
        v.x *= postHitHorizontalDamping;
        v.z *= postHitHorizontalDamping;
        SetRigidbodyVelocity(_spawnedClawRigidbody, v);
    }

    private void SetPhase(TitanClawWirePhase next, string reason)
    {
        if (_phase == next)
            return;

        TitanClawWirePhase prev = _phase;
        _phase = next;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TitanClawWire] Phase {prev} -> {next} (reason={reason}, chain={_currentChainLength:0.###}, slack={_slackLength:0.###})", this);
#endif
    }

    private static Vector3 GetRigidbodyVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetRigidbodyVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    private sealed class ClawCollisionReporter : MonoBehaviour
    {
        private TitanClawWireController _owner;

        public void Initialize(TitanClawWireController owner)
        {
            _owner = owner;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_owner != null)
                _owner.NotifyClawHitNonBoss(collision);
        }
    }
}

public enum TitanClawWirePhase
{
    Idle = 0,
    Launching = 1,
    HitBlocked = 2,
    Retracting = 3,
}

public struct TitanClawWireSnapshot
{
    public TitanClawWirePhase Phase;
    public float CurrentLength;
    public Vector3 ClawPosition;
    public Quaternion ClawRotation;
}
