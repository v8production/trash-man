public class IntroScene : BaseScene
{
    private const string IntroBgmPath = "Sounds/BGMs/Hero_BGM";
    private const float IntroBgmVolume = 0.3f;

    protected override void Init()
    {
        base.Init();
        SceneType = Define.Scene.Intro;
        _ = Managers.Input;
        Managers.Sound.Play(IntroBgmPath, Define.Sound.Bgm, pitch: 1.0f, volumeScale: IntroBgmVolume);
        Managers.UI.ShowSceneUI<UI_Intro>(nameof(UI_Intro));
        Managers.Input.SetMode(Define.InputMode.UI);
    }

    public override void Clear()
    {
    }
}
