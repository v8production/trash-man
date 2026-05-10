using System.Collections.Generic;
using Netcode.Transports;
using Unity.Netcode;
using UnityEngine;

public class LobbySessionManager
{
    private readonly Dictionary<string, RangerController> _rangersByUserId = new();
    private readonly Dictionary<string, UI_Nickname> _nicknamesByUserId = new();

    private ulong _currentHostSteamId;

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
        if (string.IsNullOrWhiteSpace(joinCode) || !ulong.TryParse(joinCode, out ulong hostSteamId) || hostSteamId == 0)
        {
            Debug.LogWarning("[Lobby] Join failed: invalid host Steam ID.");
            return false;
        }

        CleanupExistingLobbyObjects();

        CurrentJoinCode = joinCode;
        _currentHostSteamId = hostSteamId;
        _pendingSteamClientConnect = false;
        _hasRequestedSteamClientStart = false;
        _pendingSteamClientConnectDeadline = 0f;

        if (!TryStartSteamClient(hostSteamId))
        {
            Debug.LogWarning($"[Lobby] Join failed: Steam client did not start. hostSteamId={hostSteamId}");
            return false;
        }

        _hasRequestedSteamClientStart = true;
        return true;
    }

    public void QuitCurrentRoom()
    {
        TryStopNetwork();
        _rangersByUserId.Clear();
        _nicknamesByUserId.Clear();
        IsHosting = false;
        HostUserId = string.Empty;
        CurrentJoinCode = string.Empty;
        _currentHostSteamId = 0;
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
        CurrentJoinCode = localSteamId != 0 ? localSteamId.ToString() : string.Empty;
        _currentHostSteamId = localSteamId;
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

        GUIUtility.systemCopyBuffer = CurrentJoinCode;
        Managers.Toast.EnqueueMessage("Host Steam ID is copied to clipboard.", 2.5f);
        Debug.Log($"[Lobby] Steam lobby ready. hostSteamId={_currentHostSteamId}, localHosting={IsHosting}");
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