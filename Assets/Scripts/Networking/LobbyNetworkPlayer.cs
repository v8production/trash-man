using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class LobbyNetworkPlayer : NetworkBehaviour
{
    private const string LobbyRangerPrefabName = "Ranger";
    private const string MainLobbyName = "Main Lobby";
    private const string LobbyLeftGateName = "ML_GateL";
    private const string LobbyRightGateName = "ML_GateR";
    private const string LobbyGateSpawnClipName = "LobbyGateSpawn";
    private const string LobbyGateSpawnClipPath = "Animations/LobbyGateSpawn";
    private const string LobbyGateSpawnSoundPath = "Sounds/SFXs/Lobby/Door_open00";
    private const string LobbyGateLeftDoorName = "ML_Gate_doorL";
    private const string LobbyGateRightDoorName = "ML_Gate_doorR";
    private const int UnassignedLobbySpawnIndex = -1;
    private const int FirstTitanRoleValue = (int)Define.TitanRole.Torso;
    private const int LastTitanRoleValue = (int)Define.TitanRole.RightLeg;
    private const int SeatedUpperBodyEmotionStateMask = 0xFF;
    private const int SeatedUpperBodyEmotionSequenceShift = 8;
    private const float LobbySpawnEnforcementSeconds = 2f;
    private const float LobbyOriginSnapSqrDistance = 0.25f;

    private static bool s_ignoreGameEndResultUntilNetworkReset;
    private static Define.GameEndResult s_latestLocalGameEndResult = Define.GameEndResult.None;
    private static readonly Dictionary<ulong, int> s_pendingLobbySpawnIndexesByClientId = new();

    private static readonly Vector3[] LobbySpawnPositions =
    {
        new(-5.5f, 0f, -1.5f),
        new(5.5f, 0f, -1.5f),
    };

    private static readonly Vector3[] LobbySpawnEulerAngles =
    {
        new(0f, 90f, 0f),
        new(0f, -90f, 0f),
    };

    private readonly NetworkVariable<FixedString64Bytes> _userId = new(default);
    private readonly NetworkVariable<FixedString64Bytes> _displayName = new(new FixedString64Bytes("Player"));
    private readonly NetworkVariable<int> _selectedTitanRoleMask = new(0);
    private readonly NetworkVariable<int> _activeTitanRole = new(0);
    // Packed RGBA (0xRRGGBBAA) for compatibility with NGO primitive NetworkVariable types.
    private readonly NetworkVariable<int> _rangerColorRgba = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<FixedString4096Bytes> _rangerFacePayload = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _lobbySpawnIndex = new(UnassignedLobbySpawnIndex, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _lobbyRangerAnimState = new((int)Define.RangerAnimState.Idle00, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _seatedUpperBodyEmotionEvent = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _seatedUpperBodyEmotionState = new((int)Define.RangerAnimState.Idle00, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<Vector2> _seatedLookRotation = new(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanRoleInputPayload> _roleInput = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TorsoCameraStatePayload> _torsoCameraState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanRigPosePayload> _titanPose = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _titanGauge = new(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanStatPayload> _titanStat = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<TitanAbilityStatePayload> _titanAbilityState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<GrolarStatePayload> _grolarState = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _gameEndResult = new((int)Define.GameEndResult.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private RangerController _lobbyRanger;
    private CharacterController _lobbyRangerCharacterController;
    private UI_Nickname _nicknameUI;

    private Texture2D _rangerFaceTexture;

    private Animator _remoteAnimator;
    private Vector3 _remoteLastPosition;
    private bool _remoteHasLastPosition;
    private bool _remoteWasWalking;
    private bool _remoteEmotionActive;
    private bool _remotePendingSeatedPoseSnap;
    private bool _subscribedLobbyRangerEmotion;
    private bool _subscribedLobbyRangerSitAnimation;
    private bool _subscribedLobbyRangerStandUpAnimation;
    private float _lobbySpawnEnforceUntilTime;

    private bool _submittedIdentity;
    private bool _playedInitialLobbyGateAnimation;
    private Vector2 _lastSubmittedSeatedLookRotation;

    public int SelectedTitanRoleMaskValue => NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
    public bool HasSelectedTitanRole => NormalizeTitanRoleMask(_selectedTitanRoleMask.Value) != 0;
    public int ActiveTitanRoleValue => NormalizeTitanRoleValue(_activeTitanRole.Value);
    public TitanRoleInputPayload CurrentRoleInput => _roleInput.Value;
    public TorsoCameraStatePayload CurrentTorsoCameraState => _torsoCameraState.Value;
    public TitanRigPosePayload CurrentTitanPose => _titanPose.Value;
    public int CurrentTitanGauge => _titanGauge.Value;
    public TitanAbilityStatePayload CurrentTitanAbilityState => _titanAbilityState.Value;
    public string DisplayName => GetDisplayName();
    public Texture2D RangerFaceTexture => _rangerFaceTexture;
    public static event System.Action GameRoleMappingChanged;
    public static event System.Action LobbyRolePresentationChanged;

    public static void SetPendingLobbySpawnIndex(ulong clientId, int spawnIndex)
    {
        s_pendingLobbySpawnIndexesByClientId[clientId] = NormalizeLobbySpawnIndex(spawnIndex);
    }

    public bool TryGetRangerFaceTexture(out Texture2D faceTexture)
    {
        faceTexture = null;
        if (_rangerFaceTexture != null)
        {
            faceTexture = _rangerFaceTexture;
            return true;
        }

        string payload = _rangerFacePayload.Value.ToString();
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        if (!RangerFaceTextureStore.TryCreateTextureFromPayload(payload, out Texture2D decodedFaceTexture))
            return false;

        _rangerFaceTexture = decodedFaceTexture;
        faceTexture = _rangerFaceTexture;
        return true;
    }

    private float _nextPublishLogTime;
    private const float PublishLogIntervalSeconds = 0.50f;
    private uint _torsoDrillPressCounter;
    private uint _torsoShieldPressCounter;
    private uint _torsoClawPressCounter;
    private int _seatedUpperBodyEmotionSequence;

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

        if (IsServer)
            AssignRandomLobbySpawnIndex();
        else if (_lobbySpawnIndex.Value == UnassignedLobbySpawnIndex)
            return;

        bool hadLobbyRanger = _lobbyRanger != null;
        EnsureLobbyRanger();
        ApplyOwnershipState();
        EnsureNicknameUI();

        if (IsOwner)
        {
            if (!_submittedIdentity)
            {
                _submittedIdentity = true;
                SubmitIdentityServerRpc(Managers.Steam.LocalUserId, Managers.Steam.LocalDisplayName);
            }
        }

        // Register lobby objects once we have them; remote identity updates will refresh via OnValueChanged.
        RefreshIdentityPresentation();
        if (!hadLobbyRanger && _lobbyRanger != null)
            ApplyLobbyRangerAnimationState();
        EnforceLobbyDoorSpawnDuringInitialFrames();
        PlayInitialLobbyGateAnimation();
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
        _lobbySpawnIndex.OnValueChanged += HandleLobbySpawnIndexChanged;
        _lobbyRangerAnimState.OnValueChanged += HandleLobbyRangerAnimStateChanged;
        _seatedUpperBodyEmotionEvent.OnValueChanged += HandleSeatedUpperBodyEmotionEventChanged;
        _seatedUpperBodyEmotionState.OnValueChanged += HandleSeatedUpperBodyEmotionStateChanged;
        _seatedLookRotation.OnValueChanged += HandleSeatedLookRotationChanged;

        if (isLobbyScene)
        {
            if (IsServer)
                AssignRandomLobbySpawnIndex();

            if (_lobbySpawnIndex.Value != UnassignedLobbySpawnIndex)
            {
                EnsureLobbyRanger();
                ApplyOwnershipState();
                EnsureNicknameUI();
                RefreshIdentityPresentation();

                ApplyRangerColorPresentation();
                ApplyRangerFacePresentation();
                ApplyLobbyRangerAnimationState();

                // Ensure every peer starts from the server-assigned door spawn instead of the
                // NetworkObject prefab origin before the first NetworkTransform tick arrives.
                Vector3 initial = GetInitialSpawnPosition();
                Quaternion initialRotation = GetInitialSpawnRotation();
                transform.SetPositionAndRotation(initial, initialRotation);
                if (_lobbyRanger != null)
                    _lobbyRanger.transform.SetPositionAndRotation(initial, initialRotation);
                BeginLobbySpawnEnforcement();

                PlayInitialLobbyGateAnimation();
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
        string lobbyUserId = GetLobbyUserId();
        bool preserveLobbyScene = Managers.LobbySession.ShouldPreserveLobbyUserObjectsDuringHostMigration(lobbyUserId)
            && Managers.Scene.CurrentScene != null
            && Managers.Scene.CurrentScene.SceneType == Define.Scene.Lobby;

        _userId.OnValueChanged -= HandleIdentityChanged;
        _displayName.OnValueChanged -= HandleIdentityChanged;
        _selectedTitanRoleMask.OnValueChanged -= HandleSelectedRoleChanged;
        _activeTitanRole.OnValueChanged -= HandleActiveRoleChanged;
        _rangerColorRgba.OnValueChanged -= HandleRangerColorChanged;
        _rangerFacePayload.OnValueChanged -= HandleRangerFaceChanged;
        _lobbySpawnIndex.OnValueChanged -= HandleLobbySpawnIndexChanged;
        _lobbyRangerAnimState.OnValueChanged -= HandleLobbyRangerAnimStateChanged;
        _seatedUpperBodyEmotionEvent.OnValueChanged -= HandleSeatedUpperBodyEmotionEventChanged;
        _seatedUpperBodyEmotionState.OnValueChanged -= HandleSeatedUpperBodyEmotionStateChanged;
        _seatedLookRotation.OnValueChanged -= HandleSeatedLookRotationChanged;

        if (_lobbyRanger != null && _subscribedLobbyRangerEmotion)
        {
            _lobbyRanger.EmotionRequested -= HandleLocalRangerEmotionRequested;
            _subscribedLobbyRangerEmotion = false;
        }

        if (_lobbyRanger != null && _subscribedLobbyRangerSitAnimation)
        {
            _lobbyRanger.SitAnimationRequested -= HandleLocalRangerSitAnimationRequested;
            _subscribedLobbyRangerSitAnimation = false;
        }

        if (_lobbyRanger != null && _subscribedLobbyRangerStandUpAnimation)
        {
            _lobbyRanger.StandUpAnimationRequested -= HandleLocalRangerStandUpAnimationRequested;
            _subscribedLobbyRangerStandUpAnimation = false;
        }

        if (!preserveLobbyScene && !string.IsNullOrWhiteSpace(lobbyUserId))
        {
            Managers.LobbySession.UnregisterLobbyUserObjects(lobbyUserId, _lobbyRanger, _nicknameUI);

            if (Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Lobby)
                LobbyScene.RegisterUserPartSelection(lobbyUserId, 0);
        }

        if (_nicknameUI != null && !preserveLobbyScene)
            Destroy(_nicknameUI.gameObject);

        if (_lobbyRanger != null && !preserveLobbyScene)
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
    private void SubmitTorsoCameraStateServerRpc(TorsoCameraStatePayload cameraState)
    {
        if (!cameraState.IsValid || !IsActivelyControllingRole(Define.TitanRole.Torso))
            return;

        _torsoCameraState.Value = cameraState;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRangerEmotionServerRpc(int rangerAnimStateValue)
    {
        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (rangerAnimState != Define.RangerAnimState.Idle00 && !RangerController.IsEmotionState(rangerAnimState))
            return;

        if (RangerController.IsSeatedUpperBodyEmotionState(rangerAnimState)
            && RangerController.IsSitState((Define.RangerAnimState)_lobbyRangerAnimState.Value))
        {
            PublishSeatedUpperBodyRangerEmotion(rangerAnimStateValue);
            return;
        }

        PlayRangerEmotionClientRpc(rangerAnimStateValue);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitSeatedUpperBodyRangerEmotionServerRpc(int rangerAnimStateValue, int seatedRangerAnimStateValue)
    {
        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (!RangerController.IsSeatedUpperBodyEmotionState(rangerAnimState))
            return;

        Define.RangerAnimState seatedRangerAnimState = (Define.RangerAnimState)seatedRangerAnimStateValue;
        if (RangerController.IsSitState(seatedRangerAnimState))
            _lobbyRangerAnimState.Value = seatedRangerAnimStateValue;

        PublishSeatedUpperBodyRangerEmotion(rangerAnimStateValue);
    }

    private void PublishSeatedUpperBodyRangerEmotion(int rangerAnimStateValue)
    {
        _seatedUpperBodyEmotionSequence = (_seatedUpperBodyEmotionSequence + 1) & 0xFFFF;
        _seatedUpperBodyEmotionState.Value = rangerAnimStateValue;
        _seatedUpperBodyEmotionEvent.Value = (_seatedUpperBodyEmotionSequence << SeatedUpperBodyEmotionSequenceShift)
            | (rangerAnimStateValue & SeatedUpperBodyEmotionStateMask);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRangerSitAnimationServerRpc(int rangerAnimStateValue)
    {
        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (!RangerController.IsSitState(rangerAnimState))
            return;

        _lobbyRangerAnimState.Value = rangerAnimStateValue;
        _seatedUpperBodyEmotionState.Value = (int)Define.RangerAnimState.Idle00;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitRangerStandUpAnimationServerRpc()
    {
        _lobbyRangerAnimState.Value = (int)Define.RangerAnimState.Idle00;
        _seatedUpperBodyEmotionState.Value = (int)Define.RangerAnimState.Idle00;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRangerEmotionClientRpc(int rangerAnimStateValue)
    {
        if (IsOwner)
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (rangerAnimState != Define.RangerAnimState.Idle00 && !RangerController.IsEmotionState(rangerAnimState))
            return;

        PlayRemoteRangerEmotion(rangerAnimState);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitSeatedLookRotationServerRpc(Vector2 seatedLookRotation)
    {
        _seatedLookRotation.Value = new Vector2(
            Mathf.Clamp(seatedLookRotation.x, -RangerController.SeatedHeadLookYawLimit, RangerController.SeatedHeadLookYawLimit),
            Mathf.Clamp(seatedLookRotation.y, -RangerController.SeatedHeadLookPitchLimit, RangerController.SeatedHeadLookPitchLimit));
        ApplySeatedLookRotationClientRpc(_seatedLookRotation.Value);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplySeatedLookRotationClientRpc(Vector2 seatedLookRotation)
    {
        if (IsOwner)
            return;

        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        _lobbyRanger.SetSeatedLookRotation(seatedLookRotation.x, seatedLookRotation.y);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRangerSitAnimationClientRpc(int rangerAnimStateValue)
    {
        if (IsOwner)
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)rangerAnimStateValue;
        if (!RangerController.IsSitState(rangerAnimState))
            return;

        PlayRemoteRangerSitAnimation(rangerAnimState);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRangerStandUpAnimationClientRpc()
    {
        if (IsOwner)
            return;

        PlayRemoteRangerStandUpAnimation();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void LoadGameSceneClientRpc()
    {
        ResetLocalGameEndResultState();
        ResetSpawnedRoleInputStateForSceneBoundary(resetServerNetworkValues: false);
        Managers.Scene.LoadScene(Define.Scene.Game);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void LoadLobbySceneClientRpc()
    {
        ResetLocalGameEndResultState();
        ResetSpawnedRoleInputStateForSceneBoundary(resetServerNetworkValues: false);
        PrepareSpawnedPlayersForLobbySceneReturn(randomizeServerSpawn: false);
        Managers.Scene.LoadScene(Define.Scene.Lobby);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestLoadGameForAllServerRpc()
    {
        if (!Managers.TitanRole.CanStartGameWithAllRolesAssigned(out string roleError))
        {
            InputDebug.LogWarning($"Start game request rejected: {roleError}");
            return;
        }

        RequestLoadGameForAll();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestLoadLobbyForAllServerRpc()
    {
        RequestLoadLobbyForAll();
    }

    public void SubmitLocalFaceTexture(Texture2D faceTexture)
    {
        if (!IsOwner)
            return;

        string payload = RangerFaceTextureStore.CreateFacePayload(faceTexture);
        SubmitRangerFacePayloadServerRpc(new FixedString4096Bytes(payload));
    }

    public void SubmitBoardStroke(string payload)
    {
        if (!IsOwner || string.IsNullOrWhiteSpace(payload))
            return;

        SubmitBoardStrokeServerRpc(new FixedString4096Bytes(payload));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitBoardStrokeServerRpc(FixedString4096Bytes payload)
    {
        ApplyBoardStrokeClientRpc(payload);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyBoardStrokeClientRpc(FixedString4096Bytes payload)
    {
        BoardDrawingSurface.TryApplyPayload(payload.ToString());
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

    public static Color32 ResolveBoardPenColorFromRoleMask(int roleMask)
    {
        return RgbToColor32(ResolveTitanRoleColorsFromRoleMask(roleMask).BoardPenRgb);
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
            input.TorsoShieldHeld = false;
            input.TorsoYawInput = 0f;
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

    private void ResetRoleInputStateForSceneBoundary(bool resetServerNetworkValue)
    {
        _torsoDrillPressCounter = 0;
        _torsoShieldPressCounter = 0;
        _torsoClawPressCounter = 0;

        if (resetServerNetworkValue && IsServer && IsSpawned)
            _roleInput.Value = default;
    }

    private static void ResetSpawnedRoleInputStateForSceneBoundary(bool resetServerNetworkValues)
    {
        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsSpawned)
                continue;

            player.ResetRoleInputStateForSceneBoundary(resetServerNetworkValues);
        }
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
        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        List<LobbyNetworkPlayer> spawnedPlayers = new(players.Length);
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player != null && player.IsSpawned)
                spawnedPlayers.Add(player);
        }

        return spawnedPlayers.ToArray();
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

    public static bool TryPublishLocalTorsoCameraState(TorsoCameraStatePayload cameraState)
    {
        LobbyNetworkPlayer localPlayer = FindLocalOwnedPlayer();
        if (localPlayer == null || !localPlayer.IsOwner || !localPlayer.IsSpawned)
            return false;

        if (!localPlayer.IsActivelyControllingRole(Define.TitanRole.Torso))
            return false;

        if (localPlayer._torsoCameraState.Value.Equals(cameraState))
            return true;

        localPlayer.SubmitTorsoCameraStateServerRpc(cameraState);
        return true;
    }

    public static bool TrySeedServerGameState(
        bool hasTitanPose,
        TitanRigPosePayload titanPose,
        bool hasTitanStat,
        TitanStatPayload titanStat,
        bool hasTitanGauge,
        int titanGauge,
        bool hasTitanAbilityState,
        TitanAbilityStatePayload titanAbilityState,
        bool hasGrolarState,
        GrolarStatePayload grolarState)
    {
        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsServer || !publisher.IsSpawned)
            return false;

        if (hasTitanPose)
            publisher._titanPose.Value = titanPose;

        if (hasTitanStat)
            publisher._titanStat.Value = titanStat;

        if (hasTitanGauge)
            publisher._titanGauge.Value = titanGauge;

        if (hasTitanAbilityState)
            publisher._titanAbilityState.Value = titanAbilityState;

        if (hasGrolarState)
            publisher._grolarState.Value = grolarState;

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

    public static bool TryPublishServerGameEndResult(Define.GameEndResult result)
    {
        if (result == Define.GameEndResult.None)
            return false;

        s_ignoreGameEndResultUntilNetworkReset = false;
        s_latestLocalGameEndResult = result;

        LobbyNetworkPlayer[] players = FindAllSpawnedPlayers();
        bool published = false;
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsServer || !player.IsSpawned)
                continue;

            if (player._gameEndResult.Value == (int)Define.GameEndResult.None)
                player._gameEndResult.Value = (int)result;

            player.ShowGameEndClientRpc((int)result);
            published = true;
        }

        if (!published)
            return false;

        return true;
    }

    public static bool TryGetLatestGameEndResult(out Define.GameEndResult result)
    {
        result = Define.GameEndResult.None;

        if (s_latestLocalGameEndResult != Define.GameEndResult.None)
        {
            result = s_latestLocalGameEndResult;
            return true;
        }

        LobbyNetworkPlayer publisher = FindServerPosePublisher();
        if (publisher == null || !publisher.IsSpawned)
            return false;

        result = (Define.GameEndResult)publisher._gameEndResult.Value;
        if (s_ignoreGameEndResultUntilNetworkReset)
        {
            if (result == Define.GameEndResult.None)
                s_ignoreGameEndResultUntilNetworkReset = false;
            else
                result = Define.GameEndResult.None;
        }

        return true;
    }

    public static void ResetLocalGameEndResultState()
    {
        s_ignoreGameEndResultUntilNetworkReset = true;
        s_latestLocalGameEndResult = Define.GameEndResult.None;
    }

    private static void ResetServerGameEndResults()
    {
        LobbyNetworkPlayer[] players = FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player != null && player.IsServer && player.IsSpawned)
                player._gameEndResult.Value = (int)Define.GameEndResult.None;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGameEndClientRpc(int resultValue)
    {
        s_ignoreGameEndResultUntilNetworkReset = false;
        Define.GameEndResult result = (Define.GameEndResult)resultValue;
        s_latestLocalGameEndResult = result;
        if (Managers.Scene.CurrentScene is GameScene gameScene)
            gameScene.ShowGameEndFromNetwork(result);
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

            ResetServerGameEndResults();
            ResetLocalGameEndResultState();
            ResetSpawnedRoleInputStateForSceneBoundary(resetServerNetworkValues: true);

            if (player.TryLoadGameSceneForSession())
                return true;

            player.LoadGameSceneClientRpc();
            return true;
        }

        return false;
    }

    public static bool RequestLoadGameFromLocalPlayer()
    {
        if (RequestLoadGameForAll())
            return true;

        LobbyNetworkPlayer localPlayer = FindLocalOwnedPlayer();
        if (localPlayer == null || !localPlayer.IsSpawned)
            return false;

        localPlayer.RequestLoadGameForAllServerRpc();
        return true;
    }

    public static bool RequestLoadLobbyForAll()
    {
        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsServer || !player.IsSpawned)
                continue;

            ResetServerGameEndResults();
            ResetLocalGameEndResultState();
            ResetSpawnedRoleInputStateForSceneBoundary(resetServerNetworkValues: true);
            PrepareSpawnedPlayersForLobbySceneReturn(randomizeServerSpawn: true);
            player.LoadLobbySceneClientRpc();
            return true;
        }

        return false;
    }

    public static bool RequestLoadLobbyFromLocalPlayer()
    {
        if (RequestLoadLobbyForAll())
            return true;

        LobbyNetworkPlayer localPlayer = FindLocalOwnedPlayer();
        if (localPlayer == null || !localPlayer.IsSpawned)
            return false;

        localPlayer.RequestLoadLobbyForAllServerRpc();
        return true;
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
        if (Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Lobby)
            EnsureLobbyRanger();

        UpdateRuntimeObjectName();
        UpdateLobbyRangerName();
        RefreshIdentityPresentation();
    }

    private void HandleSelectedRoleChanged(int previousValue, int newValue)
    {
        string lobbyUserId = GetLobbyUserId();
        if (!string.IsNullOrWhiteSpace(lobbyUserId))
            LobbyScene.RegisterUserPartSelection(lobbyUserId, newValue);

        RefreshRoleSelectionPresentation();
        ApplyRangerColorPresentation();
        LobbyRolePresentationChanged?.Invoke();

        if (Managers.Scene.CurrentScene != null && Managers.Scene.CurrentScene.SceneType == Define.Scene.Game)
            GameRoleMappingChanged?.Invoke();
    }

    public bool TryApplyMigratedTitanRoleMask(int titanRoleMask)
    {
        if (!IsServer || !IsOwner || !IsSpawned)
            return false;

        int normalizedMask = NormalizeTitanRoleMask(titanRoleMask);
        _selectedTitanRoleMask.Value = normalizedMask;
        _rangerColorRgba.Value = ResolveRangerColorRgbaFromRoleMask(normalizedMask);

        if (normalizedMask == 0)
        {
            _activeTitanRole.Value = 0;
            return true;
        }

        int activeRoleValue = NormalizeTitanRoleValue(_activeTitanRole.Value);
        int activeBit = activeRoleValue != 0 ? (1 << (activeRoleValue - FirstTitanRoleValue)) : 0;
        if (activeBit == 0 || (normalizedMask & activeBit) == 0)
            _activeTitanRole.Value = (int)GetFirstRoleFromMask(normalizedMask);

        return true;
    }

    public bool TrySubmitRestoredTitanRoleMask(int titanRoleMask)
    {
        if (!IsOwner || !IsSpawned)
            return false;

        int normalizedMask = NormalizeTitanRoleMask(titanRoleMask);
        if (normalizedMask == NormalizeTitanRoleMask(_selectedTitanRoleMask.Value))
            return true;

        SubmitSelectedTitanRoleMaskServerRpc(normalizedMask);
        return true;
    }

    private void HandleActiveRoleChanged(int previousValue, int newValue)
    {
        RefreshRoleSelectionPresentation();

    }

    private void HandleRangerColorChanged(int previousValue, int newValue)
    {
        ApplyRangerColorPresentation();
    }

    private void HandleRangerFaceChanged(FixedString4096Bytes previousValue, FixedString4096Bytes newValue)
    {
        ClearRangerFaceTexture();
        ApplyRangerFacePresentation();
        LobbyRolePresentationChanged?.Invoke();
    }

    private void HandleLobbySpawnIndexChanged(int previousValue, int newValue)
    {
        if (newValue == UnassignedLobbySpawnIndex)
            return;

        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        Vector3 position = GetInitialSpawnPosition();
        Quaternion rotation = GetInitialSpawnRotation();
        _lobbyRanger.transform.SetPositionAndRotation(position, rotation);

        transform.SetPositionAndRotation(position, rotation);
        BeginLobbySpawnEnforcement();

        ApplyLobbyRangerAnimationState();
        PlayInitialLobbyGateAnimation();
    }

    private void HandleLobbyRangerAnimStateChanged(int previousValue, int newValue)
    {
        ApplyLobbyRangerAnimationState();
    }

    private void HandleSeatedUpperBodyEmotionEventChanged(int previousValue, int newValue)
    {
        if (newValue == 0 || IsOwner)
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)(newValue & SeatedUpperBodyEmotionStateMask);
        if (!RangerController.IsSeatedUpperBodyEmotionState(rangerAnimState))
            return;

        PlayRemoteRangerSeatedUpperBodyEmotion(rangerAnimState);
    }

    private void HandleSeatedUpperBodyEmotionStateChanged(int previousValue, int newValue)
    {
        if (newValue == (int)Define.RangerAnimState.Idle00)
            ApplySeatedUpperBodyEmotionState();
    }

    private void ApplyLobbyRangerAnimationState()
    {
        if (_lobbyRanger == null || IsOwner)
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)_lobbyRangerAnimState.Value;
        if (RangerController.IsSitState(rangerAnimState))
        {
            PlayRemoteRangerSitAnimation(rangerAnimState);
            ApplySeatedUpperBodyEmotionState();
            return;
        }

        if (rangerAnimState == Define.RangerAnimState.Idle00 && RangerController.IsSitState(_lobbyRanger.AnimState))
            PlayRemoteRangerStandUpAnimation();
    }

    private void HandleSeatedLookRotationChanged(Vector2 previousValue, Vector2 newValue)
    {
        if (_lobbyRanger == null)
            return;

        if (IsOwner)
            return;

        _lobbyRanger.SetSeatedLookRotation(newValue.x, newValue.y);
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
        {
            _lobbyRanger.enabled = isLobbyScene;
            _lobbyRanger.SetLocalControlEnabled(isLobbyScene && IsOwner);
        }

        if (_lobbyRangerCharacterController != null)
            _lobbyRangerCharacterController.enabled = isLobbyScene && IsOwner;
    }

    private void EnsureLobbyRanger()
    {
        if (_lobbyRanger != null)
            return;

        if (Managers.LobbySession.IsPreservingHostMigrationScene)
        {
            string lobbyUserId = GetLobbyUserId();
            if (string.IsNullOrWhiteSpace(lobbyUserId))
                return;

            if (Managers.LobbySession.TryGetRegisteredLobbyUserObjects(lobbyUserId, out RangerController cachedRanger, out UI_Nickname cachedNickname)
                && cachedRanger != null)
            {
                _lobbyRanger = cachedRanger;
                _nicknameUI = cachedNickname;
                _lobbyRangerCharacterController = _lobbyRanger.GetComponent<CharacterController>();
                SubscribeLobbyRangerAnimationRequests();
                ApplyOwnershipState();
                UpdateLobbyRangerName();
                ApplyRangerColorPresentation();
                ApplyRangerFacePresentation();

                transform.SetPositionAndRotation(_lobbyRanger.transform.position, _lobbyRanger.transform.rotation);

                return;
            }
        }

        GameObject rangerObject = Managers.Resource.Instantiate(LobbyRangerPrefabName);
        if (rangerObject == null)
            return;

        rangerObject.name = $"Ranger({OwnerClientId})";
        Vector3 initial = GetInitialSpawnPosition();
        Quaternion initialRotation = GetInitialSpawnRotation();
        rangerObject.transform.SetPositionAndRotation(initial, initialRotation);

        _lobbyRanger = rangerObject.GetComponent<RangerController>();
        _lobbyRangerCharacterController = rangerObject.GetComponent<CharacterController>();
        SubscribeLobbyRangerAnimationRequests();
        ApplyOwnershipState();
        UpdateLobbyRangerName();

        ApplyRangerColorPresentation();
        ApplyRangerFacePresentation();

        // On the owner, drive the network player object's transform from the visible lobby ranger.
        // This is what remote clients will replicate and follow.
        transform.SetPositionAndRotation(initial, initialRotation);

        PlayInitialLobbyGateAnimation();
    }

    private void SubscribeLobbyRangerAnimationRequests()
    {
        if (!IsOwner || _lobbyRanger == null)
            return;

        if (!_subscribedLobbyRangerEmotion)
        {
            _lobbyRanger.EmotionRequested += HandleLocalRangerEmotionRequested;
            _subscribedLobbyRangerEmotion = true;
        }

        if (!_subscribedLobbyRangerSitAnimation)
        {
            _lobbyRanger.SitAnimationRequested += HandleLocalRangerSitAnimationRequested;
            _subscribedLobbyRangerSitAnimation = true;
        }

        if (!_subscribedLobbyRangerStandUpAnimation)
        {
            _lobbyRanger.StandUpAnimationRequested += HandleLocalRangerStandUpAnimationRequested;
            _subscribedLobbyRangerStandUpAnimation = true;
        }
    }

    private void HandleLocalRangerEmotionRequested(Define.RangerAnimState rangerAnimState)
    {
        if (!IsOwner || !IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        if (_lobbyRanger != null
            && _lobbyRanger.IsSeated
            && RangerController.IsSeatedUpperBodyEmotionState(rangerAnimState))
        {
            SubmitSeatedUpperBodyRangerEmotionServerRpc((int)rangerAnimState, (int)_lobbyRanger.AnimState);
            return;
        }

        SubmitRangerEmotionServerRpc((int)rangerAnimState);
    }

    private void HandleLocalRangerSitAnimationRequested(Define.RangerAnimState rangerAnimState)
    {
        if (!IsOwner || !IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        SubmitRangerSitAnimationServerRpc((int)rangerAnimState);
    }

    private void HandleLocalRangerStandUpAnimationRequested()
    {
        if (!IsOwner || !IsSpawned)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        SubmitRangerStandUpAnimationServerRpc();
    }

    private void ApplyRangerColorPresentation()
    {
        int roleMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        Define.TitanRoleColorSet colors = ResolveTitanRoleColorsFromRoleMask(roleMask);

        if (_lobbyRanger != null)
        {
            _lobbyRanger.ApplyNetworkedColors(
                RgbToColor32(colors.RangerBodyRgb),
                RgbToColor32(colors.RangerFaceRgb),
                true,
                colors.RangerFaceEmissive
            );
        }

        if (_nicknameUI != null)
            _nicknameUI.SetTextColor(RgbToColor32(colors.NicknameTextRgb), true);
    }

    private void ApplyRangerFacePresentation()
    {
        bool hasCustomFaceTexture = TryGetRangerFaceTexture(out Texture2D faceTexture);

        if (_lobbyRanger == null)
            return;

        if (!hasCustomFaceTexture)
        {
            _lobbyRanger.ApplyDefaultFaceTexture();
            return;
        }

        _lobbyRanger.ApplyFaceTexture(faceTexture);
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
        return PackRgb(ResolveTitanRoleColorsFromRoleMask(normalizedMask).RangerBodyRgb);
    }

    private static Define.TitanRoleColorSet ResolveTitanRoleColorsFromRoleMask(int roleMask)
    {
        int normalizedMask = NormalizeTitanRoleMask(roleMask);
        for (int i = 0; i < Define.TitanRoleColorPriority.Length; i++)
        {
            Define.TitanRole role = Define.TitanRoleColorPriority[i];
            if ((normalizedMask & RoleToMaskBit(role)) == 0)
                continue;

            int roleIndex = (int)role - FirstTitanRoleValue;
            if (roleIndex >= 0 && roleIndex < Define.TitanRoleColorTable.Length)
                return Define.TitanRoleColorTable[roleIndex];
        }

        return Define.DefaultTitanRoleColors;
    }

    private static int PackRgb(int rgb)
    {
        return ((rgb & 0xFFFFFF) << 8) | 0xFF;
    }

    private static Color32 RgbToColor32(int rgb)
    {
        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return new Color32(r, g, b, 255);
    }

    private void SyncLobbyRangerTransform()
    {
        if (_lobbyRanger == null)
            return;

        EnforceLobbyDoorSpawnDuringInitialFrames();

        if (IsOwner)
        {
            // Owner drives network transform (replicated to server/others via OwnerNetworkTransform).
            Transform rangerTransform = _lobbyRanger.transform;
            if (Time.time <= _lobbySpawnEnforceUntilTime
                && rangerTransform.position.sqrMagnitude <= LobbyOriginSnapSqrDistance)
                return;

            transform.SetPositionAndRotation(rangerTransform.position, rangerTransform.rotation);
            PublishSeatedLookRotation();
            return;
        }

        // Non-owners follow the replicated network player transform.
        Transform networkTransform = transform;
        Transform ranger = _lobbyRanger.transform;
        ranger.SetPositionAndRotation(networkTransform.position, networkTransform.rotation);
        Vector2 seatedLookRotation = _seatedLookRotation.Value;
        _lobbyRanger.SetSeatedLookRotation(seatedLookRotation.x, seatedLookRotation.y);
        UpdateRemoteRangerAnimation();
    }

    private void BeginLobbySpawnEnforcement()
    {
        _lobbySpawnEnforceUntilTime = Time.time + LobbySpawnEnforcementSeconds;
    }

    private void EnforceLobbyDoorSpawnDuringInitialFrames()
    {
        if (_lobbySpawnIndex.Value == UnassignedLobbySpawnIndex)
            return;

        if (Time.time > _lobbySpawnEnforceUntilTime)
            return;

        bool networkAtOrigin = transform.position.sqrMagnitude <= LobbyOriginSnapSqrDistance;
        bool rangerAtOrigin = _lobbyRanger != null && _lobbyRanger.transform.position.sqrMagnitude <= LobbyOriginSnapSqrDistance;
        if (!networkAtOrigin && !rangerAtOrigin)
            return;

        Vector3 position = GetInitialSpawnPosition();
        Quaternion rotation = GetInitialSpawnRotation();
        transform.SetPositionAndRotation(position, rotation);

        if (_lobbyRanger != null)
            _lobbyRanger.transform.SetPositionAndRotation(position, rotation);
    }

    private void PublishSeatedLookRotation()
    {
        if (!RangerController.IsSitState(_lobbyRanger.AnimState))
        {
            if (_lastSubmittedSeatedLookRotation == Vector2.zero)
                return;

            _lastSubmittedSeatedLookRotation = Vector2.zero;
            SubmitSeatedLookRotationServerRpc(Vector2.zero);
            return;
        }

        Vector2 seatedLookRotation = new(_lobbyRanger.SeatedLookYaw, _lobbyRanger.SeatedLookPitch);
        if ((seatedLookRotation - _lastSubmittedSeatedLookRotation).sqrMagnitude < 0.25f)
            return;

        _lastSubmittedSeatedLookRotation = seatedLookRotation;
        SubmitSeatedLookRotationServerRpc(seatedLookRotation);
    }

    private void UpdateRemoteRangerAnimation()
    {
        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        _lobbyRanger.RefreshUpperBodyEmoteLayer();

        Vector3 currentPos = _lobbyRanger.transform.position;
        if (!_remoteHasLastPosition)
        {
            _remoteHasLastPosition = true;
            _remoteLastPosition = currentPos;
            if (RangerController.IsSitState(_lobbyRanger.AnimState))
                return;

            _remoteAnimator.CrossFade(Define.RangerAnimState.Idle00.ToString(), 0.05f);
            _remoteWasWalking = false;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = (currentPos - _remoteLastPosition).magnitude / dt;
        _remoteLastPosition = currentPos;

        bool walking = speed > 0.15f;

        if (RangerController.IsSitState(_lobbyRanger.AnimState))
        {
            if (_remotePendingSeatedPoseSnap)
            {
                _remotePendingSeatedPoseSnap = false;
                _remoteLastPosition = currentPos;
                return;
            }

            return;
        }

        if (_remoteEmotionActive)
        {
            if (walking && _lobbyRanger.AnimState != Define.RangerAnimState.Emote02)
            {
                _remoteEmotionActive = false;
                _remoteWasWalking = true;
                CrossFadeRemoteRanger(Define.RangerAnimState.Walk00, 0.10f);
                return;
            }

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

        if (rangerAnimState == Define.RangerAnimState.Idle00)
        {
            _remoteEmotionActive = false;
            _remoteWasWalking = false;
            _lobbyRanger.StopUpperBodyEmoteLayer();
            _lobbyRanger.AnimState = Define.RangerAnimState.Idle00;
            return;
        }

        bool isSeated = RangerController.IsSitState(_lobbyRanger.AnimState);
        _remoteEmotionActive = !isSeated;
        if (!isSeated)
            _remoteWasWalking = false;

        _lobbyRanger.PlayReplicatedEmotion(rangerAnimState);
    }

    private void PlayRemoteRangerSeatedUpperBodyEmotion(Define.RangerAnimState rangerAnimState)
    {
        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        if (!RangerController.IsSitState(_lobbyRanger.AnimState))
        {
            Define.RangerAnimState rangerAnimStateValue = (Define.RangerAnimState)_lobbyRangerAnimState.Value;
            if (RangerController.IsSitState(rangerAnimStateValue))
                PlayRemoteRangerSitAnimation(rangerAnimStateValue);
        }

        _remoteEmotionActive = false;
        _remoteWasWalking = false;
        _lobbyRanger.PlayReplicatedSeatedUpperBodyEmotion(rangerAnimState);
    }

    private void ApplySeatedUpperBodyEmotionState()
    {
        if (_lobbyRanger == null || IsOwner)
            return;

        if (!RangerController.IsSitState(_lobbyRanger.AnimState))
            return;

        Define.RangerAnimState rangerAnimState = (Define.RangerAnimState)_seatedUpperBodyEmotionState.Value;
        if (!RangerController.IsSeatedUpperBodyEmotionState(rangerAnimState))
        {
            _lobbyRanger.StopUpperBodyEmoteLayer();
            return;
        }

        if (_lobbyRanger.IsPlayingUpperBodyEmote(rangerAnimState))
            return;

        PlayRemoteRangerSeatedUpperBodyEmotion(rangerAnimState);
    }

    private void PlayRemoteRangerSitAnimation(Define.RangerAnimState rangerAnimState)
    {
        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        _remoteEmotionActive = false;
        _remoteWasWalking = false;
        _remoteHasLastPosition = true;
        _remoteLastPosition = _lobbyRanger.transform.position;
        _remotePendingSeatedPoseSnap = true;
        _lobbyRanger.PlayReplicatedSitAnimation(rangerAnimState);
    }

    private void PlayRemoteRangerStandUpAnimation()
    {
        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        if (_remoteAnimator == null)
            _remoteAnimator = _lobbyRanger.GetComponentInChildren<Animator>(true);

        if (_remoteAnimator == null)
            return;

        _remoteEmotionActive = false;
        _remoteWasWalking = false;
        _remoteHasLastPosition = true;
        _remoteLastPosition = _lobbyRanger.transform.position;
        _remotePendingSeatedPoseSnap = false;
        _lobbyRanger.PlayReplicatedStandUpAnimation();
    }

    private void CrossFadeRemoteRanger(Define.RangerAnimState state, float transitionDuration)
    {
        if (_remoteAnimator == null)
            return;

        if (_lobbyRanger != null)
        {
            _lobbyRanger.StopUpperBodyEmoteLayer();
            _lobbyRanger.AnimState = state;
            return;
        }

        _remoteAnimator.CrossFade(state.ToString(), transitionDuration);
    }

    private void CrossFadeRemoteRanger(Define.RangerAnimState state, float transitionDuration, float normalizedTime)
    {
        if (_remoteAnimator == null)
            return;

        if (_lobbyRanger != null)
        {
            _lobbyRanger.StopUpperBodyEmoteLayer();
            _lobbyRanger.AnimState = state;
            return;
        }

        _remoteAnimator.CrossFade(state.ToString(), transitionDuration, 0, normalizedTime);
    }

    private void EnsureNicknameUI()
    {
        if (_nicknameUI != null)
            return;

        if (_lobbyRanger == null)
            EnsureLobbyRanger();

        if (_lobbyRanger == null)
            return;

        if (Managers.LobbySession.IsPreservingHostMigrationScene)
        {
            string lobbyUserId = GetLobbyUserId();
            if (!string.IsNullOrWhiteSpace(lobbyUserId)
                && Managers.LobbySession.TryGetRegisteredLobbyUserObjects(lobbyUserId, out _, out UI_Nickname cachedNickname)
                && cachedNickname != null)
            {
                _nicknameUI = cachedNickname;
                _nicknameUI.SetTarget(_lobbyRanger.transform);
                return;
            }
        }

        _nicknameUI = Managers.UI.CreateSceneUI<UI_Nickname>();
        if (_nicknameUI == null)
            return;

        _nicknameUI.SetTarget(_lobbyRanger.transform);
    }

    private void RefreshIdentityPresentation()
    {
        if (_nicknameUI != null)
        {
            _nicknameUI.SetText(GetDisplayName());
            ApplyRangerColorPresentation();
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

        int roleMask = NormalizeTitanRoleMask(_selectedTitanRoleMask.Value);
        if (roleMask == 0
            && Managers.LobbySession.IsPreservingHostMigrationScene
            && LobbyScene.TryGetRegisteredUserSelectedRoleMask(lobbyUserId, out _))
            return;

        LobbyScene.RegisterUserPartSelection(lobbyUserId, roleMask);
    }

    public void PrepareForGameScene(Transform runtimeRoot)
    {
        // Network player object is already in DontDestroyOnLoad.

        if (_nicknameUI != null)
        {
            Destroy(_nicknameUI.gameObject);
            _nicknameUI = null;
        }

        if (_lobbyRanger != null)
        {
            Destroy(_lobbyRanger.gameObject);
            _lobbyRanger = null;
            _lobbyRangerCharacterController = null;
        }

        // Netcode forbids parenting a NetworkObject under a non-NetworkObject parent.
        // This object is kept alive via DontDestroyOnLoad, so we don't need to reparent it.
        // if (runtimeRoot != null)
        //     transform.SetParent(runtimeRoot, true);

        // Keep discoverable so role/input routing can find it in GameScene.
        // (HideInHierarchy can cause FindObjectsByType fallbacks to miss it.)
        gameObject.hideFlags = HideFlags.None;
    }

    private static void PrepareSpawnedPlayersForLobbySceneReturn(bool randomizeServerSpawn)
    {
        LobbyNetworkPlayer[] players = Object.FindObjectsByType<LobbyNetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null || !player.IsSpawned)
                continue;

            player.PrepareForLobbySceneReturn(randomizeServerSpawn && player.IsServer);
        }
    }

    private void PrepareForLobbySceneReturn(bool randomizeSpawnIndex)
    {
        if (randomizeSpawnIndex)
        {
            _lobbySpawnIndex.Value = GetRandomLobbySpawnIndex();
            _lobbyRangerAnimState.Value = (int)Define.RangerAnimState.Idle00;
            _seatedLookRotation.Value = Vector2.zero;
        }

        _playedInitialLobbyGateAnimation = false;
        _remoteAnimator = null;
        _remoteHasLastPosition = false;
        _remoteWasWalking = false;
        _remoteEmotionActive = false;
        _remotePendingSeatedPoseSnap = false;
        _lastSubmittedSeatedLookRotation = Vector2.zero;

        if (_nicknameUI != null)
        {
            Destroy(_nicknameUI.gameObject);
            _nicknameUI = null;
        }

        if (_lobbyRanger != null)
        {
            Destroy(_lobbyRanger.gameObject);
            _lobbyRanger = null;
            _lobbyRangerCharacterController = null;
        }

        Vector3 position = GetInitialSpawnPosition();
        Quaternion rotation = GetInitialSpawnRotation();
        transform.SetPositionAndRotation(position, rotation);
        BeginLobbySpawnEnforcement();
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

    private void AssignRandomLobbySpawnIndex()
    {
        if (_lobbySpawnIndex.Value != UnassignedLobbySpawnIndex)
            return;

        _lobbySpawnIndex.Value = TryConsumePendingLobbySpawnIndex(OwnerClientId, out int pendingSpawnIndex)
            ? pendingSpawnIndex
            : GetRandomLobbySpawnIndex();
    }

    private static bool TryConsumePendingLobbySpawnIndex(ulong clientId, out int spawnIndex)
    {
        if (!s_pendingLobbySpawnIndexesByClientId.TryGetValue(clientId, out spawnIndex))
            return false;

        s_pendingLobbySpawnIndexesByClientId.Remove(clientId);
        spawnIndex = NormalizeLobbySpawnIndex(spawnIndex);
        return true;
    }

    public static int GetRandomLobbySpawnIndex()
    {
        return Random.Range(0, LobbySpawnPositions.Length);
    }

    public static Vector3 GetLobbySpawnPosition(int spawnIndex)
    {
        return LobbySpawnPositions[NormalizeLobbySpawnIndex(spawnIndex)];
    }

    public static Quaternion GetLobbySpawnRotation(int spawnIndex)
    {
        return Quaternion.Euler(LobbySpawnEulerAngles[NormalizeLobbySpawnIndex(spawnIndex)]);
    }

    private static int GetClosestLobbySpawnIndex(Vector3 position)
    {
        int closestIndex = 0;
        float closestSqrDistance = float.MaxValue;
        for (int i = 0; i < LobbySpawnPositions.Length; i++)
        {
            float sqrDistance = (position - LobbySpawnPositions[i]).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
                continue;

            closestIndex = i;
            closestSqrDistance = sqrDistance;
        }

        return closestIndex;
    }

    private int GetLobbySpawnIndex()
    {
        return NormalizeLobbySpawnIndex(_lobbySpawnIndex.Value);
    }

    private static int NormalizeLobbySpawnIndex(int spawnIndex)
    {
        if (spawnIndex < 0 || spawnIndex >= LobbySpawnPositions.Length)
            return 0;

        return spawnIndex;
    }

    private void PlayInitialLobbyGateAnimation()
    {
        if (_playedInitialLobbyGateAnimation || _lobbySpawnIndex.Value == UnassignedLobbySpawnIndex)
            return;

        BaseScene scene = Managers.Scene.CurrentScene;
        if (scene == null || scene.SceneType != Define.Scene.Lobby)
            return;

        string gateName = GetLobbySpawnIndex() == 0 ? LobbyLeftGateName : LobbyRightGateName;
        if (!TryFindLobbyGate(gateName, out Transform gate, out Transform leftDoor, out Transform rightDoor))
        {
            Debug.LogWarning($"[Lobby] Missing lobby spawn gate. gateName={gateName}");
            return;
        }

        AnimationClip clip = Resources.Load<AnimationClip>(LobbyGateSpawnClipPath);
        if (clip == null)
        {
            Debug.LogWarning($"[Lobby] Missing lobby gate spawn animation clip. path={LobbyGateSpawnClipPath}");
            return;
        }

        Animation animation = gate.GetComponent<Animation>();
        if (animation == null)
            animation = gate.gameObject.AddComponent<Animation>();

        AnimationClip playableClip = CreatePlayableLobbyGateClip(clip, gate, leftDoor, rightDoor);
        if (animation.GetClip(LobbyGateSpawnClipName) != null)
            animation.RemoveClip(LobbyGateSpawnClipName);

        animation.AddClip(playableClip, LobbyGateSpawnClipName);

        animation.clip = playableClip;
        animation.Stop(LobbyGateSpawnClipName);
        animation.Play(LobbyGateSpawnClipName, PlayMode.StopSameLayer);
        Managers.Sound.Play(LobbyGateSpawnSoundPath);
        _playedInitialLobbyGateAnimation = true;
    }

    private static AnimationClip CreatePlayableLobbyGateClip(AnimationClip sourceClip, Transform gate, Transform leftDoor, Transform rightDoor)
    {
        AnimationClip clip = Instantiate(sourceClip);
        clip.legacy = true;
        clip.wrapMode = WrapMode.Once;
        SetDoorCurves(clip, gate, leftDoor, 1f);
        SetDoorCurves(clip, gate, rightDoor, -1f);
        return clip;
    }

    private static void SetDoorCurves(AnimationClip clip, Transform gate, Transform door, float openYOffset)
    {
        string path = GetRelativePath(gate, door);
        Vector3 closedPosition = door.localPosition;
        clip.SetCurve(path, typeof(Transform), "localPosition.x", CreateConstantCurve(closedPosition.x));
        clip.SetCurve(path, typeof(Transform), "localPosition.y", CreateLobbyGateDoorCurve(closedPosition.y, closedPosition.y + openYOffset));
        clip.SetCurve(path, typeof(Transform), "localPosition.z", CreateConstantCurve(closedPosition.z));
    }

    private static AnimationCurve CreateLobbyGateDoorCurve(float closedY, float openY)
    {
        return new AnimationCurve(
            new Keyframe(0f, closedY),
            new Keyframe(0.5f, openY),
            new Keyframe(3.5f, openY),
            new Keyframe(4f, closedY));
    }

    private static AnimationCurve CreateConstantCurve(float value)
    {
        return new AnimationCurve(
            new Keyframe(0f, value),
            new Keyframe(4f, value));
    }

    private static bool TryFindLobbyGate(string gateName, out Transform gate, out Transform leftDoor, out Transform rightDoor)
    {
        gate = null;
        leftDoor = null;
        rightDoor = null;

        GameObject mainLobby = GameObject.Find(MainLobbyName);
        if (mainLobby == null)
            return false;

        gate = FindChildRecursive(mainLobby.transform, gateName);
        if (gate == null)
            return false;

        leftDoor = FindChildRecursive(gate, LobbyGateLeftDoorName);
        rightDoor = FindChildRecursive(gate, LobbyGateRightDoorName);
        return leftDoor != null && rightDoor != null;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        string path = target.name;
        Transform current = target.parent;

        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private Vector3 GetInitialSpawnPosition()
    {
        return LobbySpawnPositions[GetLobbySpawnIndex()];
    }

    private Quaternion GetInitialSpawnRotation()
    {
        return Quaternion.Euler(LobbySpawnEulerAngles[GetLobbySpawnIndex()]);
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
