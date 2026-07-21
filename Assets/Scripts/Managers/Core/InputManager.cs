using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager
{
    private InputActionAsset _asset;
    private string _playerMapName = "Player";
    private string _uiMapName = "UI";
    private const string LookActionName = "Look";
    private const string ScrollWheelActionName = "ScrollWheel";
    private bool _hasVirtualMousePosition;
    private Vector2 _virtualMousePosition;
    private Vector2Int _virtualMouseScreenSize;
    private CursorLockMode _lastCursorLockMode;
    private uint _transientInputSequence;

    public Define.InputMode Mode { get; private set; } = Define.InputMode.Player;

    public InputActionMap PlayerMap;
    public InputActionMap UIMap;

    private InputAction _lookAction;
    private InputAction _scrollWheelAction;

    public void Init()
    {
        _asset = Managers.Resource.Load<InputActionAsset>("InputSystem_Actions");
        PlayerMap = _asset.FindActionMap(_playerMapName, throwIfNotFound: true);
        UIMap = _asset.FindActionMap(_uiMapName, throwIfNotFound: true);
        _lookAction = PlayerMap.FindAction(LookActionName, throwIfNotFound: false);
        _scrollWheelAction = UIMap.FindAction(ScrollWheelActionName, throwIfNotFound: false);
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

    public TitanAggregatedInput CaptureTitanInput()
    {
        TitanAggregatedInput input = default;
        if (Mode != Define.InputMode.Player)
            return input;

        input.TorsoDrillPressedThisFrame = WasRightMousePressedThisFrame();
        input.TorsoShieldPressedThisFrame = WasPressedThisFrame(Key.Space);
        input.TorsoClawPressedThisFrame = WasLeftMousePressedThisFrame();
        input.TorsoShieldHeld = IsPressed(Key.Space);
        input.TorsoYawInput = GetAxis(Key.A, Key.D);
        input.MouseDelta = ReadTitanMouseDelta();
        input.TorsoCameraScrollInput = ReadMouseScrollY();
        input.ArmElbowInput = GetAxis(Key.W, Key.S);
        input.LegScrollInput = Mathf.Abs(input.TorsoCameraScrollInput);
        if (input.MouseDelta.sqrMagnitude > 0.0001f || !Mathf.Approximately(input.TorsoCameraScrollInput, 0f))
            input.TransientInputSequence = ++_transientInputSequence;

        return input;
    }

    public Vector2 ReadTitanMouseDelta()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return Vector2.zero;

        return mouse.delta.ReadValue();
    }

    public Vector2 ReadPlayerLookInput()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        if (_lookAction != null)
            return _lookAction.ReadValue<Vector2>();

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return Vector2.zero;

        return mouse.delta.ReadValue();
    }

    public float ReadMouseScrollY()
    {
        if (Mode == Define.InputMode.Cinematic)
            return 0f;

        if (_scrollWheelAction != null)
        {
            float actionScrollY = _scrollWheelAction.ReadValue<Vector2>().y;
            if (!Mathf.Approximately(actionScrollY, 0f))
                return actionScrollY;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return 0f;

        return mouse.scroll.ReadValue().y;
    }

    public bool WasLeftMousePressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    public bool WasRightMousePressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.wasPressedThisFrame;
    }

    public bool WasInteractKeyPressedThisFrame()
    {
        if (Mode != Define.InputMode.Player)
            return false;

        return WasPressedThisFrame(Key.E);
    }

    private bool IsShiftHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    public Vector2 ReadMousePosition()
    {
        if (Mode != Define.InputMode.Player)
            return Vector2.zero;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return Vector2.zero;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            _hasVirtualMousePosition = false;
            _lastCursorLockMode = Cursor.lockState;
            return mouse.position.ReadValue();
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

        _virtualMousePosition += mouse.delta.ReadValue();
        _virtualMousePosition.x = Mathf.Clamp(_virtualMousePosition.x, 0f, screenSize.x);
        _virtualMousePosition.y = Mathf.Clamp(_virtualMousePosition.y, 0f, screenSize.y);
        _lastCursorLockMode = CursorLockMode.Locked;
        return _virtualMousePosition;
    }

    public float GetAxis(Key positive, Key negative)
    {
        if (Mode != Define.InputMode.Player)
            return 0f;

        float axis = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return 0f;

        if (keyboard[positive].isPressed)
            axis += 1f;

        if (keyboard[negative].isPressed)
            axis -= 1f;

        return axis;
    }

    public bool WasDigitPressedThisFrame(int digitOneToFive)
    {
        if (Mode != Define.InputMode.Player)
            return false;

        switch (digitOneToFive)
        {
            case 1:
                return WasPressedThisFrame(Key.Digit1);
            case 2:
                return WasPressedThisFrame(Key.Digit2);
            case 3:
                return WasPressedThisFrame(Key.Digit3);
            case 4:
                return WasPressedThisFrame(Key.Digit4);
            case 5:
                return WasPressedThisFrame(Key.Digit5);
            default:
                return false;
        }
    }

    private bool WasPressedThisFrame(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }

    private bool IsPressed(Key key)
    {
        return Keyboard.current != null && Keyboard.current[key].isPressed;
    }

}
