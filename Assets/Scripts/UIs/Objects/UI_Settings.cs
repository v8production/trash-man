using TMPro;
using UnityEngine;
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
        BGMVolume,
        SFXVolume,
        MouseSensitivity,
    }

    Slider _masterVolumeSlider;
    Slider _musicVolumeSlider;
    Slider _sfxVolumeSlider;
    Slider _mouseSensitivitySlider;

    public override void Init()
    {
        Bind<Image>(typeof(Images));
        Bind<Slider>(typeof(Sliders));
        Bind<TextMeshProUGUI>(typeof(Texts));

        _masterVolumeSlider = Get<Slider>((int)Sliders.MasterVolume);
        _musicVolumeSlider = Get<Slider>((int)Sliders.BGMVolume);
        _sfxVolumeSlider = Get<Slider>((int)Sliders.SFXVolume);
        _mouseSensitivitySlider = Get<Slider>((int)Sliders.MouseSensitivity);

        Data.UserSettingsData settings = Managers.Data.UserSettings;

        _masterVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.masterVolume));
        _musicVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.bgmVolume));
        _sfxVolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.sfxVolume));
        _mouseSensitivitySlider.SetValueWithoutNotify(Mathf.Clamp01(settings.mouseSensitivity));

        _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        _musicVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        _mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);

        _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        _musicVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        _mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
    }

    void OnMasterVolumeChanged(float value)
    {
        Managers.Data.UserSettings.masterVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    void OnBgmVolumeChanged(float value)
    {
        Managers.Data.UserSettings.bgmVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    void OnSfxVolumeChanged(float value)
    {
        Managers.Data.UserSettings.sfxVolume = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    void OnMouseSensitivityChanged(float value)
    {
        Managers.Data.UserSettings.mouseSensitivity = Mathf.Clamp01(value);
        SaveAndApplySettings();
    }

    void SaveAndApplySettings()
    {
        Managers.Data.SaveUserSettings();
        Managers.Sound.ApplyUserSettings(Managers.Data.UserSettings);
    }
}
