using UnityEngine;

public sealed class TitanClawWireController : MonoBehaviour
{
    private const string ClawPrefabName = "Claw";
    private const float MaxWireLength = 3f;
    private const float LaunchDuration = 0.1f;
    private const float RetractDuration = 3f;
    private const float LaunchSpeed = 30f;
    private const float Gravity = -9.81f;
    private const float ClawHitRadius = 0.18f;
    private const float WireWidth = 0.035f;
    private const int WireSegmentCount = 12;
    private const int WireConstraintIterations = 4;
    private const float WireDamping = 0.995f;

    private TitanClawWirePhase _phase;
    private float _phaseTime;
    private float _currentLength;
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
    private LineRenderer _wireRenderer;
    private Material _wireMaterial;
    private readonly Vector3[] _wirePositions = new Vector3[WireSegmentCount];
    private readonly Vector3[] _wirePreviousPositions = new Vector3[WireSegmentCount];
    private bool _wireInitialized;
    private TitanStat _attackerStat;
    private bool _hitBossThisLaunch;
    private Vector3 _launchStartPosition;
    private Quaternion _launchStartRotation;
    private Vector3 _launchDirection;
    private Vector3 _retractStartPosition;
    private Quaternion _retractStartRotation;

    public TitanClawWirePhase Phase => _phase;
    public float CurrentLength => _currentLength;
    public bool CanLaunch => _phase == TitanClawWirePhase.Idle;

    public bool TryLaunch(Stat attacker)
    {
        if (!CanLaunch)
            return false;

        EnsureClawMount();
        if (_clawMount == null)
            return false;

        _phase = TitanClawWirePhase.Launching;
        _phaseTime = 0f;
        _currentLength = 0f;
        _attackerStat = attacker as TitanStat;
        _hitBossThisLaunch = false;
        CacheLaunchPose();
        HideMountedClaw();
        SpawnClawPrefab(_launchStartPosition, _launchStartRotation);
        InitializeWire(_launchStartPosition, _launchStartPosition);
        UpdateVisuals();
        return true;
    }

    public void TickServer(float deltaTime)
    {
        if (_phase == TitanClawWirePhase.Idle)
        {
            _currentLength = 0f;
            UpdateVisuals();
            return;
        }

        float dt = Mathf.Max(0f, deltaTime);
        _phaseTime += dt;

        if (_phase == TitanClawWirePhase.Launching)
        {
            SimulateLaunchingClaw(dt);
        }
        else if (_phase == TitanClawWirePhase.Retracting)
        {
            SimulateRetractingClaw();
        }

        UpdateVisuals();
    }

    public TitanClawWireSnapshot GetSnapshot()
    {
        return new TitanClawWireSnapshot
        {
            Phase = _phase,
            CurrentLength = _currentLength,
            ClawPosition = ResolveClawPosition(),
            ClawRotation = ResolveClawRotation(),
        };
    }

    public void ApplySnapshot(TitanClawWireSnapshot snapshot)
    {
        _phase = snapshot.Phase;
        _currentLength = Mathf.Clamp(snapshot.CurrentLength, 0f, MaxWireLength);
        _phaseTime = 0f;
        _hitBossThisLaunch = _phase == TitanClawWirePhase.Idle || _hitBossThisLaunch;

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
        }

