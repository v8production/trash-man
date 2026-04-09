public class GameScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;
        LoadManagers();
        Managers.Input.SetMode(Define.InputMode.Player);
    }

    private static void LoadManagers()
    {
        _ = Managers.Input;
    }


    public override void Clear()
    {
    }
}
