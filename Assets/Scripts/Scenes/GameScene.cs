using Unity.Netcode;
using UnityEngine;

public class GameScene : BaseScene
{
    private const string TitanPrefabName = "Titan";
    private const string GrolarPrefabName = "Grolar";
    private const string HpTankPrefabName = "HPTank";
    private const string GaugeTankPrefabName = "GaugeTank";
    private const string ControlsHintToastMessage = "박사: 조종이 낯설다면 ESC 메뉴의 Controls를 확인하게.";
    private const float TankSpawnIntervalSeconds = 5f;
    private const float TankSpawnRadius = 5f;
    private const float TankSpawnY = 0f;
    private static readonly Vector3 TitanSpawnPosition = new(0, 1.5f, 0);
    private static readonly Quaternion TitanSpawnRotation = Quaternion.Euler(0f, 0f, 0f);
    private static readonly Vector3 GrolarSpawnPosition = new(0, 0, 1);
    private static readonly Quaternion GrolarSpawnRotation = Quaternion.Euler(0f, 180f, 0f);
    private TitanController _titanController;
    private GrolarController _grolarController;

    private UI_Boss _bossUi;
    private UI_TitanStat _titanStatUi;
    private UI_GameMenu _gameMenuUi;
    private UI_Victory _victoryUi;
    private UI_GameOver _gameOverUi;
    private BossStat _bossStat;
    private TitanStat _titanStat;
    private bool _gameEndShown;
    private GameObject _hpTankOriginal;
    private GameObject _gaugeTankOriginal;
    private float _nextTankSpawnTime;

    private void EnsureTitanRuntime()
    {
        GameObject titanObject = Managers.Resource.Instantiate(TitanPrefabName);
        _titanController = titanObject.GetComponent<TitanController>();

        TitanRigRuntime runtime = titanObject.GetComponent<TitanRigRuntime>();
        Managers.TitanRig.Bind(runtime);
        runtime.ApplyMovementRootPose(TitanSpawnPosition, TitanSpawnRotation, zeroVelocities: true);
        runtime.ApplyMovementRootBaseRotation();
    }

    private static void RefreshGameRoleMap()
    {
        if (!Managers.TitanRole.RefreshRoleMap(requireAllRoles: false, out string error))
            Debug.LogWarning($"{InputDebug.Prefix} GameScene role map not ready during bootstrap: {error}");
    }

    private static void SeedInitialTitanNetworkState()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            return;