        SetSpawnedClawPose(snapshot.ClawPosition, snapshot.ClawRotation);
        UpdateVisuals();
    }

    private void SimulateLaunchingClaw(float dt)
    {
        if (_spawnedClawTransform == null)
            SpawnClawPrefab(_launchStartPosition, _launchStartRotation);

        Vector3 previousPosition = ResolveClawPosition();
        Vector3 gravityOffset = Vector3.up * (0.5f * Gravity * _phaseTime * _phaseTime);
        Vector3 nextPosition = _launchStartPosition + (_launchDirection * LaunchSpeed * _phaseTime) + gravityOffset;
        Vector3 anchor = ResolveWireAnchorPosition();
        Vector3 offset = nextPosition - anchor;
        float distance = offset.magnitude;

        if (distance >= MaxWireLength || _phaseTime >= LaunchDuration)
        {
            Vector3 direction = distance > 0.0001f ? offset / distance : _launchDirection;
            nextPosition = anchor + direction * MaxWireLength;
            _currentLength = MaxWireLength;
            SetSpawnedClawPose(nextPosition, ResolveClawRotation());
            TryApplyClawAttack(previousPosition, nextPosition);
            EnterRetracting();
            return;
        }

        _currentLength = distance;
        SetSpawnedClawPose(nextPosition, ResolveClawRotation());
        TryApplyClawAttack(previousPosition, nextPosition);
    }

    private void EnterRetracting()
    {
        _phase = TitanClawWirePhase.Retracting;
        _phaseTime = 0f;
        _retractStartPosition = ResolveClawPosition();
        _retractStartRotation = ResolveClawRotation();
    }

    private void SimulateRetractingClaw()
    {
        float ratio = Mathf.Clamp01(_phaseTime / RetractDuration);
        Vector3 anchor = ResolveWireAnchorPosition();
        Vector3 nextPosition = Vector3.Lerp(_retractStartPosition, anchor, ratio);
        Quaternion nextRotation = Quaternion.Slerp(_retractStartRotation, _launchStartRotation, ratio);
        _currentLength = Vector3.Distance(anchor, nextPosition);
        SetSpawnedClawPose(nextPosition, nextRotation);

        if (ratio >= 1f)
        {
            _phase = TitanClawWirePhase.Idle;
            _phaseTime = 0f;
            _currentLength = 0f;
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

        SimulateWire(Time.deltaTime);
        _wireRenderer.positionCount = WireSegmentCount;
        _wireRenderer.SetPositions(_wirePositions);
    }

    private void SimulateWire(float deltaTime)
    {
        Vector3 start = ResolveWireAnchorPosition();
        Vector3 end = ResolveClawPosition();

        if (!_wireInitialized)
            InitializeWire(start, end);

        float dt = Mathf.Max(0f, deltaTime);
        for (int i = 1; i < WireSegmentCount - 1; i++)
        {
            Vector3 current = _wirePositions[i];
            Vector3 velocity = (current - _wirePreviousPositions[i]) * WireDamping;
            _wirePreviousPositions[i] = current;
            _wirePositions[i] = current + velocity + Vector3.up * (Gravity * dt * dt);
        }

        _wirePositions[0] = start;
        _wirePositions[^1] = end;
        float segmentLength = Mathf.Max(Vector3.Distance(start, end), 0.001f) / (WireSegmentCount - 1);

        for (int iteration = 0; iteration < WireConstraintIterations; iteration++)
        {
            _wirePositions[0] = start;
            _wirePositions[^1] = end;

            for (int i = 0; i < WireSegmentCount - 1; i++)
                ConstrainWireSegment(i, i + 1, segmentLength);
        }
    }

    private void ConstrainWireSegment(int a, int b, float segmentLength)
    {
        Vector3 delta = _wirePositions[b] - _wirePositions[a];
        float distance = delta.magnitude;
        if (distance < 0.0001f)
            return;

        Vector3 correction = delta.normalized * (distance - segmentLength);
        bool aFixed = a == 0;
        bool bFixed = b == WireSegmentCount - 1;

        if (!aFixed && !bFixed)
        {
            _wirePositions[a] += correction * 0.5f;
            _wirePositions[b] -= correction * 0.5f;
        }
        else if (aFixed && !bFixed)
        {
            _wirePositions[b] -= correction;
        }
        else if (!aFixed)
        {
            _wirePositions[a] += correction;
        }
    }

    private void InitializeWire(Vector3 start, Vector3 end)
    {
        for (int i = 0; i < WireSegmentCount; i++)
        {
            float ratio = i / (float)(WireSegmentCount - 1);
            Vector3 position = Vector3.Lerp(start, end, ratio);
            _wirePositions[i] = position;
            _wirePreviousPositions[i] = position;
        }

        _wireInitialized = true;
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
        SetSpawnedClawPose(position, rotation);
    }

    private void DestroySpawnedClaw()
    {
        if (_spawnedClaw != null)
            Managers.Resource.Destory(_spawnedClaw);

        _spawnedClaw = null;
        _spawnedClawTransform = null;
        _wireInitialized = false;
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
        _wireRenderer.positionCount = WireSegmentCount;
        _wireRenderer.useWorldSpace = true;
        _wireRenderer.startWidth = WireWidth;
        _wireRenderer.endWidth = WireWidth;
        _wireRenderer.numCapVertices = 4;
        _wireMaterial = new Material(Shader.Find("Sprites/Default"));
        _wireMaterial.color = Color.yellow;
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
}

public enum TitanClawWirePhase
{
    Idle = 0,
    Launching = 1,
    Retracting = 2,
}

public struct TitanClawWireSnapshot
{
    public TitanClawWirePhase Phase;
    public float CurrentLength;
    public Vector3 ClawPosition;
    public Quaternion ClawRotation;
}
