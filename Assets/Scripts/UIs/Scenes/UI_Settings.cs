using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Settings : UI_Menu
{
    public event Action Closed;

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        Back,
    }

    enum Sliders
    {
        MasterVolume,
        MusicVolume,
        SFXVolume,
        MouseSensitivity,
    }

    enum Texts
    {
        Back,
        MasterVolume,
        MusicVolume,
        SFXVolume,
        MouseSensitivity,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<Slider>(typeof(Sliders));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.Back).gameObject.BindEvent(OnBackButtonClicked);
    }

    private void OnDestroy()
    {
        Closed = null;
    }

    private void OnBackButtonClicked(PointerEventData eventData)
    {
        Closed?.Invoke();
    }
}
