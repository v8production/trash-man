using TMPro;
using UnityEngine;
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
        SystemSettings,
        DrawFace,
        ShowCode,
        InviteRoom,
        BackToLobby,
    }

    enum Texts
    {
        SystemSettings,
        DrawFace,
        ShowCode,
        Code,
        InviteRoom,
        BackToLobby,
    }

    private bool _isCodeVisible;
    private UI_DrawFace _drawFace;

    public bool IsDrawFaceVisible => _drawFace != null && _drawFace.gameObject.activeSelf;

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.SystemSettings).gameObject.BindEvent(OnSystemSettingsButtonClicked);
        GetButton((int)Buttons.DrawFace).gameObject.BindEvent(OnDrawFaceButtonClicked);
        GetButton((int)Buttons.ShowCode).gameObject.BindEvent(OnShowCodeButtonClicked);
        GetButton((int)Buttons.InviteRoom).gameObject.BindEvent(OnInviteRoomButtonClicked);
        GetButton((int)Buttons.BackToLobby).gameObject.BindEvent(OnBackToLobbyButtonClicked);

        ApplyJoinCodeState();
    }

    private void OnDestroy()
    {
        if (_drawFace != null)
        {
            _drawFace.Closed -= HandleDrawFaceClosed;
            Managers.Resource.Destory(_drawFace.gameObject);
            _drawFace = null;
        }
    }

    private void OnEnable()
    {
        ApplyJoinCodeState();
    }

    private void OnSystemSettingsButtonClicked(PointerEventData eventData)
    {
        Managers.Toast.EnqueueMessage("System settings UI is not ready yet.", 2.5f);
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
        _drawFace.Closed -= HandleDrawFaceClosed;
        _drawFace.Closed += HandleDrawFaceClosed;
        _drawFace.gameObject.SetActive(false);
    }

    private void HandleDrawFaceClosed()
    {
        HideDrawFace();

        gameObject.SetActive(true);
    }

    public void HideDrawFace()
    {
        if (_drawFace != null)
            _drawFace.gameObject.SetActive(false);
    }

    private void OnShowCodeButtonClicked(PointerEventData eventData)
    {
        _isCodeVisible = !_isCodeVisible;
        ApplyJoinCodeState();
    }

    private void OnInviteRoomButtonClicked(PointerEventData eventData)
    {
        if (!Managers.Discord.IsLinked)
        {
            Managers.Toast.EnqueueMessage("Connect Discord before inviting a friend.", 2.5f);
            return;
        }

        string joinCode = Managers.LobbySession.CurrentJoinCode;
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Managers.Toast.EnqueueMessage("Lobby code is not ready yet.", 2.5f);
            return;
        }

        Managers.Discord.RequestLobbyInvite(joinCode);
        Managers.Toast.EnqueueMessage($"Invite sent. Code: {joinCode}", 2.5f);
    }

    private void OnBackToLobbyButtonClicked(PointerEventData eventData)
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
