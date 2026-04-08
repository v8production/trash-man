public class IntroScene : BaseScene
{
    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Intro;
        LoadManagers();
        Managers.UI.ShowSceneUI<UI_Background>(nameof(UI_Background));
        Managers.UI.ShowSceneUI<UI_Logo>(nameof(UI_Logo));
        Managers.UI.ShowSceneUI<UI_NewGame>(nameof(UI_NewGame));
        Managers.UI.ShowSceneUI<UI_Join>(nameof(UI_Join));
        Managers.UI.ShowSceneUI<UI_Quit>(nameof(UI_Quit));
        Managers.UI.ShowSceneUI<UI_DiscordConnect>(nameof(UI_DiscordConnect));
    }

    private static void LoadManagers()
    {
        _ = Managers.Input;
    }

    public override void Clear()
    {
    }
}
