using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameScene : BaseScene
{
    private const string TitanPrefabName = "Titan";
    private const string GrolarPrefabName = "Grolar";
    private static readonly Vector3 TitanSpawnPosition = new(0, 1.5f, 0);
    private static readonly Quaternion TitanSpawnRotation = Quaternion.Euler(0f, 0f, 0f);
    private static readonly Vector3 GrolarSpawnPosition = new(0, 0, 1);
    private static readonly Quaternion GrolarSpawnRotation = Quaternion.Euler(0f, 180f, 0f);
    private TitanController _titanController;
    private GrolarController _grolarController;

    private UI_Boss _bossUi;
    private UI_TitanStat _titanStatUi;
    private UI_GameMenu _gameMenuUi;
    private UI_RoleMapping _roleMappingUi;
    private UI_Victory _victoryUi;
    private UI_GameOver _gameOverUi;
    private BossStat _bossStat;
    private TitanStat _titanStat;
    private bool _gameEndShown;

    private void EnsureTitanRuntime()
    {
        GameObject titanObject = Managers.Resource.Instantiate(TitanPrefabName);
        titanObject.transform.SetPositionAndRotation(TitanSpawnPosition, TitanSpawnRotation);

        _titanController = titanObject.GetComponent<TitanController>();
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
        _roleMappingUi = Managers.UI.ShowSceneUI<UI_RoleMapping>(nameof(UI_RoleMapping));
        _victoryUi = Managers.UI.ShowSceneUI<UI_Victory>(nameof(UI_Victory));
        _gameOverUi = Managers.UI.ShowSceneUI<UI_GameOver>(nameof(UI_GameOver));
        _gameMenuUi.gameObject.SetActive(false);
        _roleMappingUi.gameObject.SetActive(false);
        _victoryUi.gameObject.SetActive(false);
        _gameOverUi.gameObject.SetActive(false);
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

        Debug.Log($"{InputDebug.Prefix} GameScene.Init SceneType={SceneType}");

        EnsureTitanRuntime();
        EnsureGrolarRuntime();
        EnsureUI();
        MapStatsToUIs();
        CleanupLobbyRangers();
        _roleMappingUi.CaptureCurrentRoleMapping();
        Managers.Input.SetMode(Define.InputMode.Player);
    }

    private void Update()
    {
        UpdateGameEndVisibility();

        if (_gameEndShown)
            return;

        UpdateRoleMappingVisibility();

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

        Managers.UI.HideAllMenuUIs();
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

    private void UpdateRoleMappingVisibility()
    {
        if (_roleMappingUi == null)
            return;

        bool shouldShow = IsTabPressed();
        if (_roleMappingUi.gameObject.activeSelf == shouldShow)
            return;

        _roleMappingUi.gameObject.SetActive(shouldShow);
    }

    private void ToggleMenuInputMode()
    {
        if (Managers.Input.Mode == Define.InputMode.UI)
        {
            if (_gameMenuUi != null && _gameMenuUi.CloseActiveSubMenu())
                return;

            Managers.UI.HideAllMenuUIs();
            Managers.Input.SetMode(Define.InputMode.Player);
            return;
        }

        _gameMenuUi.gameObject.SetActive(true);
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private static bool IsEscapePressedThisFrame()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private static bool IsTabPressed()
    {
        return Keyboard.current != null && Keyboard.current.tabKey.isPressed;
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
        _titanController = null;
        _grolarController = null;
        _bossUi = null;
        _titanStatUi = null;
        _gameMenuUi = null;
        _roleMappingUi = null;
        _victoryUi = null;
        _gameOverUi = null;
        _bossStat = null;
        _titanStat = null;
        _gameEndShown = false;
    }
}
