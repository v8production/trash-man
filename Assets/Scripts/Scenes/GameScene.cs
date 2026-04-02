public class GameScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Game;
        LoadManagers();
    }

    private static void LoadManagers()
    {
        _ = Managers.Input;
    }


    public override void Clear()
    {
    }
}
