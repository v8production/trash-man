using UnityEngine;

public sealed class TitanClawWireController : MonoBehaviour
{
    private const float MaxWireLength = 3f;
    private const float LaunchDuration = 0.1f;
    private const float RetractDuration = 3f;
    private const float ClawHitRadius = 0.18f;
    private const float WireWidth = 0.035f;

    private TitanClawWirePhase _phase;
    private float _phaseTime;
    private float _currentLength;
    private Transform _clawVisual;
    private Transform _clawOriginalParent;
    private Vector3 _clawOriginalLocalPosition;
    private Quaternion _clawOriginalLocalRotation;
    private bool _hasClawOriginalPose;
    private LineRenderer _wireRenderer;
    private Material _wireMaterial;
    private TitanStat _attackerStat;
    private bool _hitBossThisLaunch;
    private Vector3 _launchStartPosition;
    private Quaternion _launchStartRotation;
    private Vector3 _launchDirection;

    public TitanClawWirePhase Phase => _phase;
    public float CurrentLength => _currentLength;
    public bool CanLaunch => _phase == TitanClawWirePhase.Idle;

    public bool TryLaunch(Stat attacker)
    {
        if (!CanLaunch)
            return false;

        _phase = TitanClawWirePhase.Launching;
        _phaseTime = 0f;
        _currentLength = 0f;
        _attackerStat = attacker as TitanStat;
        _hitBossThisLaunch = false;
        CacheLaunchPose();
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

        _phaseTime += Mathf.Max(0f, deltaTime);

        if (_phase == TitanClawWirePhase.Launching)
        {
            float ratio = Mathf.Clamp01(_phaseTime / LaunchDuration);
            _currentLength = MaxWireLength * ratio;

            if (ratio >= 1f)
            {
                _phase = TitanClawWirePhase.Retracting;
                _phaseTime = 0f;
                _currentLength = MaxWireLength;
            }
        }
        else if (_phase == TitanClawWirePhase.Retracting)
        {
            float ratio = Mathf.Clamp01(_phaseTime / RetractDuration);
            _currentLength = Mathf.Lerp(MaxWireLength, 0f, ratio);

            if (ratio >= 1f)
            {
                _phase = TitanClawWirePhase.Idle;
                _phaseTime = 0f;
                _currentLength = 0f;
            }
        }

        UpdateVisuals();
        TryApplyClawAttack();
    }

    public TitanClawWireSnapshot GetSnapshot()
    {
        return new TitanClawWireSnapshot
        {
            Phase = _phase,
            CurrentLength = _currentLength,
        };
    }

    public void ApplySnapshot(TitanClawWireSnapshot snapshot)
    {
        _phase = snapshot.Phase;
        _currentLength = Mathf.Clamp(snapshot.CurrentLength, 0f, MaxWireLength);
        _phaseTime = 0f;
        _hitBossThisLaunch = _phase == TitanClawWirePhase.Idle || _hitBossThisLaunch;
        UpdateVisuals();
    }

    private void TryApplyClawAttack()
    {
        if (_hitBossThisLaunch || _phase == TitanClawWirePhase.Idle || _attackerStat == null)
            return;

        Vector3 clawPosition = ResolveClawPosition();
        BossController[] bosses = Object.FindObjectsByType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController boss = bosses[i];
            if (boss == null || !boss.IsWithinHitRadius(clawPosition, ClawHitRadius))
                continue;

            boss.ReceiveAttack(_attackerStat);
            _hitBossThisLaunch = true;
            return;
        }
    }

    private Vector3 ResolveClawPosition()
    {
        return ResolveLaunchStartPosition() + (ResolveLaunchDirection() * _currentLength);
    }

    private void UpdateVisuals()
    {
        EnsureVisuals();

        Vector3 start = ResolveLaunchStartPosition();
        Vector3 direction = ResolveLaunchDirection();
        Vector3 end = start + (direction * _currentLength);
        bool visible = _phase != TitanClawWirePhase.Idle || _currentLength > 0.001f;

        if (_clawVisual != null)
        {
            if (visible)
            {
                _clawVisual.position = end;
                _clawVisual.rotation = _launchStartRotation;
            }
            else
            {
                RestoreClawVisualPose();
            }
        }

        _wireRenderer.enabled = visible;
        _wireRenderer.SetPosition(0, start);
        _wireRenderer.SetPosition(1, end);
    }

    private void EnsureVisuals()
    {
        if (_clawVisual == null)
        {
            _clawVisual = Managers.TitanRig.Claw;
            if (_clawVisual != null)
            {
                _clawOriginalParent = _clawVisual.parent;
                _clawOriginalLocalPosition = _clawVisual.localPosition;
                _clawOriginalLocalRotation = _clawVisual.localRotation;
                _hasClawOriginalPose = true;
            }
        }

        if (_wireRenderer == null)
        {
            GameObject wire = new("RightClaw_WireRenderer");
            wire.transform.SetParent(transform, worldPositionStays: true);
            _wireRenderer = wire.AddComponent<LineRenderer>();
            _wireRenderer.positionCount = 2;
            _wireRenderer.useWorldSpace = true;
            _wireRenderer.startWidth = WireWidth;
            _wireRenderer.endWidth = WireWidth;
            _wireRenderer.numCapVertices = 4;

            _wireMaterial = new Material(Shader.Find("Sprites/Default"));
            _wireMaterial.color = Color.yellow;
            _wireRenderer.material = _wireMaterial;
        }
    }

    private void RestoreClawVisualPose()
    {
        if (!_hasClawOriginalPose || _clawVisual == null)
            return;

        if (_clawVisual.parent != _clawOriginalParent)
            _clawVisual.SetParent(_clawOriginalParent, worldPositionStays: false);

        _clawVisual.localPosition = _clawOriginalLocalPosition;
        _clawVisual.localRotation = _clawOriginalLocalRotation;
    }

    private static Transform ResolveAnchor()
    {
        Transform anchor = Managers.TitanRig.RightElbow;
        return anchor != null ? anchor : Managers.TitanRig.MovementRoot;
    }

    private void CacheLaunchPose()
    {
        EnsureVisuals();

        Transform claw = _clawVisual != null ? _clawVisual : Managers.TitanRig.Claw;
        if (claw != null)
        {
            _launchStartPosition = claw.position;
            _launchStartRotation = claw.rotation;
            _launchDirection = ResolveTransformXAxis(claw);
            return;
        }

        Transform anchor = ResolveAnchor();
        _launchStartPosition = anchor.position;
        _launchStartRotation = anchor.rotation;
        _launchDirection = ResolveTransformXAxis(anchor);
    }

    private Vector3 ResolveLaunchStartPosition()
    {
        if (_phase != TitanClawWirePhase.Idle || _currentLength > 0.001f)
            return _launchStartPosition;

        Transform claw = Managers.TitanRig.Claw;
        return claw != null ? claw.position : ResolveAnchor().position;
    }

    private Vector3 ResolveLaunchDirection()
    {
        if (_launchDirection.sqrMagnitude >= 0.0001f)
            return _launchDirection.normalized;

        Transform claw = Managers.TitanRig.Claw;
        if (claw != null)
            return ResolveTransformXAxis(claw);

        return ResolveTransformXAxis(ResolveAnchor());
    }

    private static Vector3 ResolveTransformXAxis(Transform source)
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
}
