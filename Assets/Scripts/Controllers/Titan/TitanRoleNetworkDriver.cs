using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class TitanRoleNetworkDriver : MonoBehaviour
{
    private float _nextDebugLogTime;
    private const float DebugLogIntervalSeconds = 0.50f;
    private bool _shouldLogThisFrame;
    private TitanTorsoRoleController _torsoController;
    private TitanLeftArmRoleController _leftArmController;
    private TitanRightArmRoleController _rightArmController;
    private TitanLeftLegRoleController _leftLegController;
    private TitanRightLegRoleController _rightLegController;
    private TitanStat _titanStat;
    private TitanController _titanController;
    private TitanMovementFeedbackController _movementFeedbackController;

    private bool _appliedClientPhysicsMode;

    private void Awake()
    {
        _torsoController = gameObject.GetOrAddComponent<TitanTorsoRoleController>();
        _leftArmController = gameObject.GetOrAddComponent<TitanLeftArmRoleController>();
        _rightArmController = gameObject.GetOrAddComponent<TitanRightArmRoleController>();
        _leftLegController = gameObject.GetOrAddComponent<TitanLeftLegRoleController>();
        _rightLegController = gameObject.GetOrAddComponent<TitanRightLegRoleController>();
        _titanStat = gameObject.GetOrAddComponent<TitanStat>();
        _titanController = gameObject.GetOrAddComponent<TitanController>();
        _movementFeedbackController = gameObject.GetOrAddComponent<TitanMovementFeedbackController>();
    }

    private void FixedUpdate()
    {
        if (ShouldApplyServerPoseOnly())
        {
            ApplyClientPhysicsMode();
            ApplyLatestServerPose();
            ApplyLatestServerStat();
            ApplyLatestServerAbilityState();
            return;
        }

        RestoreServerPhysicsMode();

        _shouldLogThisFrame = InputDebug.Enabled && Time.unscaledTime >= _nextDebugLogTime;
        if (_shouldLogThisFrame)
            _nextDebugLogTime = Time.unscaledTime + DebugLogIntervalSeconds;

        float dt = Time.fixedDeltaTime;

        // Root physics pose must be finalized before leg IK; changing root rotation after IK invalidates the solved pose.
        Managers.TitanRig.ApplyMovementRootBaseRotation();

        TickTorsoRole(dt);

        TickRole(_leftArmController, Define.TitanRole.LeftArm, dt);
        TickRole(_rightArmController, Define.TitanRole.RightArm, dt);
        TickLegRoles(dt);
        _movementFeedbackController.TickMotorAudio();
        TickClawWire(dt);

        PublishAuthoritativePose();
        PublishAuthoritativeStat();
        PublishAuthoritativeAbilityState();
    }

    private void LateUpdate()
    {
        if (ShouldApplyServerPoseOnly())
        {
            ApplyLatestServerPose();
            return;
        }

        Managers.TitanRig.ApplyTorsoPose();
        Managers.TitanRig.ApplyArmPose(true);
        Managers.TitanRig.ApplyArmPose(false);
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

        Managers.TitanRig.SetRemotePhysicsOverride(true);
        _appliedClientPhysicsMode = true;
    }

    private void RestoreServerPhysicsMode()
    {
        if (!_appliedClientPhysicsMode)
            return;

        Managers.TitanRig.SetRemotePhysicsOverride(false);
        _appliedClientPhysicsMode = false;
    }

    private static void ApplyLatestServerPose()
    {
        if (!LobbyNetworkPlayer.TryGetLatestTitanPose(out TitanRigPosePayload posePayload))
            return;

        Managers.TitanRig.ApplyPoseSnapshot(posePayload.ToSnapshot());
    }

    private void ApplyLatestServerStat()
    {
        if (_titanStat == null)
            return;

        if (LobbyNetworkPlayer.TryGetLatestTitanStat(out TitanStatPayload titanStat))
        {
            titanStat.ApplyTo(_titanStat);
            return;
        }

        if (LobbyNetworkPlayer.TryGetLatestTitanGauge(out int gauge))
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

    private void PublishAuthoritativeStat()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (_titanStat == null)
            return;

        LobbyNetworkPlayer.TryPublishServerTitanStat(new TitanStatPayload(_titanStat));
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

        Managers.TitanRole.TryGetRoleInput(Define.TitanRole.Torso, out TitanAggregatedInput input);
        _torsoController.TickRoleInput(input, dt);
    }

    private static void TickRole(TitanBaseController controller, Define.TitanRole role, float dt)
    {
        if (controller == null)
            return;

        Managers.TitanRole.TryGetRoleInput(role, out TitanAggregatedInput input);
        controller.TickRoleInput(input, dt);
    }

    private void TickLegRoles(float dt)
    {
        Managers.TitanRole.TryGetRoleInput(
            Define.TitanRole.LeftLeg,
            out TitanAggregatedInput leftInput);

        Managers.TitanRole.TryGetRoleInput(
            Define.TitanRole.RightLeg,
            out TitanAggregatedInput rightInput);

        _leftLegController?.TickRoleInput(leftInput, dt);
        _rightLegController?.TickRoleInput(rightInput, dt);

        TitanLegInputCommand leftCommand =
            _leftLegController != null
                ? _leftLegController.ConsumePendingCommand()
                : default;

        TitanLegInputCommand rightCommand =
            _rightLegController != null
                ? _rightLegController.ConsumePendingCommand()
                : default;

        Managers.TitanRig.TickLegSystem(
            leftCommand,
            rightCommand,
            dt);
    }

}
