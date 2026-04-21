using UnityEngine;

public class TitanRoleNetworkDriver : MonoBehaviour
{
    [Header("Role Controllers")]
    [SerializeField] private TitanBodyRoleController _bodyController;
    [SerializeField] private TitanLeftArmRoleController _leftArmController;
    [SerializeField] private TitanRightArmRoleController _rightArmController;
    [SerializeField] private TitanLeftLegRoleController _leftLegController;
    [SerializeField] private TitanRightLegRoleController _rightLegController;

    private LobbyNetworkPlayer _localPlayer;

    private void Awake()
    {
        ResolveControllers();
    }

    private void FixedUpdate()
    {
        ResolveControllers();

        if (_bodyController == null && _leftArmController == null && _rightArmController == null && _leftLegController == null && _rightLegController == null)
            return;

        _localPlayer ??= LobbyNetworkPlayer.FindLocalOwnedPlayer();
        _localPlayer?.PublishLocalRoleInput();

        float dt = Time.fixedDeltaTime;

        TickBodyRole(dt);
        TickArmRole(true, dt);
        TickArmRole(false, dt);
        TickLegRole(true, dt);
        TickLegRole(false, dt);
    }

    private void TickBodyRole(float dt)
    {
        if (_bodyController == null)
            return;

        if (Managers.TitanRole.TryGetRoleInput(Define.TitanRole.Body, out TitanAggregatedInput input))
            TitanBaseController.SetSharedInput(input);
        else
            TitanBaseController.SetSharedInput(default);

        _bodyController.SetInputEnabled(true);
        _bodyController.TickRoleInput(dt);
        _bodyController.TickPhysics(dt);
    }

    private void TickArmRole(bool left, float dt)
    {
        TitanBaseArmRoleController controller = left ? _leftArmController : _rightArmController;
        if (controller == null)
            return;

        Define.TitanRole role = left ? Define.TitanRole.LeftArm : Define.TitanRole.RightArm;
        if (Managers.TitanRole.TryGetRoleInput(role, out TitanAggregatedInput input))
            TitanBaseController.SetSharedInput(input);
        else
            TitanBaseController.SetSharedInput(default);

        controller.TickRoleInput(dt);
    }

    private void TickLegRole(bool left, float dt)
    {
        TitanBaseLegRoleController controller = left ? _leftLegController : _rightLegController;
        if (controller == null)
            return;

        Define.TitanRole role = left ? Define.TitanRole.LeftLeg : Define.TitanRole.RightLeg;
        if (Managers.TitanRole.TryGetRoleInput(role, out TitanAggregatedInput input))
            TitanBaseController.SetSharedInput(input);
        else
            TitanBaseController.SetSharedInput(default);

        controller.TickRoleInput(dt);
    }

    private void ResolveControllers()
    {
        _bodyController ??= GetComponent<TitanBodyRoleController>();
        _leftArmController ??= GetComponent<TitanLeftArmRoleController>();
        _rightArmController ??= GetComponent<TitanRightArmRoleController>();
        _leftLegController ??= GetComponent<TitanLeftLegRoleController>();
        _rightLegController ??= GetComponent<TitanRightLegRoleController>();
    }

}
