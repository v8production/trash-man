using Unity.VisualScripting;
using Unity.Netcode;
using UnityEngine;

public class TitanController : MonoBehaviour
{

    [Header("References")]
    private TitanRigRuntime rigRuntime;
    private TitanTorsoRoleController torsoController;
    private TitanLeftArmRoleController leftArmController;
    private TitanRightArmRoleController rightArmController;
    private TitanLeftLegRoleController leftLegController;
    private TitanRightLegRoleController rightLegController;
    private TitanDrillController leftDrillController;
    private TitanClawWireController rightClawWireController;
    private TitanMovementFeedbackController movementFeedbackController;

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
    public TitanMovementFeedbackController MovementFeedback => movementFeedbackController;

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
        torsoController = gameObject.GetOrAddComponent<TitanTorsoRoleController>();
        leftArmController = gameObject.GetOrAddComponent<TitanLeftArmRoleController>();
        rightArmController = gameObject.GetOrAddComponent<TitanRightArmRoleController>();
        leftLegController = gameObject.GetOrAddComponent<TitanLeftLegRoleController>();
        rightLegController = gameObject.GetOrAddComponent<TitanRightLegRoleController>();
        leftDrillController = gameObject.GetOrAddComponent<TitanDrillController>();
        rightClawWireController = gameObject.GetOrAddComponent<TitanClawWireController>();
        movementFeedbackController = gameObject.GetOrAddComponent<TitanMovementFeedbackController>();
        rigRuntime.FootGrounded -= HandleFootGrounded;
        rigRuntime.FootGrounded += HandleFootGrounded;
        SetShieldVisualActive(_guard);
    }

    private void OnDestroy()
    {
        if (rigRuntime != null)
            rigRuntime.FootGrounded -= HandleFootGrounded;
    }

    private void HandleFootGrounded(bool _)
    {
        if (IsNetworkSessionActive())
        {
            if (HasServerAuthority() && LobbyNetworkPlayer.TryPublishServerFootGroundedFeedback())
                return;

            if (!HasServerAuthority())
                return;
        }

        movementFeedbackController.PlayFootGroundedFeedback();
    }

    private static bool IsNetworkSessionActive()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening;
    }

    private static bool HasServerAuthority()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening && networkManager.IsServer;
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
