using UnityEngine;

public class TitanController : MonoBehaviour
{
    [Header("Auto Attach")]
    [SerializeField] private bool attachControllersOnAwake = true;

    [Header("References")]
    [SerializeField] private TitanRigRuntime rigRuntime;
    [SerializeField] private TitanLegAnchorResolver legAnchorResolver;
    [SerializeField] private TitanTorsoRoleController torsoController;
    [SerializeField] private TitanLeftArmRoleController leftArmController;
    [SerializeField] private TitanRightArmRoleController rightArmController;
    [SerializeField] private TitanLeftLegRoleController leftLegController;
    [SerializeField] private TitanRightLegRoleController rightLegController;
    [SerializeField] private TitanDrillController leftDrillController;
    [SerializeField] private TitanClawWireController rightClawWireController;

    TitanStat _stat;
    public TitanStat Stat { get { return _stat; } }

    bool _guard;
    bool _leftDrillActive;
    int _rightClawLaunchCount;

    public bool Guard { get { return _guard; } set { _guard = value; } }
    public bool LeftDrillActive { get { return _leftDrillActive; } set { _leftDrillActive = value; } }
    public int RightClawLaunchCount { get { return _rightClawLaunchCount; } }
    public TitanClawWireController RightClawWire => rightClawWireController;

    public bool CanLaunchRightClaw => rightClawWireController != null && rightClawWireController.CanLaunch;

    public void NotifyRightClawLaunched()
    {
        _rightClawLaunchCount++;
        rightClawWireController.TryLaunch(_stat);
    }

    public void SetRightClawLaunchCount(int value)
    {
        _rightClawLaunchCount = Mathf.Max(0, value);
    }

    private void Awake()
    {
        if (attachControllersOnAwake)
        {
            EnsureInitialized();
        }
        _stat = gameObject.GetComponent<TitanStat>();
        leftDrillController ??= GetComponent<TitanDrillController>();
        leftDrillController ??= gameObject.AddComponent<TitanDrillController>();
        rightClawWireController ??= GetComponent<TitanClawWireController>();
        rightClawWireController ??= gameObject.AddComponent<TitanClawWireController>();
    }

    public void EnsureInitialized()
    {
        rigRuntime = RequireComponent(rigRuntime);
        if (rigRuntime != null)
            Managers.TitanRig.Bind(rigRuntime);

        legAnchorResolver = RequireComponent(legAnchorResolver);
        torsoController = RequireComponent(torsoController);
        leftArmController = RequireComponent(leftArmController);
        rightArmController = RequireComponent(rightArmController);
        leftLegController = RequireComponent(leftLegController);
        rightLegController = RequireComponent(rightLegController);
        leftDrillController = RequireOrAddComponent(leftDrillController);
        rightClawWireController = RequireOrAddComponent(rightClawWireController);
    }

    private T RequireOrAddComponent<T>(T existing) where T : Component
    {
        if (existing != null)
            return existing;

        T found = GetComponent<T>();
        if (found != null)
            return found;

        return gameObject.AddComponent<T>();
    }

    private T RequireComponent<T>(T existing) where T : Component
    {
        if (existing != null)
        {
            return existing;
        }

        T found = GetComponent<T>();
        if (found != null)
        {
            return found;
        }

        Debug.LogError($"[TitanController] Missing required component '{typeof(T).Name}' on '{gameObject.name}'. Add it to the Titan prefab.", this);
        return null;
    }
}
