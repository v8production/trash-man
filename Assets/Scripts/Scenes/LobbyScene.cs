using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LobbyScene : BaseScene
{
    private UI_LobbyMenu _lobbyMenu;
    private UI_RoleSelectMenu _roleSelectMenu;
    private UI_WhiteBoardMenu _whiteBoardMenu;
    private UI_Loading _loadingUi;
    private UI_MainScreen _mainScreen;
    private UI_KioskScreen _leftKioskScreen;
    private UI_KioskScreen _rightKioskScreen;
    private UI_WhiteBoard _whiteBoard;
    private bool _pendingHostBootstrap;
    private bool _isLobbySetupPending;
    private string _pendingJoinCode = string.Empty;
    private LobbyCameraController _localLobbyCamera;
    private const string LobbyCameraPrefabName = "Lobby_Camera";
    private const string MainScreenPrefabName = "UI_MainScreen";
    private const string KioskScreenPrefabName = "UI_KioskScreen";
    private const string WhiteBoardPrefabName = "UI_WhiteBoard";
    private const string MainScreenParentName = "ML_hall";
    private const string LeftKioskScreenParentName = "ML_roleconsoleL";
    private const string RightKioskScreenParentName = "ML_roleconsoleR";
    private const string WhiteBoardParentName = "ML_board";

    private static readonly Vector3 s_mainScreenLocalPosition = new(0f, 8.3f, 3f);
    private static readonly Quaternion s_mainScreenLocalRotation = Quaternion.Euler(-90f, 0f, 180f);
    private static readonly Vector3 s_screenWorldScale = new(0.01f, 0.01f, 0.01f);

    private static readonly Vector3 s_kioskScreenLocalPosition = new(0f, -0.2f, 1.45f);
    private static readonly Quaternion s_kioskScreenLocalRotation = Quaternion.Euler(-53f, 180f, 0f);

    private static readonly Vector3 s_whiteBoardLocalPosition = new(-0.1f, 0f, 1.93f);
    private static readonly Quaternion s_whiteBoardLocalRotation = Quaternion.Euler(0f, 90f, 90f);

    private static readonly Dictionary<string, LobbyUserEntry> s_userEntriesByUserId = new();

    private const float LobbyJoinTimeoutSeconds = 15f;
    private float _lobbySetupStartedAt;

    private sealed class LobbyUserEntry
    {
        public RangerController Ranger;
        public UI_Nickname Nickname;
        public int SelectedRoleMask;
    }

    public static void RegisterUserObjects(string userId, RangerController ranger, UI_Nickname nickname)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (!s_userEntriesByUserId.TryGetValue(userId, out LobbyUserEntry entry) || entry == null)
        {
            entry = new LobbyUserEntry();
            s_userEntriesByUserId[userId] = entry;
        }

        if (ranger != null)
            entry.Ranger = ranger;

        if (nickname != null)
            entry.Nickname = nickname;
    }

    public static void RegisterUserPartSelection(string userId, int selectedRoleMask)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (!s_userEntriesByUserId.TryGetValue(userId, out LobbyUserEntry entry) || entry == null)
        {
            entry = new LobbyUserEntry();
            s_userEntriesByUserId[userId] = entry;
        }

        entry.SelectedRoleMask = NormalizeRoleMask(selectedRoleMask);
    }

    public static bool TryGetRegisteredUserSelectedRoleMask(string userId, out int roleMask)
    {
        roleMask = 0;

        if (string.IsNullOrWhiteSpace(userId))
            return false;

        if (!s_userEntriesByUserId.TryGetValue(userId, out LobbyUserEntry entry) || entry == null)
            return false;

        roleMask = NormalizeRoleMask(entry.SelectedRoleMask);
        return roleMask != 0;
    }

    public static void ClearRegisteredUserPartSelections()
    {
        foreach (LobbyUserEntry entry in s_userEntriesByUserId.Values)
        {
            if (entry == null)
                continue;

            entry.SelectedRoleMask = 0;
        }
    }

    public static void UnregisterUserObjects(string userId, RangerController ranger, UI_Nickname nickname)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (!s_userEntriesByUserId.TryGetValue(userId, out LobbyUserEntry entry) || entry == null)
            return;

        if (ranger != null && entry.Ranger == ranger)
            entry.Ranger = null;

        if (nickname != null && entry.Nickname == nickname)
            entry.Nickname = null;

        if (entry.Ranger == null && entry.Nickname == null)
            s_userEntriesByUserId.Remove(userId);
    }

    public static void ClearUserObjectRegistry()
    {
        s_userEntriesByUserId.Clear();
    }

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Lobby;

        _ = Managers.Input;
        Managers.LobbySession.Init();

        Debug.Log("[Lobby] LobbyScene initialized.");
        Managers.Input.SetMode(Define.InputMode.Player);
        EnsureLobbyMenu();
        EnsureLoadingUI();
        EnsureWorldScreens();

        _pendingHostBootstrap = Managers.Scene.ConsumeLobbyHostRequest();
        _pendingJoinCode = Managers.Scene.ConsumeLobbyJoinCodeRequest(out string joinCode) ? joinCode : string.Empty;
        _isLobbySetupPending = _pendingHostBootstrap
            || !string.IsNullOrWhiteSpace(_pendingJoinCode)
            || Managers.LobbySession.HasPendingSteamLobbyJoin;

        if (_isLobbySetupPending)
        {
            _lobbySetupStartedAt = Time.unscaledTime;
            SetLobbyLoading(true, "Preparing lobby...");
        }

        ProcessPendingLobbyRequest();
    }

    private void Update()
    {
        EnsureLocalLobbyCameraReady();
        UpdateLobbyLoadingState();
        if (!IsEscapePressedThisFrame())
            return;

        if (_isLobbySetupPending)
            return;

        ToggleMenuInputMode();
    }

    private void OnDestroy()
    {

        if (_roleSelectMenu != null)
        {
            _roleSelectMenu.RoleSelected -= HandleRoleSelected;
            _roleSelectMenu.Closed -= HandleRoleSelectMenuClosed;
        }

        if (_whiteBoardMenu != null)
            _whiteBoardMenu.Closed -= HandleWhiteBoardMenuClosed;
    }

    private void ProcessPendingLobbyRequest()
    {
        if (_pendingHostBootstrap)
        {
            SetLobbyLoading(true, "Creating lobby...");
            _pendingHostBootstrap = false;
            Managers.LobbySession.BootstrapLocalHostLobby();
            return;
        }

        if (string.IsNullOrWhiteSpace(_pendingJoinCode))
        {
            if (_isLobbySetupPending)
            {
                if (!Managers.LobbySession.HasJoinedLobbySession)
                {
                    SetLobbyLoading(true, "Preparing lobby...");
                    return;
                }

                if (!IsLocalLobbyInteractionReady())
                {
                    SetLobbyLoading(true, "Spawning local player...");
                    return;
                }
            }

            SetLobbyLoading(false);
            return;
        }

        string joinCode = _pendingJoinCode;
        SetLobbyLoading(true, "Joining lobby...");
        _pendingJoinCode = string.Empty;
        if (!Managers.LobbySession.JoinLobbyByCode(joinCode))
        {
            Managers.Chat.EnqueueMessage("Failed to join lobby with that code.", 2.5f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
        }
    }

    private void UpdateLobbyLoadingState()
    {
        if (!_isLobbySetupPending)
            return;

        if (Managers.LobbySession.HasLobbyNetworkConnectionFailed)
        {
            SetLobbyLoading(false);
            _isLobbySetupPending = false;
            Managers.Toast.EnqueueMessage("Failed to connect to lobby host.", 3f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
            return;
        }

        bool hasLocalRanger = Managers.LobbySession.TryGetLocalRangerTransform(out _);
        bool hasCamera = _localLobbyCamera != null;

        if (Managers.LobbySession.HasJoinedLobbySession &&
            Managers.LobbySession.IsLobbyNetworkConnected &&
            hasLocalRanger &&
            hasCamera)
        {
            SetLobbyLoading(false);
            _isLobbySetupPending = false;
            return;
        }

        if (Time.unscaledTime - _lobbySetupStartedAt > LobbyJoinTimeoutSeconds)
        {
            SetLobbyLoading(false);
            _isLobbySetupPending = false;
            Managers.Toast.EnqueueMessage("Lobby connection timed out.", 3f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
        }
    }

    private void SetLobbyLoading(bool active, string message = null)
    {
        EnsureLoadingUI();

        _isLobbySetupPending = active;
        _loadingUi.gameObject.SetActive(active);

        if (!string.IsNullOrWhiteSpace(message))
            _loadingUi.SetMessage(message);

        if (active)
            Managers.Input.SetMode(Define.InputMode.UI);
        else
            RefreshInputMode();
    }

    private void EnsureLobbyMenu()
    {
        if (_lobbyMenu != null)
            return;

        _lobbyMenu = Managers.UI.ShowSceneUI<UI_LobbyMenu>(nameof(UI_LobbyMenu));
        _lobbyMenu.gameObject.SetActive(false);
    }

    private void EnsureLoadingUI()
    {
        if (_loadingUi != null)
            return;

        _loadingUi = Managers.UI.ShowSceneUI<UI_Loading>(nameof(UI_Loading));
        _loadingUi.gameObject.SetActive(false);
    }

    private void EnsureWorldScreens()
    {
        _mainScreen = _mainScreen != null ? _mainScreen : EnsureWorldScreen(_mainScreen, MainScreenParentName, MainScreenPrefabName, s_mainScreenLocalPosition, s_mainScreenLocalRotation, s_screenWorldScale);
        _leftKioskScreen = _leftKioskScreen != null ? _leftKioskScreen : EnsureWorldScreen(_leftKioskScreen, LeftKioskScreenParentName, KioskScreenPrefabName, s_kioskScreenLocalPosition, s_kioskScreenLocalRotation, s_screenWorldScale);
        _rightKioskScreen = _rightKioskScreen != null ? _rightKioskScreen : EnsureWorldScreen(_rightKioskScreen, RightKioskScreenParentName, KioskScreenPrefabName, s_kioskScreenLocalPosition, s_kioskScreenLocalRotation, s_screenWorldScale);
        _whiteBoard = _whiteBoard != null ? _whiteBoard : EnsureWorldScreen(_whiteBoard, WhiteBoardParentName, WhiteBoardPrefabName, s_whiteBoardLocalPosition, s_whiteBoardLocalRotation, s_screenWorldScale);
    }

    private static T EnsureWorldScreen<T>(T screen, string parentName, string prefabName, Vector3 localPosition, Quaternion localRotation, Vector3 localScale) where T : UI_Base
    {
        Transform parent = GameObject.Find(parentName).transform;
        screen = Managers.UI.CreateWorldSpaceUI<T>(parent, prefabName);
        ApplyWorldScreenTransform(screen.transform, parent, localPosition, localRotation, localScale);
        return screen;
    }

    private static void ApplyWorldScreenTransform(Transform targetTransform, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        targetTransform.SetParent(parent, false);
        targetTransform.localPosition = localPosition;
        targetTransform.localRotation = localRotation;
        targetTransform.localScale = localScale;

        Canvas canvas = targetTransform.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
    }

    public void RequestShowRoleSelectMenu()
    {
        if (_isLobbySetupPending || !Managers.LobbySession.HasJoinedLobbySession)
            return;

        ShowRoleSelectMenu();
    }

    public void RequestShowWhiteBoardMenu()
    {
        if (_isLobbySetupPending || !Managers.LobbySession.HasJoinedLobbySession)
            return;

        ShowWhiteBoardMenu();
    }

    private void HandleRoleSelected(Define.TitanRole role)
    {
        if (_isLobbySetupPending || !Managers.LobbySession.HasJoinedLobbySession)
            return;

        if (TryToggleLocalRole(role) && _roleSelectMenu != null)
            _roleSelectMenu.RefreshRoleNicknames();
    }

    private void HandleRoleSelectMenuClosed()
    {
        CloseRoleSelectMenu();
    }

    private void HandleWhiteBoardMenuClosed()
    {
        CloseWhiteBoardMenu();
    }

    private bool TryToggleLocalRole(Define.TitanRole selectedRole)
    {
        LobbyNetworkPlayer localPlayer = FindLocalOwnedNetworkPlayer();
        if (localPlayer == null)
            return false;

        int currentMask = localPlayer.SelectedTitanRoleMaskValue;
        int bit = 1 << (((int)selectedRole) - (int)Define.TitanRole.Torso);
        int nextMask = currentMask ^ bit;
        bool isSelecting = (currentMask & bit) == 0;

        if (isSelecting && localPlayer.IsTitanRoleSelectedByOtherPlayer(selectedRole))
        {
            Managers.Toast.EnqueueMessage($"Already selected: {GetRoleLabel(selectedRole)}", 1.4f);
            return false;
        }

        localPlayer.ToggleTitanRoleSelection(selectedRole);

        if (localPlayer.TryGetLobbyUserId(out string lobbyUserId))
            RegisterUserPartSelection(lobbyUserId, nextMask);

        bool isSelected = (nextMask & bit) != 0;
        Managers.Toast.EnqueueMessage($"{(isSelected ? "Selected" : "Unselected")} part: {GetRoleLabel(selectedRole)}", 1.4f);
        return true;
    }

    private void EnsureRoleSelectMenu()
    {
        if (_roleSelectMenu != null)
            return;

        _roleSelectMenu = Managers.UI.ShowSceneUI<UI_RoleSelectMenu>(nameof(UI_RoleSelectMenu));

        _roleSelectMenu.RoleSelected -= HandleRoleSelected;
        _roleSelectMenu.RoleSelected += HandleRoleSelected;
        _roleSelectMenu.Closed -= HandleRoleSelectMenuClosed;
        _roleSelectMenu.Closed += HandleRoleSelectMenuClosed;
        _roleSelectMenu.gameObject.SetActive(false);
    }

    private void ShowRoleSelectMenu()
    {
        EnsureRoleSelectMenu();

        _roleSelectMenu.gameObject.SetActive(true);
        _roleSelectMenu.RefreshRoleNicknames();
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private bool CloseRoleSelectMenu()
    {
        if (_roleSelectMenu == null || !_roleSelectMenu.gameObject.activeSelf)
            return false;

        _roleSelectMenu.gameObject.SetActive(false);
        RefreshInputMode();
        return true;
    }

    private void EnsureWhiteBoardMenu()
    {
        if (_whiteBoardMenu != null)
            return;

        _whiteBoardMenu = Managers.UI.ShowSceneUI<UI_WhiteBoardMenu>(nameof(UI_WhiteBoardMenu));

        _whiteBoardMenu.Closed -= HandleWhiteBoardMenuClosed;
        _whiteBoardMenu.Closed += HandleWhiteBoardMenuClosed;
        _whiteBoardMenu.gameObject.SetActive(false);
    }

    private void ShowWhiteBoardMenu()
    {
        EnsureWhiteBoardMenu();

        _whiteBoardMenu.gameObject.SetActive(true);
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private bool CloseWhiteBoardMenu()
    {
        if (_whiteBoardMenu == null || !_whiteBoardMenu.gameObject.activeSelf)
            return false;

        _whiteBoardMenu.gameObject.SetActive(false);
        RefreshInputMode();
        return true;
    }

    private void EnsureLocalLobbyCameraReady()
    {
        if (!Managers.LobbySession.HasJoinedLobbySession)
            return;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform localRanger) || localRanger == null)
            return;

        if (_localLobbyCamera == null)
        {
            GameObject cameraObject = Managers.Resource.Instantiate(LobbyCameraPrefabName);
            _localLobbyCamera = cameraObject.GetComponent<LobbyCameraController>();
        }

        _localLobbyCamera.SetTarget(localRanger);
    }

    private bool IsLocalLobbyInteractionReady()
    {
        if (_localLobbyCamera == null)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform localRanger) || localRanger == null)
            return false;

        return true;
    }

    private static LobbyNetworkPlayer FindLocalOwnedNetworkPlayer()
    {
        return LobbyNetworkPlayer.FindLocalOwnedPlayer();
    }

    private static bool AreAllLobbyUsersReadyForGame(out string missingUserId)
    {
        missingUserId = string.Empty;
        if (s_userEntriesByUserId.Count == 0)
        {
            missingUserId = "No lobby users";
            return false;
        }

        Dictionary<int, string> ownerByRoleValue = new();
        int combinedRoleMask = 0;

        foreach (KeyValuePair<string, LobbyUserEntry> pair in s_userEntriesByUserId)
        {
            string userId = pair.Key;
            LobbyUserEntry entry = pair.Value;
            if (entry == null || entry.Ranger == null)
            {
                missingUserId = userId;
                return false;
            }

            int roleMask = NormalizeRoleMask(entry.SelectedRoleMask);
            combinedRoleMask |= roleMask;

            for (int roleValue = (int)Define.TitanRole.Torso; roleValue <= (int)Define.TitanRole.RightLeg; roleValue++)
            {
                int bit = 1 << (roleValue - 1);
                if ((roleMask & bit) == 0)
                    continue;

                if (ownerByRoleValue.TryGetValue(roleValue, out string existingOwner) && !string.Equals(existingOwner, userId))
                {
                    missingUserId = $"Duplicate role: {GetRoleLabel((Define.TitanRole)roleValue)}";
                    return false;
                }

                ownerByRoleValue[roleValue] = userId;
            }
        }

        for (int roleValue = (int)Define.TitanRole.Torso; roleValue <= (int)Define.TitanRole.RightLeg; roleValue++)
        {
            int bit = 1 << (roleValue - 1);
            if ((combinedRoleMask & bit) == 0)
            {
                missingUserId = $"Missing role: {GetRoleLabel((Define.TitanRole)roleValue)}";
                return false;
            }
        }

        return true;
    }

    private static int NormalizeRoleMask(int roleMask)
    {
        return roleMask & GetAllRoleMask();
    }

    private static int GetAllRoleMask()
    {
        int count = ((int)Define.TitanRole.RightLeg - (int)Define.TitanRole.Torso) + 1;
        return (1 << count) - 1;
    }

    private static string GetRoleLabel(Define.TitanRole role)
    {
        return role switch
        {
            Define.TitanRole.Torso => "Center",
            Define.TitanRole.LeftArm => "Left Arm",
            Define.TitanRole.RightArm => "Right Arm",
            Define.TitanRole.LeftLeg => "Left Leg",
            Define.TitanRole.RightLeg => "Right Leg",
            _ => "Unknown",
        };
    }

    private void ToggleMenuInputMode()
    {
        if (Managers.Input.Mode == Define.InputMode.UI)
        {
            if (_lobbyMenu != null && _lobbyMenu.CloseActiveSubMenu())
                return;

            HideAllMenus();
            Managers.Input.SetMode(Define.InputMode.Player);
            return;
        }

        EnsureLobbyMenu();
        _lobbyMenu.gameObject.SetActive(true);
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private void HideAllMenus()
    {
        Managers.UI.HideAllMenuUIs();
    }

    private void RefreshInputMode()
    {
        bool hasBlockingUi = _isLobbySetupPending
            || Managers.UI.HasActiveMenuUI();

        Managers.Input.SetMode(hasBlockingUi ? Define.InputMode.UI : Define.InputMode.Player);
    }

    private static bool IsEscapePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    public override void Clear()
    {
        ClearUserObjectRegistry();

        if (_lobbyMenu != null)
        {
            Managers.Resource.Destory(_lobbyMenu.gameObject);
            _lobbyMenu = null;
        }

        if (_loadingUi != null)
        {
            Managers.Resource.Destory(_loadingUi.gameObject);
            _loadingUi = null;
        }

        if (_roleSelectMenu != null)
        {
            _roleSelectMenu.RoleSelected -= HandleRoleSelected;
            _roleSelectMenu.Closed -= HandleRoleSelectMenuClosed;
            Managers.Resource.Destory(_roleSelectMenu.gameObject);
            _roleSelectMenu = null;
        }

        if (_whiteBoardMenu != null)
        {
            _whiteBoardMenu.Closed -= HandleWhiteBoardMenuClosed;
            Managers.Resource.Destory(_whiteBoardMenu.gameObject);
            _whiteBoardMenu = null;
        }

        if (_mainScreen != null)
        {
            Managers.Resource.Destory(_mainScreen.gameObject);
            _mainScreen = null;
        }

        if (_leftKioskScreen != null)
        {
            Managers.Resource.Destory(_leftKioskScreen.gameObject);
            _leftKioskScreen = null;
        }

        if (_rightKioskScreen != null)
        {
            Managers.Resource.Destory(_rightKioskScreen.gameObject);
            _rightKioskScreen = null;
        }

        if (_whiteBoard != null)
        {
            Managers.Resource.Destory(_whiteBoard.gameObject);
            _whiteBoard = null;
        }

        _localLobbyCamera = null;
    }
}
