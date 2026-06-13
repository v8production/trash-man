using System;
using System.Collections.Generic;
using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class LobbySessionManager
{
    private const int LobbyCapacity = 5;
    private const string LobbyJoinCodeKey = "join_code";
    private const string LobbyHostSteamIdKey = "host_steam_id";

    private readonly Dictionary<string, RangerController> _rangersByUserId = new();
    private readonly Dictionary<string, UI_Nickname> _nicknamesByUserId = new();

    private ulong _currentHostSteamId;

    private CSteamID _currentSteamLobbyId = CSteamID.Nil;

    private bool _steamCallbacksReady;
    private Callback<LobbyEnter_t> _lobbyEnterCallback;
    private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private Callback<NewUrlLaunchParameters_t> _newUrlLaunchParametersCallback;
    private CallResult<LobbyCreated_t> _lobbyCreatedResult;
    private CallResult<LobbyMatchList_t> _lobbyMatchListResult;
    private bool _checkedInitialSteamLaunchParameters;

    private bool _pendingSteamClientConnect;
    private bool _hasRequestedSteamClientStart;
    private float _pendingSteamClientConnectDeadline;
    private bool _handlingHostMigration;
    private int _pendingMigratedHostRoleMask;
    private float _pendingMigratedHostRoleMaskDeadline;
    private int _pendingRestoredLocalRoleMask;
    private float _pendingRestoredLocalRoleMaskDeadline;

    private static bool s_loggedNetcodeMissing;
    private static bool s_loggedNetworkManagerMissing;
    private static bool s_loggedTransportMissing;

    public bool IsHosting { get; private set; }
    public string HostUserId { get; private set; } = string.Empty;
    public string CurrentJoinCode { get; private set; } = string.Empty;
    public bool HasJoinedLobbySession => IsLobbyNetworkConnected;
    public bool HasPendingSteamLobbyJoin { get; private set; }

    public bool HasLobbyNetworkConnectionFailed { get; private set; }
    public string LastLobbyNetworkError { get; private set; } = string.Empty;

    public void Init()
    {
        EnsureSteamCallbacks();
        TryHandleSteamLaunchParameters(force: false);
    }

    public void OnUpdate()
    {
        EnsureSteamCallbacks();
        TryHandleSteamLaunchParameters(force: false);

        if (Managers.Scene.CurrentScene == null)
            return;

        Define.Scene sceneType = Managers.Scene.CurrentScene.SceneType;
        if (sceneType != Define.Scene.Lobby && sceneType != Define.Scene.Game)
            return;

        TryResolvePendingSteamClientConnect();
        TryApplyPendingMigratedHostRoleMask();
        TryApplyPendingRestoredLocalRoleMask();
    }

    public void Clear()
    {
        _rangersByUserId.Clear();
        _nicknamesByUserId.Clear();
        IsHosting = false;
        HostUserId = string.Empty;
        CurrentJoinCode = string.Empty;
        _currentHostSteamId = 0;
        _currentSteamLobbyId = CSteamID.Nil;
        HasPendingSteamLobbyJoin = false;
        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;
        _handlingHostMigration = false;
        _pendingMigratedHostRoleMask = 0;
        _pendingMigratedHostRoleMaskDeadline = 0f;
        _pendingRestoredLocalRoleMask = 0;
        _pendingRestoredLocalRoleMaskDeadline = 0f;
        ResetClientConnectionTracking();
    }

    public static string NormalizeJoinCode(string value) => Util.NormalizeLobbyJoinCode(value);

    public bool HasJoinCode(string rawJoinCode)
    {
        string joinCode = NormalizeJoinCode(rawJoinCode);
        return !string.IsNullOrWhiteSpace(joinCode);
    }

    public void RegisterLobbyUserObjects(string userId, RangerController ranger, UI_Nickname nickname)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (nickname != null && _nicknamesByUserId.TryGetValue(userId, out UI_Nickname prevNickname) && prevNickname != null && prevNickname != nickname)
            UnityEngine.Object.Destroy(prevNickname.gameObject);

        if (ranger != null)
            _rangersByUserId[userId] = ranger;

        if (nickname != null)
            _nicknamesByUserId[userId] = nickname;

        LobbyScene.RegisterUserObjects(userId, ranger, nickname);
    }

    public bool TryGetLocalRangerTransform(out Transform rangerTransform)
    {
        rangerTransform = null;

        string localUserId = Managers.Steam.LocalUserId;
        if (!string.IsNullOrWhiteSpace(localUserId) && _rangersByUserId.TryGetValue(localUserId, out RangerController cachedRanger) && cachedRanger != null)
        {
            rangerTransform = cachedRanger.transform;
            if (rangerTransform != null)
                return true;
        }

        LobbyNetworkPlayer[] networkPlayers = UnityEngine.Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < networkPlayers.Length; i++)
        {
            LobbyNetworkPlayer networkPlayer = networkPlayers[i];
            if (networkPlayer == null || !networkPlayer.IsOwner)
                continue;

            if (networkPlayer.TryGetLobbyRangerTransform(out Transform lobbyRangerTransform) && lobbyRangerTransform != null)
            {
                rangerTransform = lobbyRangerTransform;
                return true;
            }
        }

        return false;
    }

    public void UnregisterLobbyUserObjects(string userId, RangerController ranger, UI_Nickname nickname)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (_rangersByUserId.TryGetValue(userId, out RangerController storedRanger) && storedRanger == ranger)
            _rangersByUserId.Remove(userId);

        if (_nicknamesByUserId.TryGetValue(userId, out UI_Nickname storedNickname) && storedNickname == nickname)
            _nicknamesByUserId.Remove(userId);

        LobbyScene.UnregisterUserObjects(userId, ranger, nickname);
    }

    public bool JoinLobbyByCode(string rawJoinCode)
    {
        string joinCode = NormalizeJoinCode(rawJoinCode);

        if (!Managers.Steam.IsInitialized)
        {
            Debug.LogWarning("[Lobby] Join failed: Steam is not initialized.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Lobby] Join failed: invalid join code.");
            return false;
        }

        EnsureSteamCallbacks();

        CleanupExistingLobbyObjects();

        CurrentJoinCode = joinCode;
        _currentHostSteamId = 0;
        _currentSteamLobbyId = CSteamID.Nil;
        HasPendingSteamLobbyJoin = true;
        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;

        SteamMatchmaking.AddRequestLobbyListStringFilter(
            LobbyJoinCodeKey,
            joinCode,
            ELobbyComparison.k_ELobbyComparisonEqual);

        SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
        _lobbyMatchListResult.Set(call);
        return true;
    }

    public void QuitCurrentRoom()
    {
        if (Managers.Steam.IsInitialized && _currentSteamLobbyId.IsValid())
        {
            if (IsHosting)
                TryAssignHostToRemainingLobbyMember();

            SteamMatchmaking.LeaveLobby(_currentSteamLobbyId);
            _currentSteamLobbyId = CSteamID.Nil;
        }

        TryStopNetwork();
        _rangersByUserId.Clear();
        _nicknamesByUserId.Clear();
        IsHosting = false;
        HostUserId = string.Empty;
        CurrentJoinCode = string.Empty;
        _currentHostSteamId = 0;
        HasPendingSteamLobbyJoin = false;
        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;
        _handlingHostMigration = false;
        _pendingMigratedHostRoleMask = 0;
        _pendingMigratedHostRoleMaskDeadline = 0f;
        _pendingRestoredLocalRoleMask = 0;
        _pendingRestoredLocalRoleMaskDeadline = 0f;
        ResetClientConnectionTracking();
    }

    public void BootstrapLocalHostLobby()
    {
        if (!Managers.Steam.IsInitialized)
        {
            Managers.Toast.EnqueueMessage($"Steam is not initialized.\n{Managers.Steam.LastInitError}", 3f);
            return;
        }

        CleanupExistingLobbyObjects();

        ulong localSteamId = Managers.Steam.LocalSteamId.m_SteamID;
        HostUserId = Managers.Steam.LocalUserId;
        CurrentJoinCode = Util.CreateLobbyJoinCode();
        _currentHostSteamId = localSteamId;
        _currentSteamLobbyId = CSteamID.Nil;
        HasPendingSteamLobbyJoin = false;
        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;

        IsHosting = TryStartSteamHost();

        if (!IsHosting)
        {
            Debug.LogWarning("[Lobby] Host bootstrap failed: Steam host did not start.");
            Managers.Toast.EnqueueMessage("Failed to start lobby host.\nCheck Steam/Netcode setup.", 3f);
            HostUserId = string.Empty;
            _currentHostSteamId = 0;
            CurrentJoinCode = string.Empty;
            return;
        }

        EnsureSteamCallbacks();

        SteamAPICall_t createCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, LobbyCapacity);
        _lobbyCreatedResult.Set(createCall);

        GUIUtility.systemCopyBuffer = CurrentJoinCode;
        Managers.Toast.EnqueueMessage("Join code copied to clipboard.", 2.5f);
        Debug.Log($"[Lobby] Lobby host ready. hostSteamId={_currentHostSteamId}, joinCode={CurrentJoinCode}");
    }

    public void OpenSteamFriendsOverlay()
    {
        if (!Managers.Steam.IsInitialized)
            return;

        SteamFriends.ActivateGameOverlay("Friends");
    }

    public bool OpenSteamInviteOverlay()
    {
        if (!Managers.Steam.IsInitialized)
            return false;

        if (!_currentSteamLobbyId.IsValid())
            return false;

        SteamFriends.ActivateGameOverlayInviteDialog(_currentSteamLobbyId);
        return true;
    }

    public bool OpenSteamInviteOverlayOrFriends()
    {
        if (!OpenSteamInviteOverlay())
        {
            OpenSteamFriendsOverlay();
            return false;
        }

        return true;
    }

    private static void CleanupExistingLobbyObjects()
    {
        RangerController[] rangers = UnityEngine.Object.FindObjectsByType<RangerController>();
        for (int i = 0; i < rangers.Length; i++)
            UnityEngine.Object.Destroy(rangers[i].gameObject);

        LobbyCameraController[] cameras = UnityEngine.Object.FindObjectsByType<LobbyCameraController>();
        for (int i = 0; i < cameras.Length; i++)
            UnityEngine.Object.Destroy(cameras[i].gameObject);
    }

    private void TryResolvePendingSteamClientConnect()
    {
        if (IsHosting)
            return;

        if (!_pendingSteamClientConnect || _hasRequestedSteamClientStart)
            return;

        if (_currentSteamLobbyId.IsValid())
        {
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentSteamLobbyId);
            if (owner.m_SteamID != 0)
                _currentHostSteamId = owner.m_SteamID;
        }

        if (_currentHostSteamId != 0)
        {
            if (!TryStartSteamClient(_currentHostSteamId))
            {
                HasLobbyNetworkConnectionFailed = true;
                LastLobbyNetworkError = $"Failed to start Steam client. hostSteamId={_currentHostSteamId}";
                Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
                Managers.Toast.EnqueueMessage("Failed to connect to lobby host.", 2.5f);
                Managers.Scene.LoadScene(Define.Scene.Intro);
                return;
            }

            _hasRequestedSteamClientStart = true;
            _pendingSteamClientConnect = false;
            return;
        }

        if (Time.unscaledTime >= _pendingSteamClientConnectDeadline)
        {
            HasLobbyNetworkConnectionFailed = true;
            LastLobbyNetworkError = "Lobby is missing host Steam ID.";
            Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
            Managers.Toast.EnqueueMessage("Failed to connect to lobby host.", 2.5f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
        }
    }

    private void TryApplyPendingMigratedHostRoleMask()
    {
        if (_pendingMigratedHostRoleMask == 0)
            return;

        if (!IsHosting)
            return;

        if (Managers.Scene.CurrentScene == null || Managers.Scene.CurrentScene.SceneType != Define.Scene.Game)
        {
            _pendingMigratedHostRoleMask = 0;
            _pendingMigratedHostRoleMaskDeadline = 0f;
            return;
        }

        LobbyNetworkPlayer[] players = LobbyNetworkPlayer.FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsOwner)
                continue;

            if (!player.TryApplyMigratedTitanRoleMask(_pendingMigratedHostRoleMask))
                continue;

            Debug.Log($"[Lobby] Migrated host role mask applied. clientId={player.OwnerClientId}, roleMask=0x{_pendingMigratedHostRoleMask:X}");
            _pendingMigratedHostRoleMask = 0;
            _pendingMigratedHostRoleMaskDeadline = 0f;
            Managers.TitanRole.RefreshRoleMap(requireAllRoles: false, out _);
            return;
        }

        if (Time.unscaledTime < _pendingMigratedHostRoleMaskDeadline)
            return;

        Debug.LogWarning($"[Lobby] Timed out applying migrated host role mask. roleMask=0x{_pendingMigratedHostRoleMask:X}");
        _pendingMigratedHostRoleMask = 0;
        _pendingMigratedHostRoleMaskDeadline = 0f;
    }

    private void TryApplyPendingRestoredLocalRoleMask()
    {
        if (_pendingRestoredLocalRoleMask == 0)
            return;

        if (Managers.Scene.CurrentScene == null || Managers.Scene.CurrentScene.SceneType != Define.Scene.Game)
        {
            _pendingRestoredLocalRoleMask = 0;
            _pendingRestoredLocalRoleMaskDeadline = 0f;
            return;
        }

        LobbyNetworkPlayer[] players = LobbyNetworkPlayer.FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsOwner)
                continue;

            if (!player.TrySubmitRestoredTitanRoleMask(_pendingRestoredLocalRoleMask))
                continue;

            Debug.Log($"[Lobby] Restored local role mask after game host migration. clientId={player.OwnerClientId}, roleMask=0x{_pendingRestoredLocalRoleMask:X}");
            _pendingRestoredLocalRoleMask = 0;
            _pendingRestoredLocalRoleMaskDeadline = 0f;
            return;
        }

        if (Time.unscaledTime < _pendingRestoredLocalRoleMaskDeadline)
            return;

        Debug.LogWarning($"[Lobby] Timed out restoring local role mask after game host migration. roleMask=0x{_pendingRestoredLocalRoleMask:X}");
        _pendingRestoredLocalRoleMask = 0;
        _pendingRestoredLocalRoleMaskDeadline = 0f;
    }

    private void EnsureSteamCallbacks()
    {
        if (_steamCallbacksReady)
            return;

        if (!Managers.Steam.IsInitialized)
            return;

        _steamCallbacksReady = true;

        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(HandleLobbyEnter);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(HandleLobbyChatUpdate);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(HandleGameLobbyJoinRequested);
        _newUrlLaunchParametersCallback = Callback<NewUrlLaunchParameters_t>.Create(HandleNewUrlLaunchParameters);
        _lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(HandleLobbyCreated);
        _lobbyMatchListResult = CallResult<LobbyMatchList_t>.Create(HandleLobbyMatchList);
    }

    private void HandleNewUrlLaunchParameters(NewUrlLaunchParameters_t callback)
    {
        TryHandleSteamLaunchParameters(force: true);
    }

    private void HandleLobbyCreated(LobbyCreated_t callback, bool ioFailure)
    {
        if (!IsHosting)
            return;

        if (ioFailure || callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogWarning($"[Lobby] Steam lobby create failed. ioFailure={ioFailure}, result={callback.m_eResult}");
            Managers.Toast.EnqueueMessage("Failed to create Steam lobby.", 2.5f);
            return;
        }

        _currentSteamLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        SteamMatchmaking.SetLobbyJoinable(_currentSteamLobbyId, true);
        SteamMatchmaking.SetLobbyMemberLimit(_currentSteamLobbyId, LobbyCapacity);
        SteamMatchmaking.SetLobbyData(_currentSteamLobbyId, LobbyJoinCodeKey, CurrentJoinCode);
        SteamMatchmaking.SetLobbyData(_currentSteamLobbyId, LobbyHostSteamIdKey, _currentHostSteamId.ToString());

        Debug.Log($"[Lobby] Steam lobby created. lobbyId={_currentSteamLobbyId.m_SteamID}, joinCode={CurrentJoinCode}");
    }

    private void HandleLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (!_currentSteamLobbyId.IsValid() || callback.m_ulSteamIDLobby != _currentSteamLobbyId.m_SteamID)
            return;

        EChatMemberStateChange state = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;
        bool memberLeft = (state & EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0
            || (state & EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0
            || (state & EChatMemberStateChange.k_EChatMemberStateChangeKicked) != 0
            || (state & EChatMemberStateChange.k_EChatMemberStateChangeBanned) != 0;

        if (!memberLeft)
            return;

        ulong changedSteamId = callback.m_ulSteamIDUserChanged;
        if (changedSteamId == 0 || changedSteamId != _currentHostSteamId)
            return;

        TryRecoverFromHostMigration("Steam lobby host left");
    }

    private void HandleLobbyMatchList(LobbyMatchList_t callback, bool ioFailure)
    {
        if (IsHosting)
            return;

        if (ioFailure || callback.m_nLobbiesMatching <= 0)
        {
            HasPendingSteamLobbyJoin = false;
            HasLobbyNetworkConnectionFailed = true;
            LastLobbyNetworkError = "No matching lobby found for that join code.";
            Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
            Managers.Toast.EnqueueMessage("No lobby found for that join code.", 2.5f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
            return;
        }

        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    private void HandleGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        if (!Managers.Steam.IsInitialized)
            return;

        // User accepted invite or selected Join Game from Steam overlay.
        RequestJoinSteamLobby(callback.m_steamIDLobby);
    }

    private void RequestJoinSteamLobby(CSteamID steamLobbyId)
    {
        if (!steamLobbyId.IsValid())
            return;

        HasPendingSteamLobbyJoin = true;
        Managers.Scene.LoadScene(Define.Scene.Lobby);
        SteamMatchmaking.JoinLobby(steamLobbyId);
    }

    private void TryHandleSteamLaunchParameters(bool force)
    {
        if (!force && _checkedInitialSteamLaunchParameters)
            return;

        if (!Managers.Steam.IsInitialized)
            return;

        if (!force)
            _checkedInitialSteamLaunchParameters = true;

        if (TryGetLaunchLobbyId(out CSteamID steamLobbyId))
            RequestJoinSteamLobby(steamLobbyId);
    }

    private static bool TryGetLaunchLobbyId(out CSteamID steamLobbyId)
    {
        steamLobbyId = CSteamID.Nil;

        string queryLobbyId = SteamApps.GetLaunchQueryParam("connect_lobby");
        if (TryParseSteamLobbyId(queryLobbyId, out steamLobbyId))
            return true;

        SteamApps.GetLaunchCommandLine(out string commandLine, 1024);
        if (string.IsNullOrWhiteSpace(commandLine))
            return false;

        string[] args = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("+connect_lobby", StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length)
                return false;

            return TryParseSteamLobbyId(args[i + 1], out steamLobbyId);
        }

        return false;
    }

    private static bool TryParseSteamLobbyId(string value, out CSteamID steamLobbyId)
    {
        steamLobbyId = CSteamID.Nil;
        if (!ulong.TryParse(value, out ulong rawLobbyId) || rawLobbyId == 0)
            return false;

        steamLobbyId = new CSteamID(rawLobbyId);
        return steamLobbyId.IsValid();
    }

    private bool TryAssignHostToRemainingLobbyMember()
    {
        if (!Managers.Steam.IsInitialized || !_currentSteamLobbyId.IsValid())
            return false;

        CSteamID localSteamId = Managers.Steam.LocalSteamId;
        if (SteamMatchmaking.GetLobbyOwner(_currentSteamLobbyId) != localSteamId)
            return false;

        if (!TrySelectNextHostSteamId(localSteamId.m_SteamID, out ulong nextHostSteamId))
            return false;

        CSteamID nextHost = new CSteamID(nextHostSteamId);
        SteamMatchmaking.SetLobbyData(_currentSteamLobbyId, LobbyHostSteamIdKey, nextHostSteamId.ToString());
        bool assigned = SteamMatchmaking.SetLobbyOwner(_currentSteamLobbyId, nextHost);

        Debug.Log($"[Lobby] Host migration requested. previousHost={localSteamId.m_SteamID}, nextHost={nextHostSteamId}, assigned={assigned}");
        return assigned;
    }

    private bool TrySelectNextHostSteamId(ulong leavingHostSteamId, out ulong nextHostSteamId)
    {
        nextHostSteamId = 0;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentSteamLobbyId);
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_currentSteamLobbyId, i);
            ulong memberSteamId = member.m_SteamID;
            if (memberSteamId == 0 || memberSteamId == leavingHostSteamId)
                continue;

            if (nextHostSteamId == 0 || memberSteamId < nextHostSteamId)
                nextHostSteamId = memberSteamId;
        }

        return nextHostSteamId != 0;
    }

    private bool TrySelectCurrentHostSteamId(out ulong hostSteamId)
    {
        hostSteamId = 0;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentSteamLobbyId);
        for (int i = 0; i < memberCount; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_currentSteamLobbyId, i);
            ulong memberSteamId = member.m_SteamID;
            if (memberSteamId == 0)
                continue;

            if (hostSteamId == 0 || memberSteamId < hostSteamId)
                hostSteamId = memberSteamId;
        }

        return hostSteamId != 0;
    }

    private void TryRecoverFromHostMigration(string reason)
    {
        if (_handlingHostMigration || IsHosting)
            return;

        if (!Managers.Steam.IsInitialized || !_currentSteamLobbyId.IsValid())
            return;

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentSteamLobbyId);
        ulong ownerSteamId = owner.m_SteamID;
        if (ownerSteamId == 0)
            return;

        Define.Scene sceneType = Managers.Scene.CurrentScene != null ? Managers.Scene.CurrentScene.SceneType : Define.Scene.Unknown;
        bool isGameScene = sceneType == Define.Scene.Game;
        int migratedHostRoleMask = 0;
        if (isGameScene && _currentHostSteamId != 0)
            LobbyScene.TryGetRegisteredUserSelectedRoleMask(_currentHostSteamId.ToString(), out migratedHostRoleMask);

        _handlingHostMigration = true;
        if (sceneType == Define.Scene.Lobby)
        {
            LobbyScene.ClearRegisteredUserPartSelections();
            CleanupExistingLobbyObjects();
        }

        CSteamID localSteamId = Managers.Steam.LocalSteamId;
        if (ownerSteamId == localSteamId.m_SteamID
            && TrySelectCurrentHostSteamId(out ulong electedHostSteamId)
            && electedHostSteamId != localSteamId.m_SteamID)
        {
            CSteamID electedHost = new CSteamID(electedHostSteamId);
            SteamMatchmaking.SetLobbyData(_currentSteamLobbyId, LobbyHostSteamIdKey, electedHostSteamId.ToString());
            SteamMatchmaking.SetLobbyOwner(_currentSteamLobbyId, electedHost);
            TryReconnectToMigratedHost(electedHostSteamId, migratedHostRoleMask, $"{reason}; reassigned by deterministic host election");
            return;
        }

        if (ownerSteamId == localSteamId.m_SteamID)
        {
            TryPromoteLocalClientToHost(migratedHostRoleMask, reason);
            return;
        }

        TryReconnectToMigratedHost(ownerSteamId, migratedHostRoleMask, reason);
    }

    private void TryPromoteLocalClientToHost(int migratedHostRoleMask, string reason)
    {
        if (Managers.Scene.CurrentScene != null
            && Managers.Scene.CurrentScene.SceneType == Define.Scene.Game
            && LobbyScene.TryGetRegisteredUserSelectedRoleMask(Managers.Steam.LocalUserId, out int localRoleMask))
        {
            migratedHostRoleMask |= localRoleMask;
        }

        TryStopNetwork();

        HostUserId = Managers.Steam.LocalUserId;
        _currentHostSteamId = Managers.Steam.LocalSteamId.m_SteamID;
        SteamMatchmaking.SetLobbyData(_currentSteamLobbyId, LobbyHostSteamIdKey, _currentHostSteamId.ToString());

        IsHosting = TryStartSteamHost();
        _handlingHostMigration = false;

        if (IsHosting && migratedHostRoleMask != 0 && Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Game)
        {
            _pendingMigratedHostRoleMask = migratedHostRoleMask;
            _pendingMigratedHostRoleMaskDeadline = Time.unscaledTime + 5f;
        }

        if (!IsHosting)
        {
            HasLobbyNetworkConnectionFailed = true;
            LastLobbyNetworkError = $"Failed to promote local client to lobby host. reason={reason}";
            Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
            Managers.Toast.EnqueueMessage("Failed to take over lobby host.", 2.5f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
            return;
        }

        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;
        Debug.Log($"[Lobby] Local client promoted to lobby host. hostSteamId={_currentHostSteamId}, reason={reason}");
        Managers.Toast.EnqueueMessage(migratedHostRoleMask != 0 ? "Game host transferred to you. Previous host roles were assigned." : "Lobby host transferred to you. Roles were reset.", 2.5f);
    }

    private void TryReconnectToMigratedHost(ulong hostSteamId, int migratedHostRoleMask, string reason)
    {
        int localRoleMask = 0;
        bool shouldRestoreLocalRoleMask = Managers.Scene.CurrentScene != null
            && Managers.Scene.CurrentScene.SceneType == Define.Scene.Game
            && LobbyScene.TryGetRegisteredUserSelectedRoleMask(Managers.Steam.LocalUserId, out localRoleMask);

        TryStopNetwork();

        IsHosting = false;
        HostUserId = string.Empty;
        _currentHostSteamId = hostSteamId;
        _handlingHostMigration = false;

        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;
        _pendingSteamClientConnect = true;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = Time.unscaledTime + 5f;

        if (shouldRestoreLocalRoleMask)
        {
            _pendingRestoredLocalRoleMask = localRoleMask;
            _pendingRestoredLocalRoleMaskDeadline = Time.unscaledTime + 5f;
        }

        Debug.Log($"[Lobby] Scheduled reconnect to migrated lobby host. hostSteamId={hostSteamId}, reason={reason}");
        Managers.Toast.EnqueueMessage(migratedHostRoleMask != 0 ? "Game host changed. Reconnecting... Host roles were transferred." : "Lobby host changed. Reconnecting... Roles were reset.", 2.5f);
    }

    private void HandleLobbyEnter(LobbyEnter_t callback)
    {
        if (IsHosting)
            return;

        _currentSteamLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentSteamLobbyId);
        ulong hostSteamId = owner.m_SteamID;
        if (hostSteamId == 0)
        {
            HasPendingSteamLobbyJoin = false;
            HasLobbyNetworkConnectionFailed = true;
            LastLobbyNetworkError = "Failed to resolve lobby owner.";
            Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
            Managers.Scene.LoadScene(Define.Scene.Intro);
            return;
        }

        _currentHostSteamId = hostSteamId;

        if (!TryStartSteamClient(hostSteamId))
        {
            HasPendingSteamLobbyJoin = false;
            HasLobbyNetworkConnectionFailed = true;
            LastLobbyNetworkError = $"Failed to start Steam client. hostSteamId={hostSteamId}";
            Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
            Managers.Toast.EnqueueMessage("Failed to connect to lobby host.", 2.5f);
            Managers.Scene.LoadScene(Define.Scene.Intro);
            return;
        }

        _hasRequestedSteamClientStart = true;
        HasPendingSteamLobbyJoin = false;
    }

    private bool TryStartSteamHost()
    {
        if (!TryResolveNetworkObjects(out NetworkManager networkManager, out SteamNetworkingSocketsTransport steamTransport))
        {
            Debug.LogWarning("[Lobby] TryStartSteamHost failed: TryResolveNetworkObjects returned false.");
            return false;
        }

        if (networkManager.IsListening)
        {
            if (networkManager.IsHost)
                return true;

            networkManager.Shutdown();
            Debug.Log("[Lobby] Existing NetworkManager was listening but not host. Shutdown requested before retry.");
            return false;
        }

        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;

        return networkManager.StartHost();
    }

    private bool TryStartSteamClient(ulong hostSteamId)
    {
        if (!TryResolveNetworkObjects(out NetworkManager networkManager, out SteamNetworkingSocketsTransport steamTransport))
            return false;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;
        _currentHostSteamId = hostSteamId;

        steamTransport.ConnectToSteamID = hostSteamId;

        RegisterClientConnectionCallbacks(networkManager);

        return networkManager.StartClient();
    }

    private void TryStopNetwork()
    {
        NetworkManager networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        ResetClientConnectionTracking();
    }

    private void ResetClientConnectionTracking()
    {
        _currentHostSteamId = 0;
        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;

        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;
    }

    private static bool TryResolveNetworkObjects(out NetworkManager networkManager, out SteamNetworkingSocketsTransport steamTransport)
    {
        if (!LobbyNetworkRuntime.EnsureSetup(out networkManager, out steamTransport))
        {
            if (!s_loggedNetcodeMissing)
            {
                Debug.LogWarning("[Lobby] Failed to ensure lobby network runtime.");
                s_loggedNetcodeMissing = true;
            }

            return false;
        }

        if (networkManager == null)
        {
            if (!s_loggedNetworkManagerMissing)
            {
                Debug.LogWarning("[Lobby] NetworkManager is missing after runtime setup.");
                s_loggedNetworkManagerMissing = true;
            }

            return false;
        }

        if (steamTransport == null)
        {
            if (!s_loggedTransportMissing)
            {
                Debug.LogWarning("[Lobby] Steam transport is missing after runtime setup.");
                s_loggedTransportMissing = true;
            }

            return false;
        }

        return true;
    }

    public bool IsLobbyNetworkConnected
    {
        get
        {
            NetworkManager networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
            if (networkManager == null)
                return false;

            if (IsHosting)
                return networkManager.IsListening && networkManager.IsHost;

            return networkManager.IsListening && networkManager.IsClient && networkManager.IsConnectedClient;
        }
    }

    private void RegisterClientConnectionCallbacks(NetworkManager networkManager)
    {
        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        HasLobbyNetworkConnectionFailed = false;
        LastLobbyNetworkError = string.Empty;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (IsHosting)
            return;

        ulong previousHostSteamId = _currentHostSteamId;

        NetworkManager networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
        if (networkManager != null && networkManager.IsConnectedClient)
            return;

        if (previousHostSteamId != 0)
        {
            TryRecoverFromHostMigration($"Netcode host disconnected. clientId={clientId}");
            if (_currentHostSteamId != previousHostSteamId || IsHosting)
                return;
        }

        HasLobbyNetworkConnectionFailed = true;
        LastLobbyNetworkError = $"Disconnected from lobby host. clientId={clientId}";
        Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
    }
}
