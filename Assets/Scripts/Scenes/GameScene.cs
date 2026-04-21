using UnityEngine;

public class GameScene : BaseScene
{
    private const string TitanPrefabName = "Titan";
    private TitanController _titanController;
    private TitanRoleNetworkDriver _titanRoleDriver;

    private static void LoadManagers()
    {
        _ = Managers.Input;
        _ = Managers.TitanRole;
    }

    private void EnsureTitanRuntime()
    {
        _titanController = FindAnyObjectByType<TitanController>();
        if (_titanController == null)
        {
            GameObject titanObject = Managers.Resource.Instantiate(TitanPrefabName);
            if (titanObject == null)
                return;

            _titanController = titanObject.GetComponent<TitanController>();
            if (_titanController == null)
                _titanController = titanObject.AddComponent<TitanController>();
        }

        _titanController.EnsureInitialized();

        _titanRoleDriver = _titanController.GetComponent<TitanRoleNetworkDriver>();
        if (_titanRoleDriver == null)
            _titanRoleDriver = _titanController.gameObject.AddComponent<TitanRoleNetworkDriver>();
    }

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;
        LoadManagers();
        EnsureTitanRuntime();
        CleanupLobbyRangers();
        Managers.Input.SetMode(Define.InputMode.Player);
    }

    private static void CleanupLobbyRangers()
    {
        Transform runtimeRoot = GameObject.Find("@LobbyNetworkRuntime")?.transform;

        LobbyNetworkPlayer[] players = FindObjectsByType<LobbyNetworkPlayer>();
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
        _titanRoleDriver = null;
    }
}
