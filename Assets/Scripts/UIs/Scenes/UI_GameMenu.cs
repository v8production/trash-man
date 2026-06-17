using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_GameMenu : UI_Menu
{
    private UI_Settings _settings;
    private UI_Controls _controls;

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        Settings,
        Controls,
        TempButton,
        TempButton2,
        LeaveGame,
    }

    enum Texts
    {
        Settings,
        Controls,
        TempButton,
        TempButton2,
        LeaveGame,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.Settings).gameObject.BindEvent(OnSettingsButtonClicked);
        GetButton((int)Buttons.Controls).gameObject.BindEvent(OnControlsButtonClicked);
        GetButton((int)Buttons.TempButton).gameObject.BindEvent(OnTempButtonClicked);
        GetButton((int)Buttons.TempButton2).gameObject.BindEvent(OnTempButtonClicked);
        GetButton((int)Buttons.LeaveGame).gameObject.BindEvent(OnLeaveGameButtonClicked);
    }

    private void OnDestroy()
    {
        DestroySettingsUI();
        DestroyControlsUI();
    }

    private void OnEnable()
    {
    }

    private void OnSettingsButtonClicked(PointerEventData eventData)
    {
        EnsureSettingsUI();
        _settings.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnControlsButtonClicked(PointerEventData eventData)
    {
        EnsureControlsUI();
        _controls.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnTempButtonClicked(PointerEventData eventData)
    {
        Managers.Toast.EnqueueMessage("There is no functions on this button.", 2.5f);
    }

    private void OnLeaveGameButtonClicked(PointerEventData eventData)
    {
        Managers.LobbySession.QuitCurrentRoom();
        Managers.Scene.LoadScene(Define.Scene.Intro);
    }

    private void EnsureSettingsUI()
    {
        if (_settings != null)
            return;

        _settings = Managers.UI.ShowSceneUI<UI_Settings>(nameof(UI_Settings));
        _settings.Closed -= HandleSubMenuClosed;
        _settings.Closed += HandleSubMenuClosed;
        _settings.gameObject.SetActive(false);
    }

    private void EnsureControlsUI()
    {
        if (_controls != null)
            return;

        _controls = Managers.UI.ShowSceneUI<UI_Controls>(nameof(UI_Controls));
        _controls.Closed -= HandleSubMenuClosed;
        _controls.Closed += HandleSubMenuClosed;
        _controls.gameObject.SetActive(false);
    }

    private void HandleSubMenuClosed()
    {
        HideSubMenus();
        gameObject.SetActive(true);
    }

    public void HideSubMenus()
    {
        HideSettingsUI();
        HideControlsUI();
    }

    private void HideSettingsUI()
    {
        if (_settings == null)
            return;

        _settings.gameObject.SetActive(false);
    }

    private void HideControlsUI()
    {
        if (_controls == null)
            return;

        _controls.gameObject.SetActive(false);
    }

    private void DestroySettingsUI()
    {
        if (_settings == null)
            return;

        _settings.Closed -= HandleSubMenuClosed;
        Managers.Resource.Destory(_settings.gameObject);
        _settings = null;
    }

    private void DestroyControlsUI()
    {
        if (_controls == null)
            return;

        _controls.Closed -= HandleSubMenuClosed;
        Managers.Resource.Destory(_controls.gameObject);
        _controls = null;
    }
}
