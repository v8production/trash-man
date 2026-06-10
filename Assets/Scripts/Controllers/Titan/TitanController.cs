using Unity.VisualScripting;
using UnityEngine;

public class TitanController : MonoBehaviour
{

    [Header("References")]
    private TitanRigRuntime rigRuntime;
    private TitanLegAnchorResolver legAnchorResolver;
    private TitanTorsoRoleController torsoController;
    private TitanLeftArmRoleController leftArmController;
    private TitanRightArmRoleController rightArmController;
    private TitanLeftLegRoleController leftLegController;
    private TitanRightLegRoleController rightLegController;
    private TitanDrillController leftDrillController;
    private TitanClawWireController rightClawWireController;

    [Header("Shield Visual")]
    [SerializeField] private string shieldPrefabName = "Shield";
    [SerializeField] private Vector3 shieldLocalPosition = new(0f, 0.5f, 0f);
    [SerializeField] private Vector3 shieldLocalEulerAngles;
    [SerializeField] private Vector3 shieldLocalScale = new(1f, 1.2f, 1f);

    TitanStat _stat;
    public TitanStat Stat { get { return _stat; } }

    bool _guard;
    bool _leftDrillActive;
    int _rightClawLaunchCount;
    GameObject shieldVisual;

    public bool Guard
    {
        get { return _guard; }
        set
        {
            _guard = value;
            SetShieldVisualActive(_guard);
        }
    }
    public bool LeftDrillActive { get { return _leftDrillActive; } set { _leftDrillActive = value; } }
    public int RightClawLaunchCount { get { return _rightClawLaunchCount; } }
    public TitanClawWireController RightClawWire => rightClawWireController;

    public bool CanLaunchRightClaw => rightClawWireController.CanLaunch;

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
        rigRuntime = gameObject.GetOrAddComponent<TitanRigRuntime>();
        Managers.TitanRig.Bind(rigRuntime);

        _stat = gameObject.GetOrAddComponent<TitanStat>();
        legAnchorResolver = gameObject.GetOrAddComponent<TitanLegAnchorResolver>();
        torsoController = gameObject.GetOrAddComponent<TitanTorsoRoleController>();
        leftArmController = gameObject.GetOrAddComponent<TitanLeftArmRoleController>();
        rightArmController = gameObject.GetOrAddComponent<TitanRightArmRoleController>();
        leftLegController = gameObject.GetOrAddComponent<TitanLeftLegRoleController>();
        rightLegController = gameObject.GetOrAddComponent<TitanRightLegRoleController>();
        leftDrillController = gameObject.GetOrAddComponent<TitanDrillController>();
        rightClawWireController = gameObject.GetOrAddComponent<TitanClawWireController>();
        SetShieldVisualActive(_guard);
    }

    private void SetShieldVisualActive(bool active)
    {
        if (active)
        {
            if (shieldVisual == null)
            {
                shieldVisual = Managers.Resource.Instantiate(shieldPrefabName, transform);
                shieldVisual.transform.localPosition = shieldLocalPosition;
                shieldVisual.transform.localRotation = Quaternion.Euler(shieldLocalEulerAngles);
                shieldVisual.transform.localScale = shieldLocalScale;
            }

            return;
        }

        if (shieldVisual != null)
        {
            Managers.Resource.Destory(shieldVisual);
            shieldVisual = null;
        }
    }
}
