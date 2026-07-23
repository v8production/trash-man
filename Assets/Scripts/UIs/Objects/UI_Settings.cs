using TMPro;
using UnityEngine.UI;

public class UI_Settings : UI_Base
{
    enum Images
    {
        Background,
    }

    enum Sliders
    {
        MasterVolume,
        BGMVolume,
        SFXVolume,
        MouseSensitivity,
    }

    enum Texts
    {
        MasterVolume,
        MusicVolume,
        SFXVolume,
        MouseSensitivity,
    }

    public override void Init()
    {
        Bind<Image>(typeof(Images));
        Bind<Slider>(typeof(Sliders));
        Bind<TextMeshProUGUI>(typeof(Texts));
    }
}
