using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class TitanRoleNetworkDriver : MonoBehaviour
{
    private float _nextDebugLogTime;
    private const float DebugLogIntervalSeconds = 0.50f;
    private bool _shouldLogThisFrame;
    private TitanTorsoRoleController _torsoController;
    private TitanLegAnchorResolver _legAnchorResolver;
    private TitanLeftArmRoleController _leftArmController;
    private TitanRightArmRoleController _rightArmController;
    private TitanLeftLegRoleController _leftLegController;
    private TitanRightLegRoleController _rightLegController;
    private TitanStat _titanStat;
    private TitanController _titanController;

    private bool _appliedClientPhysicsMode;

    private void Awake()
    {
        _torsoController = gameObject.GetOrAddComponent<TitanTorsoRoleController>();
        _legAnchorResolver = gameObject.GetOrAddComponent<TitanLegAnchorResolver>();
        _leftArmController = gameObject.GetOrAddComponent<TitanLeftArmRoleController>();
        _rightArmController = gameObject.GetOrAddComponent<TitanRightArmRoleController>();
        _leftLegController = gameObject.GetOrAddComponent<TitanLeftLegRoleController>();
        _rightLegController = gameObject.GetOrAddComponent<TitanRightLegRoleController>();
        _titanStat = gameObject.GetOrAddComponent<TitanStat>();
        _titanController = gameObject.GetOrAddComponent<TitanController>();
    }

    private void FixedUpdate()
    {
        if (ShouldApplyServerPoseOnly())
        {
            ApplyClientPhysicsMode();
            ApplyLatestServerPose();
            ApplyLatestServerGauge();
            ApplyLatestServerAbilityState();
            return;
        }

        RestoreServerPhysicsMode();

        _shouldLogThisFrame = InputDebug.Enabled && Time.unscaledTime >= _nextDebugLogTime;
        if (_shouldLogThisFrame)
            _nextDebugLogTime = Time.unscaledTime + DebugLogIntervalSeconds;

        float dt = Time.fixedDeltaTime;

        TitanAggregatedInput leftLegInput = default;
        TitanAggregatedInput rightLegInput = default;
        bool hasLeftLegInput = TryGetLegRoleInput(true, out leftLegInput);
        bool hasRightLegInput = TryGetLegRoleInput(false, out rightLegInput);

        ApplyLegAttachInput(true, in leftLegInput, hasLeftLegInput);
        ApplyLegAttachInput(false, in rightLegInput, hasRightLegInput);

        TickTorsoRole(dt);
        TickArmRole(true, dt);
        TickArmRole(false, dt);
        TickLegRole(true, hasLeftLegInput, in leftLegInput, dt);
        TickLegRole(false, hasRightLegInput, in rightLegInput, dt);
        TickPassiveStabilization(hasLeftLegInput, hasRightLegInput, dt);
        TickClawWire(dt);

        PublishAuthoritativePose();
        PublishAuthoritativeGauge();
        PublishAuthoritativeAbilityState();
    }

    private static bool ShouldApplyServerPoseOnly()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null
            && networkManager.IsListening
            && networkManager.IsClient
            && !networkManager.IsServer;
    }

    private void ApplyClientPhysicsMode()
    {
        if (_appliedClientPhysicsMode)
            return;

        Rigidbody movementRigidbody = Managers.TitanRig.MovementRigidbody;
        if (movementRigidbody != null)
        {
            movementRigidbody.linearVelocity = Vector3.zero;
            movementRigidbody.angularVelocity = Vector3.zero;
            movementRigidbody.isKinematic = true;
        }

        _appliedClientPhysicsMode = true;
    }

    private void RestoreServerPhysicsMode()
    {
        if (!_appliedClientPhysicsMode)
            return;

        Rigidbody movementRigidbody = Managers.TitanRig.MovementRigidbody;
        if (movementRigidbody != null)
            movementRigidbody.isKinematic = false;

        _appliedClientPhysicsMode = false;
    }

    private static void ApplyLatestServerPose()
    {
        if (!LobbyNetworkPlayer.TryGetLatestTitanPose(out TitanRigPosePayload posePayload))
            return;

        Managers.TitanRig.ApplyPoseSnapshot(posePayload.ToSnapshot());
    }

    private void ApplyLatestServerGauge()
    {
        if (_titanStat == null)
            return;

        if (!LobbyNetworkPlayer.TryGetLatestTitanGauge(out int gauge))
            return;

        _titanStat.Gauge = gauge;
    }

    private void ApplyLatestServerAbilityState()
    {
        if (_titanController == null)
            return;

        if (!LobbyNetworkPlayer.TryGetLatestTitanAbilityState(out TitanAbilityStatePayload abilityState))
            return;

        abilityState.ApplyTo(_titanController);
    }

    private static void PublishAuthoritativePose()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (!Managers.TitanRig.TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot))
            return;

        LobbyNetworkPlayer.TryPublishServerTitanPose(new TitanRigPosePayload(snapshot));
    }

    private void PublishAuthoritativeGauge()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (_titanStat == null)
            return;

        LobbyNetworkPlayer.TryPublishServerTitanGauge(_titanStat.Gauge);
    }

    private void PublishAuthoritativeAbilityState()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (_titanController == null)
            return;

        LobbyNetworkPlayer.TryPublishServerTitanAbilityState(new TitanAbilityStatePayload(_titanController));
    }

    private void TickClawWire(float dt)
    {
        if (_titanController == null || _titanController.RightClawWire == null)
            return;

        _titanController.RightClawWire.TickServer(dt);
    }

    private void TickTorsoRole(float dt)
    {
        if (_torsoController == null)
            return;

        bool anchorActive = _legAnchorResolver != null && _legAnchorResolver.HasAnyAttachedFoot();
        _torsoController.SetAnchorPhysicsOverride(anchorActive);
        Managers.TitanRole.TryGetRoleInput(Define.TitanRole.Torso, out TitanAggregatedInput input);
        _torsoController.SetInputEnabled(true);
        _torsoController.TickRoleInput(input, dt);
        _torsoController.TickPhysics(dt);
    }

    private void TickArmRole(bool left, float dt)
    {
        TitanBaseArmRoleController controller = left ? _leftArmController : _rightArmController;
        if (controller == null)
            return;

        Define.TitanRole role = left ? Define.TitanRole.LeftArm : Define.TitanRole.RightArm;
        bool ok = Managers.TitanRole.TryGetRoleInput(role, out TitanAggregatedInput input);

        if (ok)
        {
            controller.TickRoleInput(input, dt);
        }
    }

    private bool TryGetLegRoleInput(bool left, out TitanAggregatedInput input)
    {
        Define.TitanRole role = left ? Define.TitanRole.LeftLeg : Define.TitanRole.RightLeg;
        return Managers.TitanRole.TryGetRoleInput(role, out input);
    }

    private void ApplyLegAttachInput(bool left, in TitanAggregatedInput activeInput, bool hasActiveInput)
    {
        TitanBaseLegRoleController controller = left ? _leftLegController : _rightLegController;
        if (controller == null)
        {
            return;
        }

        if (hasActiveInput)
        {
            controller.TickAttachInput(activeInput);
            return;
        }

        controller.TickAttachInput(default);
    }

    private void TickLegRole(bool left, bool hasInput, in TitanAggregatedInput input, float dt)
    {
        TitanBaseLegRoleController controller = left ? _leftLegController : _rightLegController;
        if (controller == null)
        {
            return;
        }

        if (hasInput)
        {
            controller.TickRoleInput(input, dt);
        }
    }

    private void TickPassiveStabilization(bool hasLeftLegInput, bool hasRightLegInput, float dt)
    {
        if (_leftArmController != null && !Managers.TitanRole.TryGetRoleInput(Define.TitanRole.LeftArm, out _))
            _leftArmController.TickIdle(dt);

        if (_rightArmController != null && !Managers.TitanRole.TryGetRoleInput(Define.TitanRole.RightArm, out _))
            _rightArmController.TickIdle(dt);

        if (_leftLegController != null && !hasLeftLegInput)
            _leftLegController.TickIdle(dt);

        if (_rightLegController != null && !hasRightLegInput)
            _rightLegController.TickIdle(dt);
    }

}
