using UnityEngine;

public class GameScene : BaseScene
{
    private const string TitanPrefabName = "Titan";
    private TitanController _titanController;
    private TitanRoleNetworkDriver _titanRoleDriver;

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;
        LoadManagers();
        EnsureTitanRuntime();
        Managers.Input.SetMode(Define.InputMode.Player);
    }

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

        TitanLocalRoleSwitchTester tester = _titanController.GetComponent<TitanLocalRoleSwitchTester>();
        if (tester != null)
            tester.enabled = false;

        _titanRoleDriver = _titanController.GetComponent<TitanRoleNetworkDriver>();
        if (_titanRoleDriver == null)
            _titanRoleDriver = _titanController.gameObject.AddComponent<TitanRoleNetworkDriver>();
    }

    public override void Clear()
    {
        _titanController = null;
        _titanRoleDriver = null;
    }
}
