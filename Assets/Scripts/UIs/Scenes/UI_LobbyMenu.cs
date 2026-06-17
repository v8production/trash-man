using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_LobbyMenu : UI_Menu
{
    private const string MaskedCodeText = "******";
    private const string HiddenCodeButtonText = "Show Code";
    private const string VisibleCodeButtonText = "Hide Code";

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        DrawFace,
        InviteRoom,
        ShowCode,
        Settings,
        LeaveGame,
    }

    enum Texts
    {
        DrawFace,
        InviteRoom,
        ShowCode,
        Code,
        Settings,
        LeaveGame,
    }

    private bool _isCodeVisible;
    private UI_DrawFace _drawFace;
    private UI_Settings _settings;

    public bool IsDrawFaceVisible => _drawFace != null && _drawFace.gameObject.activeSelf;
    public bool IsSettingsVisible => _settings != null && _settings.gameObject.activeSelf;

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.DrawFace).gameObject.BindEvent(OnDrawFaceButtonClicked);
        GetButton((int)Buttons.InviteRoom).gameObject.BindEvent(OnInviteRoomButtonClicked);
        GetButton((int)Buttons.ShowCode).gameObject.BindEvent(OnShowCodeButtonClicked);
        GetButton((int)Buttons.Settings).gameObject.BindEvent(OnSettingsButtonClicked);
        GetButton((int)Buttons.LeaveGame).gameObject.BindEvent(OnLeaveGameButtonClicked);

        ApplyJoinCodeState();
    }

    private void OnDestroy()
    {
        DestroyDrawFaceUI();
        DestroySettingsUI();
    }

    private void OnEnable()
    {
        ApplyJoinCodeState();
    }

    private void OnSettingsButtonClicked(PointerEventData eventData)
    {
        EnsureSettingsUI();
        _settings.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    private void OnDrawFaceButtonClicked(PointerEventData eventData)
    {
        EnsureDrawFaceUI();
        _drawFace.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }

    private void EnsureDrawFaceUI()
    {
        if (_drawFace != null)
            return;

        _drawFace = Managers.UI.ShowSceneUI<UI_DrawFace>(nameof(UI_DrawFace));
        _drawFace.Closed -= HandleSubMenuClosed;
        _drawFace.Closed += HandleSubMenuClosed;
        _drawFace.gameObject.SetActive(false);
    }

    private void HandleSubMenuClosed()
    {
        HideSubMenus();
        gameObject.SetActive(true);
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

    public void HideSubMenus()
    {
        HideDrawFaceUI();
        HideSettingsUI();
    }

    public bool CloseActiveSubMenu()
    {
        if (!HasActiveSubMenu())
            return false;

        HideSubMenus();
        gameObject.SetActive(true);
        return true;
    }

    private bool HasActiveSubMenu()
    {
        return IsDrawFaceVisible || IsSettingsVisible;
    }

    private void HideDrawFaceUI()
    {
        if (_drawFace == null)
            return;

        _drawFace.gameObject.SetActive(false);
    }

    private void DestroyDrawFaceUI()
    {
        if (_drawFace == null)
            return;

        _drawFace.Closed -= HandleSubMenuClosed;
        Managers.Resource.Destory(_drawFace.gameObject);
        _drawFace = null;
    }

    private void HideSettingsUI()
    {
        if (_settings == null)
            return;

        _settings.gameObject.SetActive(false);
    }

    private void DestroySettingsUI()
    {
        if (_settings == null)
            return;

        _settings.Closed -= HandleSubMenuClosed;
        Managers.Resource.Destory(_settings.gameObject);
        _settings = null;
    }

    private void OnShowCodeButtonClicked(PointerEventData eventData)
    {
        _isCodeVisible = !_isCodeVisible;
        ApplyJoinCodeState();
    }

    private void OnInviteRoomButtonClicked(PointerEventData eventData)
    {
        if (!Managers.Steam.IsInitialized)
        {
            Managers.Toast.EnqueueMessage($"Steam is not initialized.\n{Managers.Steam.LastInitError}", 3f);
            return;
        }

        Managers.LobbySession.OpenSteamFriendsOverlay();
    }

    private void OnLeaveGameButtonClicked(PointerEventData eventData)
    {
        Managers.LobbySession.QuitCurrentRoom();
        Managers.Scene.LoadScene(Define.Scene.Intro);
    }

    private void ApplyJoinCodeState()
    {
        TextMeshProUGUI showCodeText = Get<TextMeshProUGUI>((int)Texts.ShowCode);
        TextMeshProUGUI codeText = Get<TextMeshProUGUI>((int)Texts.Code);

        string joinCode = Managers.LobbySession.CurrentJoinCode;
        bool canRevealCode = !string.IsNullOrWhiteSpace(joinCode);

        if (showCodeText != null)
            showCodeText.text = _isCodeVisible && canRevealCode ? VisibleCodeButtonText : HiddenCodeButtonText;

        if (codeText != null)
            codeText.text = _isCodeVisible && canRevealCode ? joinCode : MaskedCodeText;
    }
}
