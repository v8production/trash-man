using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class LobbyNetworkPlayer : NetworkBehaviour
{
    private const string LobbyCameraPrefabName = "Lobby_Camera";
    private const string LobbyRangerPrefabName = "Ranger";
    private const string RangerColorMaterialName = "Ranger Color_Mat";
    private const string RangerFaceMaterialName = "Ranger Face_Mat";
    private const string ImportedRangerFaceMaterialName = "Ranger_Face";
    private const int FirstTitanRoleValue = (int)Define.TitanRole.Torso;
    private const int LastTitanRoleValue = (int)Define.TitanRole.RightLeg;

    private readonly NetworkVariable<FixedString64Bytes> _userId = new(default);
    private readonly NetworkVariable<FixedString64Bytes> _displayName = new(new FixedString64Bytes("Player"));
    private readonly NetworkVariable<int> _selectedTitanRoleMask = new(0);
    private readonly NetworkVariable<int> _activeTitanRole = new(0);
    // Packed RGBA (0xRRGGBBAA) for compatibility with NGO primitive NetworkVariable types.
    private readonly NetworkVariable<int> _rangerColorRgba = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString4096Bytes> _rangerFacePayload = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanRoleInputPayload> _roleInput = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanRigPosePayload> _titanPose = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _titanGauge = new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanStatPayload> _titanStat = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanAbilityStatePayload> _titanAbilityState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<GrolarStatePayload> _grolarState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private RangerController _lobbyRanger;
    private CharacterController _lobbyRangerCharacterController;
    private UI_Nickname _nicknameUI;
    private LobbyCameraController _localCamera;

    private MaterialPropertyBlock _rangerColorPropertyBlock;
    private Texture2D _rangerFaceTexture;

    private Animator _remoteAnimator;
    private Vector3 _remoteLastPosition;
    private bool _remoteHasLastPosition;
    private bool _remoteWasWalking;
    private bool _remoteEmotionActive;
    private Define.RangerAnimState _remoteEmotionState;
    private bool _subscribedLobbyRangerEmotion;

    private bool _submittedIdentity;

    public int SelectedTitanRoleMaskValue => NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
    public bool HasSelectedTitanRole => NormalizeTitanRoleMask(_selectedTitanRoleMask.Value) != 0;
    public int ActiveTitanRoleValue => NormalizeTitanRoleValue(_activeTitanRole.Value);
    public TitanRoleInputPayload CurrentRoleInput => _roleInput.Value;
    public TitanRigPosePayload CurrentTitanPose => _titanPose.Value;
    public int CurrentTitanGauge => _titanGauge.Value;
    public TitanAbilityStatePayload CurrentTitanAbilityState => _titanAbilityState.Value;
    public string DisplayName => GetDisplayName();

    private float _nextPublishLogTime;
    private const float PublishLogIntervalSeconds = 0.50f;
    private const float AttachInputBufferSeconds = 0.20f;
    private float _attachInputBufferRemaining;
    private uint _torsoDrillPressCounter;
    private uint _torsoShieldPressCounter;
    private uint _torsoClawPressCounter;

    private void Awake()
    {
        // This NetworkBehaviour lives on the minimal Netcode player object.
    }

    private void Update()
    {
        // Handle local control switching on the render frame so we don't miss key down events.
        TryHandleLocalRoleSwitchInput();

        // Publish local titan input from the owning network player itself.
        // This keeps input flow alive even if the Titan runtime discovers the local player later.
        PublishLocalRoleInput();

        // Netcode player objects can spawn before the LobbyScene finishes initializing.
        // Ensure lobby-local objects (ranger/camera/nickname) are created once the lobby scene is actually active.
        TryEnsureLobbyLocalObjects();
    }

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        SyncLobbyRangerTransform();
    }

    private void TryEnsureLobbyLocalObjects()
    {
        if (!IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        EnsureLobbyRanger();
        ApplyOwnershipState();
        EnsureNicknameUI();

        if (IsOwner)
        {
            EnsureLocalCamera();

            if (!_submittedIdentity)
            {
                _submittedIdentity = true;
                SubmitIdentityServerRpc(Managers.Steam.LocalUserId, Managers.Steam.LocalDisplayName);
            }
        }

        // Register lobby objects once we have them; remote identity updates will refresh via OnValueChanged.
        RefreshIdentityPresentation();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // We use Unity scene loads (NGO scene management disabled), so this NetworkObject must survive
        // the Lobby -> Game transition for role/input routing.
        DontDestroyOnLoad(gameObject);

        _submittedIdentity = false;

        // Unity appends "(Clone)" to instantiated prefab names; use stable, readable names in Hierarchy.
        UpdateRuntimeObjectName();

        bool isGameScene = Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Game;
        bool isLobbyScene = Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Lobby;

        gameObject.hideFlags = HideFlags.None;
        _userId.OnValueChanged += HandleIdentityChanged;
        _displayName.OnValueChanged += HandleIdentityChanged;
        _selectedTitanRoleMask.OnValueChanged += HandleSelectedRoleChanged;
        _activeTitanRole.OnValueChanged += HandleActiveRoleChanged;
        _rangerColorRgba.OnValueChanged += HandleRangerColorChanged;
        _rangerFacePayload.OnValueChanged += HandleRangerFaceChanged;

        if (isLobbyScene)
        {
            EnsureLobbyRanger();
            ApplyOwnershipState();
            EnsureNicknameUI();
            RefreshIdentityPresentation();

            ApplyRangerColorPresentation();
            ApplyRangerFacePresentation();

            // Ensure an initial, deterministic spawn position is applied on the server.
            // This avoids a frame of (0,0,0) before the first NetworkTransform tick.
            if (IsServer)
            {
                Vector3 initial = GetInitialSpawnPosition();
                transform.SetPositionAndRotation(initial, Quaternion.identity);
                if (_lobbyRanger != null)
                    _lobbyRanger.transform.SetPositionAndRotation(initial, Quaternion.identity);
            }
        }
        else if (isGameScene)
        {
            Transform runtimeRoot = NetworkManager != null ? NetworkManager.transform : GameObject.Find("@NetworkManager")?.transform;
            PrepareForGameScene(runtimeRoot);

            // Always print once per spawn so we can verify this object exists in GameScene builds.
            Debug.Log($"{InputDebug.Prefix} OnNetworkSpawn(Game) ownerClientId={OwnerClientId} isOwner={IsOwner} selectedMask=0x{SelectedTitanRoleMaskValue:X} activeRole={ActiveTitanRoleValue}");
        }

        if (IsOwner)
        {
            if (isLobbyScene)
            {
                EnsureLocalCamera();
                SubmitIdentityServerRpc(Managers.Steam.LocalUserId, Managers.Steam.LocalDisplayName);
                SubmitLocalSavedFace();
                _submittedIdentity = true;
            }
            else if (isGameScene)
            {
                SubmitIdentityServerRpc(Managers.Steam.LocalUserId, Managers.Steam.LocalDisplayName);
                _submittedIdentity = true;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        _userId.OnValueChanged -= HandleIdentityChanged;
        _displayName.OnValueChanged -= HandleIdentityChanged;
        _selectedTitanRoleMask.OnValueChanged -= HandleSelectedRoleChanged;
        _activeTitanRole.OnValueChanged -= HandleActiveRoleChanged;
        _rangerColorRgba.OnValueChanged -= HandleRangerColorChanged;
        _rangerFacePayload.OnValueChanged -= HandleRangerFaceChanged;

        if (_lobbyRanger != null && _subscribedLobbyRangerEmotion)
        {
            _lobbyRanger.EmotionRequested -= HandleLocalRangerEmotionRequested;
            _subscribedLobbyRangerEmotion = false;
        }

        string lobbyUserId = GetLobbyUserId();
        if (!string.IsNullOrWhiteSpace(lobbyUserId))
        {
            Managers.LobbySession.UnregisterLobbyUserObjects(lobbyUserId, _lobbyRanger, _nicknameUI);
            LobbyScene.RegisterUserPartSelection(lobbyUserId, 0);
        }

        if (_nicknameUI != null)
            Destroy(_nicknameUI.gameObject);

        if (_localCamera != null)
            Destroy(_localCamera.gameObject);

        if (_lobbyRanger != null)
            Destroy(_lobbyRanger.gameObject);

        ClearRangerFaceTexture();

        base.OnNetworkDespawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitIdentityServerRpc(FixedString64Bytes userId, FixedString64Bytes displayName)
    {
        _userId.Value = userId;
        _displayName.Value = displayName;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitSelectedTitanRoleMaskServerRpc(int titanRoleMask)
    {
        InputDebug.Log($"[ServerRpc] SubmitSelectedTitanRoleMaskServerRpc from client={OwnerClientId} raw=0x{titanRoleMask:X}");
        int currentMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        int requestedMask = NormalizeTitanRoleMask(titanRoleMask);
        int addedMask = requestedMask & ~currentMask;
        int occupiedByOtherMask = GetRoleMaskSelectedByOtherPlayers();
        int acceptedMask = NormalizeTitanRoleMask(requestedMask & ~occupiedByOtherMask);

        if ((addedMask & occupiedByOtherMask) != 0)
            InputDebug.LogWarning($"Role selection rejected for client={OwnerClientId}. requested=0x{requestedMask:X}, occupiedByOther=0x{occupiedByOtherMask:X}, accepted=0x{acceptedMask:X}");

        _selectedTitanRoleMask.Value = acceptedMask;

        int normalizedMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        _rangerColorRgba.Value = ResolveRangerColorRgbaFromRoleMask(normalizedMask);

        int activeRoleValue = NormalizeTitanRoleValue(_activeTitanRole.Value);
        if (normalizedMask == 0)
        {
            _activeTitanRole.Value = 0;
            return;
        }

        int activeBit = activeRoleValue != 0 ? (1 << (activeRoleValue - FirstTitanRoleValue)) : 0;
        if (activeBit == 0 || (normalizedMask & activeBit) == 0)
            _activeTitanRole.Value = (int)GetFirstRoleFromMask(normalizedMask);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitActiveTitanRoleServerRpc(int titanRoleValue)
    {
        InputDebug.Log($"[ServerRpc] SubmitActiveTitanRoleServerRpc from client={OwnerClientId} value={titanRoleValue}");
        int normalizedRoleValue = NormalizeTitanRoleValue(titanRoleValue);
        if (normalizedRoleValue == 0)
            return;

        int mask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        int bit = 1 << (normalizedRoleValue - FirstTitanRoleValue);
        if ((mask & bit) == 0)
            return;

        _activeTitanRole.Value = normalizedRoleValue;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRoleInputServerRpc(TitanRoleInputPayload inputPayload)
    {
        _roleInput.Value = inputPayload;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRangerEmotionServerRpc(int rangerAnimStateValue)
    {
        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (!RangerController.IsEmotionState(rangerAnimState))
            return;

        PlayRangerEmotionClientRpc(rangerAnimStateValue);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRangerEmotionClientRpc(int rangerAnimStateValue)
    {
        if (IsOwner)
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (!RangerController.IsEmotionState(rangerAnimState))
            return;

        PlayRemoteRangerEmotion(rangerAnimState);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void LoadGameSceneClientRpc()
    {
        Managers.Scene.LoadScene(Define.Scene.Game);
    }

    public void SubmitLocalFaceTexture(Texture2D faceTexture)
    {
        if (!IsOwner)
            return;

        string payload = RangerFaceTextureStore.CreateFacePayload(faceTexture);
        SubmitRangerFacePayloadServerRpc(new FixedString4096Bytes(payload));
    }

    private void SubmitLocalSavedFace()
    {
        if (!IsOwner)
            return;

        SubmitRangerFacePayloadServerRpc(new FixedString4096Bytes(RangerFaceTextureStore.CreateLocalCustomFacePayload()));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRangerFacePayloadServerRpc(FixedString4096Bytes facePayload)
    {
        _rangerFacePayload.Value = facePayload;
    }

    public bool TryGetLobbyUserId(out string userId)
    {
        userId = GetLobbyUserId();
        return !string.IsNullOrWhiteSpace(userId);
    }

    public bool TryGetSelectedRole(out Define.TitanRole role)
    {
        role = Define.TitanRole.Torso;
        int mask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        if (mask == 0)
            return false;

        role = GetFirstRoleFromMask(mask);
        return true;
    }

    public bool TryGetSelectedRoleMask(out int roleMask)
    {
        roleMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        return roleMask != 0;
    }

    public bool HasSelectedTitanRoleValue(Define.TitanRole titanRole)
    {
        int bit = RoleToMaskBit(titanRole);
        if (bit == 0)
            return false;

        return (NormalizeTitanRoleMask(_selectedTitanRoleMask.Value) & bit) != 0;
    }

    public void ToggleTitanRoleSelection(Define.TitanRole titanRole)
    {
        if (!IsOwner)
            return;

        int bit = RoleToMaskBit(titanRole);
        if (bit == 0)
            return;

        int currentMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        int nextMask = NormalizeTitanRoleMask(currentMask ^ bit);
        if (nextMask == currentMask)
            return;

        SubmitSelectedTitanRoleMaskServerRpc(nextMask);
    }

    public bool IsTitanRoleSelectedByOtherPlayer(Define.TitanRole titanRole)
    {
        int bit = RoleToMaskBit(titanRole);
        if (bit == 0)
            return false;

        LobbyNetworkPlayer[] players = FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || player.OwnerClientId == OwnerClientId)
                continue;

            if ((NormalizeTitanRoleMask(player._selectedTitanRoleMask.Value) & bit) != 0)
                return true;
        }

        return false;
    }

    public bool TryGetActiveTitanRole(out Define.TitanRole role)
    {
        role = Define.TitanRole.Torso;
        int activeRoleValue = NormalizeTitanRoleValue(_activeTitanRole.Value);
        if (activeRoleValue == 0)
            return false;

        int mask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        int bit = 1 << (activeRoleValue - FirstTitanRoleValue);
        if ((mask & bit) == 0)
            return false;

        role = (Define.TitanRole)activeRoleValue;
        return true;
    }

    public bool IsActivelyControllingRole(Define.TitanRole role)
    {
        if (!TryGetActiveTitanRole(out Define.TitanRole active))
            return false;

        return active == role;
    }

    public void TryHandleLocalRoleSwitchInput()
    {
        if (!IsOwner)
            return;

        if (Managers.Scene.CurrentScene == null || Managers.Scene.CurrentScene.SceneType != Define.Scene.Game)
            return;

        TrySwitchActiveRoleFromDigit(1, Define.TitanRole.Torso);
        TrySwitchActiveRoleFromDigit(2, Define.TitanRole.LeftArm);
        TrySwitchActiveRoleFromDigit(3, Define.TitanRole.RightArm);
        TrySwitchActiveRoleFromDigit(4, Define.TitanRole.LeftLeg);
        TrySwitchActiveRoleFromDigit(5, Define.TitanRole.RightLeg);
    }

    private void TrySwitchActiveRoleFromDigit(int digit, Define.TitanRole role)
    {
        if (!Managers.Input.WasDigitPressedThisFrame(digit))
            return;

        InputDebug.Log($"Digit{digit} pressed (client={OwnerClientId}, isOwner={IsOwner}). role={role}, selectedMask=0x{SelectedTitanRoleMaskValue:X}, activeRole={ActiveTitanRoleValue}");

        if (!HasSelectedTitanRoleValue(role))
        {
            InputDebug.LogWarning($"Digit{digit} ignored: role {role} not in selectedMask (mask=0x{SelectedTitanRoleMaskValue:X}).");
            return;
        }

        InputDebug.Log($"Switching active role -> {role} (rpc)");
        SubmitActiveTitanRoleServerRpc((int)role);
    }

    public void PublishLocalRoleInput()
    {
        if (!IsOwner)
            return;

        if (Managers.Scene.CurrentScene == null || Managers.Scene.CurrentScene.SceneType != Define.Scene.Game)
            return;

        // If the user owns roles but hasn't established an active one yet, pick the first role.
        if (!TryGetActiveTitanRole(out _))
        {
            int mask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
            if (mask != 0)
                SubmitActiveTitanRoleServerRpc((int)GetFirstRoleFromMask(mask));
        }

        int selectedMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        int activeRole = NormalizeTitanRoleValue(_activeTitanRole.Value);

        if (Time.unscaledTime >= _nextPublishLogTime)
        {
            _nextPublishLogTime = Time.unscaledTime + PublishLogIntervalSeconds;
            // InputDebug.Log($"PublishLocalRoleInput (client={OwnerClientId}, isOwner={IsOwner}) selectedMask=0x{selectedMask:X}, activeRole={activeRole}");
        }

        TitanAggregatedInput currentInput = Managers.Input.CaptureTitanInput();
        if (currentInput.RightMousePressedThisFrame)
        {
            _attachInputBufferRemaining = AttachInputBufferSeconds;
        }
        else
        {
            _attachInputBufferRemaining = Mathf.Max(0f, _attachInputBufferRemaining - Time.unscaledDeltaTime);
        }

        currentInput.RightMouseAttachBuffered = currentInput.RightMouseHeld || _attachInputBufferRemaining > 0f;
        StampTorsoPressCounters(ref currentInput, activeRole == (int)Define.TitanRole.Torso);
        TitanRoleInputPayload payload = new(currentInput);
        if (_roleInput.Value.Equals(payload))
            return;

        SubmitRoleInputServerRpc(payload);
    }

    private void StampTorsoPressCounters(ref TitanAggregatedInput input, bool isTorsoActive)
    {
        if (!isTorsoActive)
        {
            input.TorsoDrillPressedThisFrame = false;
            input.TorsoShieldPressedThisFrame = false;
            input.TorsoClawPressedThisFrame = false;
        }

        if (isTorsoActive && input.TorsoDrillPressedThisFrame)
            _torsoDrillPressCounter++;

        if (isTorsoActive && input.TorsoShieldPressedThisFrame)
            _torsoShieldPressCounter++;

        if (isTorsoActive && input.TorsoClawPressedThisFrame)
            _torsoClawPressCounter++;

        input.TorsoDrillPressCounter = _torsoDrillPressCounter;
        input.TorsoShieldPressCounter = _torsoShieldPressCounter;
        input.TorsoClawPressCounter = _torsoClawPressCounter;
    }

    public static LobbyNetworkPlayer FindLocalOwnedPlayer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.SpawnManager != null)
        {
            ulong localClientId = networkManager.LocalClientId;
            var spawned = networkManager.SpawnManager.SpawnedObjectsList;
            foreach (NetworkObject obj in spawned)
            {
                if (obj == null || !obj.IsPlayerObject || obj.OwnerClientId != localClientId)
                    continue;

                if (obj.TryGetComponent(out LobbyNetworkPlayer player))
                    return player;
            }
        }

        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player != null && player.IsOwner)
                return player;
        }

        return null;
    }

    public static LobbyNetworkPlayer[] FindAllSpawnedPlayers()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.SpawnManager != null)
        {
            var spawned = networkManager.SpawnManager.SpawnedObjectsList;
            List<LobbyNetworkPlayer> result = new();
            foreach (NetworkObject obj in spawned)
            {
                if (obj == null)
                    continue;

                if (!obj.TryGetComponent(out LobbyNetworkPlayer player))
                    continue;

                if (!player.IsSpawned)
                    continue;

                result.Add(player);
            }

            // If SpawnManager is present but doesn't yet report player objects (timing / scene load edge),
            // fall back to a scene search.
            if (result.Count > 0)
                return result.ToArray();
        }

        return Object.FindObjectsByType<LobbyNetworkPlayer>();
    }

    public static bool TryPublishServerTitanPose(TitanRigPosePayload posePayload)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (publisher._titanPose.Value.Equals(posePayload))
            return true;

        publisher._titanPose.Value = posePayload;
        return true;
    }

    public static bool TryGetLatestTitanPose(out TitanRigPosePayload posePayload)
    {
        posePayload = default;

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        posePayload = publisher._titanPose.Value;
        return posePayload.IsValid;
    }

    public static bool TryPublishServerTitanGauge(int gauge)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (publisher._titanGauge.Value == gauge)
            return true;

        publisher._titanGauge.Value = gauge;
        return true;
    }

    public static bool TryPublishServerTitanStat(TitanStatPayload titanStat)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (publisher._titanStat.Value.Equals(titanStat))
            return true;

        publisher._titanStat.Value = titanStat;
        return true;
    }

    public static bool TryGetLatestTitanStat(out TitanStatPayload titanStat)
    {
        titanStat = default;

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        titanStat = publisher._titanStat.Value;
        return titanStat.BaseStat.MaxHp > 0 || titanStat.MaxGauge > 0;
    }

    public static bool TryGetLatestTitanGauge(out int gauge)
    {
        gauge = 0;

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        gauge = publisher._titanGauge.Value;
        return true;
    }

    public static bool TryPublishServerTitanAbilityState(TitanAbilityStatePayload abilityState)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (publisher._titanAbilityState.Value.Equals(abilityState))
            return true;

        publisher._titanAbilityState.Value = abilityState;
        return true;
    }

    public static bool TryGetLatestTitanAbilityState(out TitanAbilityStatePayload abilityState)
    {
        abilityState = default;

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        abilityState = publisher._titanAbilityState.Value;
        return true;
    }

    public static bool TryPublishServerGrolarState(GrolarStatePayload grolarState)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (publisher._grolarState.Value.Equals(grolarState))
            return true;

        publisher._grolarState.Value = grolarState;
        return true;
    }

    public static bool TryGetLatestGrolarState(out GrolarStatePayload grolarState)
    {
        grolarState = default;

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        grolarState = publisher._grolarState.Value;
        return grolarState.IsValid;
    }

    private static LobbyNetworkPlayer FindServerPosePublisher()
    {
        LobbyNetworkPlayer[] players = FindAllSpawnedPlayers();
        LobbyNetworkPlayer fallback = null;

        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsSpawned)
                continue;

            if (fallback == null || player.OwnerClientId < fallback.OwnerClientId)
                fallback = player;

            if (player.OwnerClientId == NetworkManager.ServerClientId)
                return player;
        }

        return fallback;
    }

    public static bool RequestLoadGameForAll()
    {
        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsServer || !player.IsSpawned)
                continue;

            if (player.TryLoadGameSceneForSession())
                return true;

            player.LoadGameSceneClientRpc();
            return true;
        }

        return false;
    }

    private bool TryLoadGameSceneForSession()
    {
        if (!IsServer || NetworkManager == null)
            return false;

        if (!NetworkManager.NetworkConfig.EnableSceneManagement || NetworkManager.SceneManager == null)
            return false;

        SceneEventProgressStatus status = NetworkManager.SceneManager.LoadScene(Util.GetEnumName(Define.Scene.Game), LoadSceneMode.Single);
        return status == SceneEventProgressStatus.Started || status == SceneEventProgressStatus.SceneEventInProgress;
    }

    private void HandleIdentityChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateRuntimeObjectName();
        UpdateLobbyRangerName();
        RefreshIdentityPresentation();
    }

    private void HandleSelectedRoleChanged(int previousValue, int newValue)
    {
        RefreshRoleSelectionPresentation();
        ApplyRangerColorPresentation();
    }

    private void HandleActiveRoleChanged(int previousValue, int newValue)
    {
        RefreshRoleSelectionPresentation();

        // Owner-side: when active role switches, reset virtual mouse baseline and detach buffer.
        // This prevents immediate pose snaps for roles that map absolute mouse position to joints (legs/arms).
        if (!IsOwner)
        {
            return;
        }

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Game)
        {
            return;
        }

        _attachInputBufferRemaining = 0f;
        Managers.Input.ResetTitanMouseBaseline();
    }

    private void HandleRangerColorChanged(int previousValue, int newValue)
    {
        ApplyRangerColorPresentation();
    }

    private void HandleRangerFaceChanged(FixedString4096Bytes previousValue, FixedString4096Bytes newValue)
    {
        ApplyRangerFacePresentation();
    }

    private void UpdateRuntimeObjectName()
    {
        string userId = GetLobbyUserId();
        string suffix = string.IsNullOrWhiteSpace(userId) ? OwnerClientId.ToString() : userId;
        gameObject.name = $"@NetworkObject({suffix})";
    }

    private void UpdateLobbyRangerName()
    {
        if (_lobbyRanger == null)
            return;

        string userId = GetLobbyUserId();
        string suffix = string.IsNullOrWhiteSpace(userId) ? OwnerClientId.ToString() : userId;
        _lobbyRanger.gameObject.name = $"Ranger({suffix})";
    }

    private void ApplyOwnershipState()
    {
        bool isLobbyScene = Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Lobby;

        if (_lobbyRanger != null)
            _lobbyRanger.enabled = isLobbyScene && IsOwner;

        if (_lobbyRangerCharacterController != null)
            _lobbyRangerCharacterController.enabled = isLobbyScene && IsOwner;
    }

    private void EnsureLobbyRanger()
    {
        if (_lobbyRanger != null)
            return;

        GameObject rangerObject = Managers.Resource.Instantiate(LobbyRangerPrefabName);
        if (rangerObject == null)
            return;

        rangerObject.name = $"Ranger({OwnerClientId})";
        Vector3 initial = GetInitialSpawnPosition();
        rangerObject.transform.SetPositionAndRotation(initial, Quaternion.identity);

        _lobbyRanger = rangerObject.GetComponent<RangerController>();
        _lobbyRangerCharacterController = rangerObject.GetComponent<CharacterController>();
        SubscribeLobbyRangerEmotion();
        ApplyOwnershipState();
        UpdateLobbyRangerName();

        ApplyRangerColorPresentation();
        ApplyRangerFacePresentation();

        // On the owner, drive the network player object's transform from the visible lobby ranger.
        // This is what remote clients will replicate and follow.
        if (IsOwner)
            transform.SetPositionAndRotation(initial, Quaternion.identity);
    }

    private void SubscribeLobbyRangerEmotion()
    {
        if (!IsOwner || _lobbyRanger == null || _subscribedLobbyRangerEmotion)
            return;

        _lobbyRanger.EmotionRequested += HandleLocalRangerEmotionRequested;
        _subscribedLobbyRangerEmotion = true;
    }

    private void HandleLocalRangerEmotionRequested(Define.RangerAnimState rangerAnimState)
    {
        if (!IsOwner || !IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        SubmitRangerEmotionServerRpc((int)rangerAnimState);
    }

    private void ApplyRangerColorPresentation()
    {
        if (_lobbyRanger == null)
            return;

        int roleMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        bool shouldApplyOverride = roleMask != 0;

        Renderer[] renderers = _lobbyRanger.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        if (_rangerColorPropertyBlock == null)
            _rangerColorPropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            // Apply only to material slots that use role-tinted Ranger materials.
            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
                continue;

            for (int m = 0; m < sharedMaterials.Length; m++)
            {
                Material mat = sharedMaterials[m];
                if (mat == null || !IsRangerRoleTintMaterial(mat))
                    continue;

                if (!shouldApplyOverride)
                {
                    // No selection: remove overrides so the material's own Color field is used.
                    renderer.SetPropertyBlock(null, m);
                    continue;
                }

                // Apply color to both common properties so builds/shaders stay consistent.
                Color color = RgbaToColor(_rangerColorRgba.Value);
                renderer.GetPropertyBlock(_rangerColorPropertyBlock, m);
                _rangerColorPropertyBlock.SetColor("_Color", color);
                _rangerColorPropertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(_rangerColorPropertyBlock, m);
            }
        }
    }

    private static bool IsRangerRoleTintMaterial(Material material)
    {
        string materialName = material.name;
        return materialName.StartsWith(RangerColorMaterialName)
            || materialName.StartsWith(RangerFaceMaterialName)
            || materialName == ImportedRangerFaceMaterialName
            || materialName.StartsWith(ImportedRangerFaceMaterialName + " ");
    }

    private void ApplyRangerFacePresentation()
    {
        if (_lobbyRanger == null)
            return;

        string payload = _rangerFacePayload.Value.ToString();
        if (string.IsNullOrWhiteSpace(payload))
        {
            ClearRangerFaceTexture();
            RangerFaceTextureStore.ApplyDefaultTo(_lobbyRanger.gameObject);
            return;
        }

        if (!RangerFaceTextureStore.TryCreateTextureFromPayload(payload, out Texture2D faceTexture))
        {
            ClearRangerFaceTexture();
            RangerFaceTextureStore.ApplyDefaultTo(_lobbyRanger.gameObject);
            return;
        }

        ClearRangerFaceTexture();
        _rangerFaceTexture = faceTexture;
        RangerFaceTextureStore.ApplyTextureTo(_lobbyRanger.gameObject, _rangerFaceTexture);
    }

    private void ClearRangerFaceTexture()
    {
        if (_rangerFaceTexture == null)
            return;

        Destroy(_rangerFaceTexture);
        _rangerFaceTexture = null;
    }

    private static int ResolveRangerColorRgbaFromRoleMask(int normalizedMask)
    {
        // No selection: keep the prefab material's default color.
        if (normalizedMask == 0)
            return 0;

        // Priority: Red > Blue > Green > Yellow > Black
        if ((normalizedMask & RoleToMaskBit(Define.TitanRole.Torso)) != 0)
            return PackRgba(220, 20, 60, 255); // Red (#DC143C)
        if ((normalizedMask & RoleToMaskBit(Define.TitanRole.RightLeg)) != 0)
            return PackRgba(0, 102, 255, 255); // Blue (#0066FF)
        if ((normalizedMask & RoleToMaskBit(Define.TitanRole.LeftLeg)) != 0)
            return PackRgba(0, 170, 60, 255); // Green (#00AA3C)
        if ((normalizedMask & RoleToMaskBit(Define.TitanRole.RightArm)) != 0)
            return PackRgba(255, 215, 0, 255); // Yellow (#FFD700)
        if ((normalizedMask & RoleToMaskBit(Define.TitanRole.LeftArm)) != 0)
            return PackRgba(30, 30, 35, 255); // Black (#1E1E23)

        return 0;
    }

    private static int PackRgba(byte r, byte g, byte b, byte a)
    {
        return (r << 24) | (g << 16) | (b << 8) | a;
    }

    private static Color RgbaToColor(int rgba)
    {
        if (rgba == 0)
            return default;

        float r = ((rgba >> 24) & 0xFF) / 255f;
        float g = ((rgba >> 16) & 0xFF) / 255f;
        float b = ((rgba >> 8) & 0xFF) / 255f;
        float a = (rgba & 0xFF) / 255f;
        return new Color(r, g, b, a);
    }

    private void SyncLobbyRangerTransform()
    {
        if (_lobbyRanger == null)
            return;

        if (IsOwner)
        {
            // Owner drives network transform (replicated to server/others via OwnerNetworkTransform).
            Transform rangerTransform = _lobbyRanger.transform;
            transform.SetPositionAndRotation(rangerTransform.position, rangerTransform.rotation);
            return;
        }

        // Non-owners follow the replicated network player transform.
        Transform networkTransform = transform;
        Transform ranger = _lobbyRanger.transform;
        ranger.SetPositionAndRotation(networkTransform.position, networkTransform.rotation);
        UpdateRemoteRangerAnimation();
    }

    private void UpdateRemoteRangerAnimation()
    {
        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        Vector3 currentPos = _lobbyRanger.transform.position;
        if (!_remoteHasLastPosition)
        {
            _remoteHasLastPosition = true;
            _remoteLastPosition = currentPos;
            _remoteAnimator.CrossFade(Define.RangerAnimState.Idle00.ToString(), 0.05f);
            _remoteWasWalking = false;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = (currentPos - _remoteLastPosition).magnitude / dt;
        _remoteLastPosition = currentPos;

        bool walking = speed > 0.15f;

        if (_remoteEmotionActive)
        {
            if (walking)
            {
                _remoteEmotionActive = false;
                _remoteWasWalking = true;
                CrossFadeRemoteRanger(Define.RangerAnimState.Walk00, 0.10f);
                return;
            }

            if (!IsRemoteRangerEmotionFinished())
                return;

            _remoteEmotionActive = false;
            _remoteWasWalking = false;
            CrossFadeRemoteRanger(Define.RangerAnimState.Idle00, 0.10f);
            return;
        }

        if (walking == _remoteWasWalking)
            return;

        _remoteWasWalking = walking;
        CrossFadeRemoteRanger(walking ? Define.RangerAnimState.Walk00 : Define.RangerAnimState.Idle00, 0.10f);
    }

    private void PlayRemoteRangerEmotion(Define.RangerAnimState rangerAnimState)
    {
        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        _remoteEmotionActive = true;
        _remoteEmotionState = rangerAnimState;
        _remoteWasWalking = false;
        CrossFadeRemoteRanger(rangerAnimState, 0.10f, 0f);
    }

    private bool IsRemoteRangerEmotionFinished()
    {
        if (_remoteAnimator == null || _remoteAnimator.IsInTransition(0))
            return false;

        AnimatorStateInfo stateInfo = _remoteAnimator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(_remoteEmotionState.ToString()) && stateInfo.normalizedTime >= 1f;
    }

    private void CrossFadeRemoteRanger(Define.RangerAnimState state, float transitionDuration)
    {
        if (_remoteAnimator == null)
            return;

        _remoteAnimator.CrossFade(state.ToString(), transitionDuration);
    }

    private void CrossFadeRemoteRanger(Define.RangerAnimState state, float transitionDuration, float normalizedTime)
    {
        if (_remoteAnimator == null)
            return;

        _remoteAnimator.CrossFade(state.ToString(), transitionDuration, 0, normalizedTime);
    }

    private void EnsureLocalCamera()
    {
        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_localCamera != null)
        {
            _localCamera.SetTarget(_lobbyRanger != null ? _lobbyRanger.transform : transform);
            return;
        }

        // Prefer reusing an authored/existing lobby camera when present.
        LobbyCameraController existingCamera = Object.FindAnyObjectByType<LobbyCameraController>();
        if (existingCamera != null)
        {
            _localCamera = existingCamera;
            _localCamera.SetTarget(_lobbyRanger != null ? _lobbyRanger.transform : transform);
            return;
        }

        GameObject cameraObject = Managers.Resource.Instantiate(LobbyCameraPrefabName);
        if (cameraObject == null)
            return;

        _localCamera = cameraObject.GetComponent<LobbyCameraController>();
        if (_localCamera != null)
            _localCamera.SetTarget(_lobbyRanger != null ? _lobbyRanger.transform : transform);
    }

    private void EnsureNicknameUI()
    {
        if (_nicknameUI != null)
            return;

        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        _nicknameUI = Managers.UI.CreateWorldSpaceUI<UI_Nickname>(_lobbyRanger.transform);
        if (_nicknameUI == null)
            return;
    }

    private void RefreshIdentityPresentation()
    {
        if (_nicknameUI != null)
        {
            _nicknameUI.SetText(GetDisplayName());
        }

        string lobbyUserId = GetLobbyUserId();
        if (!string.IsNullOrWhiteSpace(lobbyUserId))
            Managers.LobbySession.RegisterLobbyUserObjects(lobbyUserId, _lobbyRanger, _nicknameUI);

        RefreshRoleSelectionPresentation();
    }

    private void RefreshRoleSelectionPresentation()
    {
        string lobbyUserId = GetLobbyUserId();
        if (string.IsNullOrWhiteSpace(lobbyUserId))
            return;

        LobbyScene.RegisterUserPartSelection(lobbyUserId, NormalizeTitanRoleMask(_selectedTitanRoleMask.Value));
    }

    public void PrepareForGameScene(Transform runtimeRoot)
    {
        // Network player object is already in DontDestroyOnLoad.

        if (_nicknameUI != null)
            Destroy(_nicknameUI.gameObject);

        if (_localCamera != null)
            Destroy(_localCamera.gameObject);

        if (_lobbyRanger != null)
            Destroy(_lobbyRanger.gameObject);

        // Netcode forbids parenting a NetworkObject under a non-NetworkObject parent.
        // This object is kept alive via DontDestroyOnLoad, so we don't need to reparent it.
        // if (runtimeRoot != null)
        //     transform.SetParent(runtimeRoot, true);

        // Keep discoverable so role/input routing can find it in GameScene.
        // (HideInHierarchy can cause FindObjectsByType fallbacks to miss it.)
        gameObject.hideFlags = HideFlags.None;
    }

    public bool TryGetLobbyRangerTransform(out Transform rangerTransform)
    {
        rangerTransform = null;

        if (_lobbyRanger == null)
            return false;

        rangerTransform = _lobbyRanger.transform;
        return rangerTransform != null;
    }

    private string GetLobbyUserId()
    {
        string syncedUserId = _userId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(syncedUserId))
            return syncedUserId;

        return IsOwner ? Managers.Steam.LocalUserId : string.Empty;
    }

    private string GetDisplayName()
    {
        string syncedDisplayName = _displayName.Value.ToString();
        if (!string.IsNullOrWhiteSpace(syncedDisplayName))
            return syncedDisplayName;

        return IsOwner ? Managers.Steam.LocalDisplayName : $"Player {OwnerClientId}";
    }

    private Vector3 GetInitialSpawnPosition()
    {
        int slot = (int)(OwnerClientId % 4);
        return new Vector3(slot * 2.5f, 0f, 0f);
    }

    private static int NormalizeTitanRoleValue(int roleValue)
    {
        return IsValidTitanRoleValue(roleValue) ? roleValue : 0;
    }

    private static int NormalizeTitanRoleMask(int roleMask)
    {
        return roleMask & GetAllTitanRoleMask();
    }

    private static int GetAllTitanRoleMask()
    {
        int count = (LastTitanRoleValue - FirstTitanRoleValue) + 1;
        return (1 << count) - 1;
    }

    private static int RoleToMaskBit(Define.TitanRole role)
    {
        int roleValue = (int)role;
        if (!IsValidTitanRoleValue(roleValue))
            return 0;

        return 1 << (roleValue - FirstTitanRoleValue);
    }

    private static Define.TitanRole GetFirstRoleFromMask(int roleMask)
    {
        int normalized = NormalizeTitanRoleMask(roleMask);
        for (int roleValue = FirstTitanRoleValue; roleValue <= LastTitanRoleValue; roleValue++)
        {
            int bit = 1 << (roleValue - FirstTitanRoleValue);
            if ((normalized & bit) != 0)
                return (Define.TitanRole)roleValue;
        }

        return Define.TitanRole.Torso;
    }

    private int GetRoleMaskSelectedByOtherPlayers()
    {
        int occupiedMask = 0;
        LobbyNetworkPlayer[] players = FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || player.OwnerClientId == OwnerClientId)
                continue;

            occupiedMask |= NormalizeTitanRoleMask(player._selectedTitanRoleMask.Value);
        }

        return NormalizeTitanRoleMask(occupiedMask);
    }

    private static bool IsValidTitanRoleValue(int roleValue)
    {
        return roleValue >= FirstTitanRoleValue && roleValue <= LastTitanRoleValue;
    }
}
