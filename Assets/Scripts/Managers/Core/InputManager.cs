using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager
{
    private InputActionAsset _asset;
    private string _playerMapName = "Player";
    private string _uiMapName = "UI";
    private const string MoveActionName = "Move";
    private const string LookActionName = "Look";
    private const string RangerMoveActionName = "RangerMove";
    private const string RangerLookActionName = "RangerLook";
    private const string TorsoYawActionName = "TorsoYaw";
    private const string TorsoLookActionName = "TorsoLook";
    private const string ArmElbowActionName = "ArmElbow";
    private const string ArmAimActionName = "ArmAim";
    private const string LegAimActionName = "LegAim";
    private const string LegScrollActionName = "LegScroll";
    private const string AttackActionName = "Attack";
    private const string InteractActionName = "Interact";
    private const string DrillActionName = "Drill";
    private const string ShieldActionName = "Shield";
    private const string RoleActionPrefix = "Role";
    private const string CancelActionName = "Cancel";
    private const string PointActionName = "Point";
    private const string ClickActionName = "Click";
    private const string RightClickActionName = "RightClick";
    private const string ScrollWheelActionName = "ScrollWheel";
    private const string RoomCodeRevealActionName = "RoomCodeReveal";
    private bool _hasVirtualMousePosition;
    private Vector2 _virtualMousePosition;
    private Vector2Int _virtualMouseScreenSize;
    private CursorLockMode _lastCursorLockMode;
    private uint _transientInputSequence;

    public Define.InputMode Mode { get; private set; } = Define.InputMode.Player;

    public InputActionMap PlayerMap;
    public InputActionMap UIMap;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _rangerMoveAction;
    private InputAction _rangerLookAction;
    private InputAction _torsoYawAction;
    private InputAction _torsoLookAction;
    private InputAction _armElbowAction;
    private InputAction _armAimAction;
    private InputAction _legAimAction;
    private InputAction _legScrollAction;
    private InputAction _attackAction;
    private InputAction _interactAction;
    private InputAction _drillAction;
    private InputAction _shieldAction;
    private InputAction[] _roleActions;
    private InputAction _cancelAction;
    private InputAction _pointAction;
    private InputAction _clickAction;
    private InputAction _rightClickAction;
    private InputAction _scrollWheelAction;
    private InputAction _roomCodeRevealAction;

    public void Init()
    {
        _asset = Managers.Resource.Load<InputActionAsset>("InputSystem_Actions");
        PlayerMap = _asset.FindActionMap(_playerMapName, throwIfNotFound: true);
        UIMap = _asset.FindActionMap(_uiMapName, throwIfNotFound: true);
        _moveAction = PlayerMap.FindAction(MoveActionName, throwIfNotFound: true);
        _lookAction = PlayerMap.FindAction(LookActionName, throwIfNotFound: true);
        _rangerMoveAction = PlayerMap.FindAction(RangerMoveActionName, throwIfNotFound: true);
        _rangerLookAction = PlayerMap.FindAction(RangerLookActionName, throwIfNotFound: true);
        _torsoYawAction = PlayerMap.FindAction(TorsoYawActionName, throwIfNotFound: true);
        _torsoLookAction = PlayerMap.FindAction(TorsoLookActionName, throwIfNotFound: true);
        _armElbowAction = PlayerMap.FindAction(ArmElbowActionName, throwIfNotFound: true);
        _armAimAction = PlayerMap.FindAction(ArmAimActionName, throwIfNotFound: true);
        _legAimAction = PlayerMap.FindAction(LegAimActionName, throwIfNotFound: true);
        _legScrollAction = PlayerMap.FindAction(LegScrollActionName, throwIfNotFound: true);
        _attackAction = PlayerMap.FindAction(AttackActionName, throwIfNotFound: true);
        _interactAction = PlayerMap.FindAction(InteractActionName, throwIfNotFound: true);
        _drillAction = PlayerMap.FindAction(DrillActionName, throwIfNotFound: true);
        _shieldAction = PlayerMap.FindAction(ShieldActionName, throwIfNotFound: true);
        _roleActions = new InputAction[5];
        for (int i = 0; i < _roleActions.Length; i++)
            _roleActions[i] = PlayerMap.FindAction($"{RoleActionPrefix}{i + 1}", throwIfNotFound: true);

        _cancelAction = UIMap.FindAction(CancelActionName, throwIfNotFound: true);
        _pointAction = UIMap.FindAction(PointActionName, throwIfNotFound: true);
        _clickAction = UIMap.FindAction(ClickActionName, throwIfNotFound: true);
        _rightClickAction = UIMap.FindAction(RightClickActionName, throwIfNotFound: true);
        _scrollWheelAction = UIMap.FindAction(ScrollWheelActionName, throwIfNotFound: true);
        _roomCodeRevealAction = UIMap.FindAction(RoomCodeRevealActionName, throwIfNotFound: true);
        SetMode(Define.InputMode.UI);
    }

    public void SetMode(Define.InputMode mode)
    {
        Mode = mode;

        // Disable everything first (clean slate)
        PlayerMap.Disable();
        UIMap.Disable();

        switch (mode)
        {
            case Define.InputMode.Player:
                PlayerMap.Enable();
                // Keep UI map enabled so the first UI interaction after a mode switch
                // (e.g., opening a menu then clicking immediately) is not dropped.
                UIMap.Enable();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;

            case Define.InputMode.UI:
                UIMap.Enable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;

            case Define.InputMode.Cinematic:
                // none enabled
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
        }
    }

    public TitanAggregatedInput CaptureTitanInput(Define.TitanRole activeRole)
    {
        TitanAggregatedInput input = default;
        if (Mode != Define.InputMode.Player)
            return input;

        switch (activeRole)
        {
            case Define.TitanRole.Torso:
                input.TorsoDrillPressedThisFrame = WasDrillPressedThisFrame();
                input.TorsoShieldPressedThisFrame = WasShieldPressedThisFrame();
                input.TorsoClawPressedThisFrame = WasLeftMousePressedThisFrame();
                input.TorsoShieldHeld = IsShieldHeld();
                input.TorsoYawInput = ReadTorsoYawInput();
                input.MouseDelta = ReadTorsoLookInput();
                input.TorsoCameraScrollInput = ReadMouseScrollY();
                break;
            case Define.TitanRole.LeftArm:
            case Define.TitanRole.RightArm:
                input.ArmElbowInput = ReadArmElbowInput();
                input.MouseDelta = ReadArmAimInput();
                break;
            case Define.TitanRole.LeftLeg:
            case Define.TitanRole.RightLeg:
                input.MouseDelta = ReadLegAimInput();
                input.LegScrollInput = Mathf.Abs(ReadLegScrollY());
                break;
        }

        if (input.MouseDelta.sqrMagnitude > 0.0001f
            || !Mathf.Approximately(input.TorsoCameraScrollInput, 0f)
            || !Mathf.Approximately(input.LegScrollInput, 0f))
            input.TransientInputSequence = ++_transientInputSequence;

        return input;
    }

    public Vector2 ReadMoveInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _moveAction.ReadValue<Vector2>();
    }

    public Vector2 ReadRangerMoveInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _rangerMoveAction.ReadValue<Vector2>();
    }

    public Vector2 ReadRangerLookInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _rangerLookAction.ReadValue<Vector2>();
    }

    public float ReadTorsoYawInput()
    {
        if (Mode != Define.InputMode.Player)
            return 0f;

        return _torsoYawAction.ReadValue<float>();
    }

    public Vector2 ReadTorsoLookInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _torsoLookAction.ReadValue<Vector2>();
    }

    public float ReadArmElbowInput()
    {
        if (Mode != Define.InputMode.Player)
            return 0f;

        return _armElbowAction.ReadValue<float>();
    }

    public Vector2 ReadArmAimInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _armAimAction.ReadValue<Vector2>();
    }

    public Vector2 ReadLegAimInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _legAimAction.ReadValue<Vector2>();
    }

    public float ReadLegScrollY()
    {
        if (Mode != Define.InputMode.Player)
            return 0f;

        return _legScrollAction.ReadValue<Vector2>().y;
    }

    public Vector2 ReadTitanMouseDelta()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return ReadTorsoLookInput();
    }

    public Vector2 ReadPlayerLookInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        return _lookAction.ReadValue<Vector2>();
    }

    public float ReadMouseScrollY()
    {
        if (Mode == Define.InputMode.Cinematic)
            return 0f;

        return _scrollWheelAction.ReadValue<Vector2>().y;
    }

    public bool WasLeftMousePressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _attackAction.WasPressedThisFrame() || _clickAction.WasPressedThisFrame();
    }

    public bool WasRightMousePressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _drillAction.WasPressedThisFrame() || _rightClickAction.WasPressedThisFrame();
    }

    public bool WasDrillPressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _drillAction.WasPressedThisFrame();
    }

    public bool WasShieldPressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _shieldAction.WasPressedThisFrame();
    }

    public bool IsShieldHeld()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _shieldAction.IsPressed();
    }

    public bool WasInteractKeyPressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return _interactAction.WasPressedThisFrame();
    }

    public bool WasEscapePressedThisFrame()
    {
        if (Mode == Define.InputMode.Cinematic)
            return false;

        return _cancelAction.WasPressedThisFrame();
    }

    public bool IsRoomCodeRevealHeld()
    {
        if (Mode == Define.InputMode.Cinematic)
            return false;

        return _roomCodeRevealAction.IsPressed();
    }

    public Vector2 ReadMousePosition()
    {
        if (Mode == Define.InputMode.Cinematic)
            return Vector2.zero;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _hasVirtualMousePosition = false;
            _lastCursorLockMode = Cursor.lockState;
            return _pointAction.ReadValue<Vector2>();
        }

        Vector2Int screenSize = new(Screen.width, Screen.height);
        if (!_hasVirtualMousePosition
            || _virtualMouseScreenSize != screenSize
            || _lastCursorLockMode != CursorLockMode.Locked)
        {
            _virtualMousePosition = new Vector2(screenSize.x * 0.5f, screenSize.y * 0.5f);
            _virtualMouseScreenSize = screenSize;
            _hasVirtualMousePosition = true;
        }

        _virtualMousePosition += _lookAction.ReadValue<Vector2>();
        _virtualMousePosition.x = Mathf.Clamp(_virtualMousePosition.x, 0f, screenSize.x);
        _virtualMousePosition.y = Mathf.Clamp(_virtualMousePosition.y, 0f, screenSize.y);
        _lastCursorLockMode = CursorLockMode.Locked;
        return _virtualMousePosition;
    }

    public bool WasDigitPressedThisFrame(int digitOneToFive)
    {
        if (Mode != Define.InputMode.Player)
            return false;

        if (digitOneToFive < 1 || digitOneToFive > _roleActions.Length)
            return false;

        return _roleActions[digitOneToFive - 1].WasPressedThisFrame();
    }
}
