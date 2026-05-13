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
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private CallResult<LobbyCreated_t> _lobbyCreatedResult;
    private CallResult<LobbyMatchList_t> _lobbyMatchListResult;

    private bool _pendingSteamClientConnect;
    private bool _hasRequestedSteamClientStart;
    private float _pendingSteamClientConnectDeadline;

    private static bool s_loggedNetcodeMissing;
    private static bool s_loggedNetworkManagerMissing;
    private static bool s_loggedTransportMissing;

    public bool IsHosting { get; private set; }
    public string HostUserId { get; private set; } = string.Empty;
    public string CurrentJoinCode { get; private set; } = string.Empty;
    public string CurrentVoiceSecret => string.Empty;
    public bool HasJoinedLobbySession => IsLobbyNetworkConnected;
    public bool HasPendingSteamLobbyJoin { get; private set; }

    public bool HasLobbyNetworkConnectionFailed { get; private set; }
    public string LastLobbyNetworkError { get; private set; } = string.Empty;

    public void Init()
    {
    }

    public void OnUpdate()
    {
        if (Managers.Scene.CurrentScene == null || Managers.Scene.CurrentScene.SceneType != Define.Scene.Lobby)
            return;

        TryResolvePendingSteamClientConnect();
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
        ResetClientConnectionTracking();
    }

    public void SetRangerNicknameVoiceActive(string userId, bool isVoiceChatActive)
    {
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

    private void EnsureSteamCallbacks()
    {
        if (_steamCallbacksReady)
            return;

        if (!Managers.Steam.IsInitialized)
            return;

        _steamCallbacksReady = true;

        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(HandleLobbyEnter);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(HandleGameLobbyJoinRequested);
        _lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(HandleLobbyCreated);
        _lobbyMatchListResult = CallResult<LobbyMatchList_t>.Create(HandleLobbyMatchList);
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
        HasPendingSteamLobbyJoin = true;
        Managers.Scene.LoadScene(Define.Scene.Lobby);
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
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

        NetworkManager networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
        if (networkManager != null && networkManager.IsConnectedClient)
            return;

        HasLobbyNetworkConnectionFailed = true;
        LastLobbyNetworkError = $"Disconnected from lobby host. clientId={clientId}";
        Debug.LogWarning($"[Lobby] {LastLobbyNetworkError}");
    }
}
