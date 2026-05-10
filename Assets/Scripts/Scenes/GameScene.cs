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
        _gameMenuUi.gameObject.SetActive(false);
    }

    private void MapStatsToUIs()
    {
        if (_bossUi != null && _grolarController != null)
            _bossUi.SetStat(_grolarController.GetComponent<BossStat>());

        if (_titanStatUi != null && _titanController != null)
            _titanStatUi.SetStat(_titanController.GetComponent<TitanStat>());
    }

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;

        Debug.Log($"{InputDebug.Prefix} GameScene.Init SceneType={SceneType}");

        EnsureTitanRuntime();
        EnsureGrolarRuntime();
        EnsureUI();
        MapStatsToUIs();
        CleanupLobbyRangers();
        Managers.Input.SetMode(Define.InputMode.Player);
    }

    private void Update()
    {
        if (!IsEscapePressedThisFrame())
            return;

        ToggleMenuInputMode();
    }

    private void ToggleMenuInputMode()
    {
        if (Managers.Input.Mode == Define.InputMode.UI)
        {
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
        _titanController = null;
        _grolarController = null;
        _bossUi = null;
        _titanStatUi = null;
        _gameMenuUi = null;
    }
}
