using UnityEngine;
using UnityEngine.UI;

public class UI_IntroSettings : UI_Scene
{
    private const int CanvasOrder = 50;

    private UI_Settings _settings;

    enum GameObjects
    {
        Settings,
    }

    enum Buttons
    {
        Cancel,
    }

    public bool IsSettingsVisible => _settings != null && _settings.gameObject.activeSelf;

    public override void Init()
    {
        base.Init();
        Managers.UI.ShowCanvas(gameObject, CanvasOrder);
        Bind<GameObject>(typeof(GameObjects));
        Bind<Button>(typeof(Buttons));

        BindSettings(GetObject((int)GameObjects.Settings).GetorAddComponent<UI_Settings>());
        GetButton((int)Buttons.Cancel).gameObject.BindEvent(_ => Close());
    }

    private void Update()
    {
        if (Managers.Input.WasEscapePressedThisFrame())
            Close();
    }

    private void BindSettings(UI_Settings settings)
    {
        _settings = settings;
    }

    private void Close()
    {
        Destroy(gameObject);
    }
}
