public class IntroScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Intro;
        LoadManagers();
        Managers.UI.ShowSceneUI<UI_Intro>(nameof(UI_Intro));
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    private static void LoadManagers()
    {
        _ = Managers.Input;
    }

    public override void Clear()
    {
    }
}