        if (!Managers.TitanRig.TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot))
            return;

        LobbyNetworkPlayer.TryPublishServerTitanPose(new TitanRigPosePayload(snapshot));
    }

    private void EnsureGrolarRuntime()
    {
        GameObject grolarObject = Managers.Resource.Instantiate(GrolarPrefabName);
        grolarObject.transform.SetPositionAndRotation(GrolarSpawnPosition, GrolarSpawnRotation);
        _grolarController = grolarObject.GetComponent<GrolarController>();
    }

    private void EnsureUI()
    {
        _bossUi = Managers.UI.ShowSceneUI<UI_Boss>(nameof(UI_Boss));
        _titanStatUi = Managers.UI.ShowSceneUI<UI_TitanStat>(nameof(UI_TitanStat));
        _gameMenuUi = Managers.UI.ShowSceneUI<UI_GameMenu>(nameof(UI_GameMenu));
        _victoryUi = Managers.UI.ShowSceneUI<UI_Victory>(nameof(UI_Victory));
        _gameOverUi = Managers.UI.ShowSceneUI<UI_GameOver>(nameof(UI_GameOver));
        _gameMenuUi.gameObject.SetActive(false);
        ResetGameEndPresentation();
    }

    private void MapStatsToUIs()
    {
        if (_grolarController != null)
            _bossStat = _grolarController.GetComponent<BossStat>();

        if (_titanController != null)
            _titanStat = _titanController.GetComponent<TitanStat>();

        if (_bossUi != null)
            _bossUi.SetStat(_bossStat);

        if (_titanStatUi != null)
            _titanStatUi.SetStat(_titanStat);
    }

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;
        UI_Victory.ResumeGameTime();
        UI_GameOver.ResumeGameTime();
        LobbyNetworkPlayer.ResetLocalGameEndResultState();

        Debug.Log($"{InputDebug.Prefix} GameScene.Init SceneType={SceneType}");

        CleanupLobbyRangers();
        EnsureTitanRuntime();
        RefreshGameRoleMap();
        SeedInitialTitanNetworkState();
        EnsureGrolarRuntime();
        EnsureUI();
        UI_Toast controlsHintToast = Managers.UI.ShowSceneUI<UI_Toast>();
        controlsHintToast.ShowBossMessage(ControlsHintToastMessage);
        MapStatsToUIs();
        Managers.Input.SetMode(Define.InputMode.Player);
        CacheTankPrefabs();
        _nextTankSpawnTime = Time.time + TankSpawnIntervalSeconds;
    }

    private void Update()
    {
        UpdateGameEndVisibility();

        if (_gameEndShown)
            return;

        UpdateTankSpawning();

        if (!IsEscapePressedThisFrame())
            return;

        ToggleMenuInputMode();
    }

    private void UpdateGameEndVisibility()
    {
        if (_gameEndShown)
            return;

        if (LobbyNetworkPlayer.TryGetLatestGameEndResult(out Define.GameEndResult networkResult)
            && networkResult != Define.GameEndResult.None)
        {
            ShowGameEnd(networkResult);
            return;
        }

        Define.GameEndResult result = GetLocalGameEndResult();
        if (result == Define.GameEndResult.None)
            return;

        if (IsNetworkSessionActive())
        {
            if (HasServerAuthority() && !LobbyNetworkPlayer.TryPublishServerGameEndResult(result))
                ShowGameEnd(result);

            return;
        }

        ShowGameEnd(result);
    }

    public void ShowGameEndFromNetwork(Define.GameEndResult result)
    {
        ShowGameEnd(result);
    }

    private void ShowGameEnd(Define.GameEndResult result)
    {
        if (result == Define.GameEndResult.None)
            return;

        if (_gameEndShown)
            return;

        HideGameMenu();
        if (result == Define.GameEndResult.GameOver)
            _gameOverUi.Open();
        else
            _victoryUi.Open();

        Managers.Input.SetMode(Define.InputMode.UI);
        _gameEndShown = true;
    }

    private Define.GameEndResult GetLocalGameEndResult()
    {
        if (IsHpDepleted(_titanStat))
            return Define.GameEndResult.GameOver;

        if (IsHpDepleted(_bossStat))
            return Define.GameEndResult.Victory;

        return Define.GameEndResult.None;
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

    private static bool IsHpDepleted(Stat stat)
    {
        return stat != null && stat.Hp <= 0;
    }

    private void CacheTankPrefabs()
    {
        _hpTankOriginal = Managers.Resource.Load<GameObject>($"Prefabs/{HpTankPrefabName}");
        _gaugeTankOriginal = Managers.Resource.Load<GameObject>($"Prefabs/{GaugeTankPrefabName}");
    }

    private void UpdateTankSpawning()
    {
        if (_titanController == null || Time.time < _nextTankSpawnTime)
            return;

        SpawnRandomTank();
        _nextTankSpawnTime = Time.time + TankSpawnIntervalSeconds;
    }

    private void SpawnRandomTank()
    {
        GameObject original = Random.value < 0.5f ? _hpTankOriginal : _gaugeTankOriginal;
        if (original == null)
            return;

        Poolable tank = Managers.Pool.Pop(original, transform);
        tank.transform.SetPositionAndRotation(GetRandomTankSpawnPosition(), Quaternion.identity);
    }

    private Vector3 GetRandomTankSpawnPosition()
    {
        Vector2 offset = Random.insideUnitCircle * TankSpawnRadius;
        Vector3 titanPosition = _titanController.transform.position;
        return new Vector3(titanPosition.x + offset.x, TankSpawnY, titanPosition.z + offset.y);
    }

    private void ToggleMenuInputMode()
    {
        if (Managers.Input.Mode == Define.InputMode.UI)
        {
            if (_gameMenuUi != null && _gameMenuUi.CloseActiveSubMenu())
                return;

            HideGameMenu();
            Managers.Input.SetMode(Define.InputMode.Player);
            return;
        }

        _gameMenuUi.gameObject.SetActive(true);
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private void HideGameMenu()
    {
        if (_gameMenuUi == null)
            return;

        _gameMenuUi.HideSubMenus();
        _gameMenuUi.gameObject.SetActive(false);
    }

    private void ResetGameEndPresentation()
    {
        if (_victoryUi != null)
            _victoryUi.Close();

        if (_gameOverUi != null)
            _gameOverUi.Close();

        _gameEndShown = false;
    }

    private static bool IsEscapePressedThisFrame()
    {
        return Managers.Input.WasEscapePressedThisFrame();
    }

    private static void CleanupLobbyRangers()
    {
        Transform runtimeRoot = GameObject.Find("@NetworkManager")?.transform;

        LobbyNetworkPlayer[] players = LobbyNetworkPlayer.FindAllSpawnedPlayers();
        for (int i = 0; i < players.Length; i++)
        {
            LobbyNetworkPlayer player = players[i];
            if (player == null)
                continue;

            player.PrepareForGameScene(runtimeRoot);
        }
    }

    public override void Clear()
    {
        UI_Victory.ResumeGameTime();
        UI_GameOver.ResumeGameTime();
        ResetGameEndPresentation();
        _titanController = null;
        _grolarController = null;
        _bossUi = null;
        _titanStatUi = null;
        _gameMenuUi = null;
        _victoryUi = null;
        _gameOverUi = null;
        _bossStat = null;
        _titanStat = null;
        _gameEndShown = false;
        _hpTankOriginal = null;
        _gaugeTankOriginal = null;
    }
}
