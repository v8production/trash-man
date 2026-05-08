public class IntroScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Intro;
        _ = Managers.Input;
        Managers.UI.ShowSceneUI<UI_Intro>(nameof(UI_Intro));
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    public override void Clear()
    {
    }
}
