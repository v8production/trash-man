using UnityEngine;

public class TitanRoleNetworkDriver : MonoBehaviour
{
    private float _nextDebugLogTime;
    private const float DebugLogIntervalSeconds = 0.50f;
    private bool _shouldLogThisFrame;
    [Header("Role Controllers")]
    [SerializeField] private TitanBodyRoleController _bodyController;
    [SerializeField] private TitanLeftArmRoleController _leftArmController;
    [SerializeField] private TitanRightArmRoleController _rightArmController;
    [SerializeField] private TitanLeftLegRoleController _leftLegController;
    [SerializeField] private TitanRightLegRoleController _rightLegController;

    private void Awake()
    {
        ResolveControllers();
    }

    private void FixedUpdate()
    {
        ResolveControllers();

        if (_bodyController == null && _leftArmController == null && _rightArmController == null && _leftLegController == null && _rightLegController == null)
            return;

        _shouldLogThisFrame = InputDebug.Enabled && Time.unscaledTime >= _nextDebugLogTime;
        if (_shouldLogThisFrame)
            _nextDebugLogTime = Time.unscaledTime + DebugLogIntervalSeconds;

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

        bool ok = Managers.TitanRole.TryGetRoleInput(Define.TitanRole.Body, out TitanAggregatedInput input);
        _bodyController.SetInputEnabled(true);
        _bodyController.TickRoleInput(input, dt);
        _bodyController.TickPhysics(dt);
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

    private void TickLegRole(bool left, float dt)
    {
        TitanBaseLegRoleController controller = left ? _leftLegController : _rightLegController;
        if (controller == null)
            return;

        Define.TitanRole role = left ? Define.TitanRole.LeftLeg : Define.TitanRole.RightLeg;
        bool ok = Managers.TitanRole.TryGetRoleInput(role, out TitanAggregatedInput input);

        if (ok)
        {
            controller.TickRoleInput(input, dt);
        }
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
