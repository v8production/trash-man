using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_LobbyMenu : UI_Scene
{
    private const string MaskedCodeText = "******";
    private const string ContentLayoutName = "ContentLayout";
    private const string ReusableObjectPath = "UIs/Objects";

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        DrawFace,
        InviteRoom,
        RoomCode,
        Settings,
        LeaveGame,
    }

    enum Texts
    {
        Code,
    }

    private bool _isCodeVisible;
    private UI_DrawFace _drawFace;
    private UI_Settings _settings;
    private readonly Dictionary<string, GameObject> _contentPanels = new();

    public bool IsDrawFaceVisible => _drawFace != null && _drawFace.gameObject.activeSelf;
    public bool IsSettingsVisible => _settings != null && _settings.gameObject.activeSelf;
    private bool IsRoomCodeVisible => IsContentPanelVisible(nameof(Buttons.RoomCode));

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        BindDrawFace(GetContentPanel<UI_DrawFace>(nameof(Buttons.DrawFace)));
        GetButton((int)Buttons.DrawFace).gameObject.BindEvent(_ => ShowContentPanel(nameof(Buttons.DrawFace)));
        GetButton((int)Buttons.InviteRoom).gameObject.BindEvent(OnInviteRoomButtonClicked);
        GetButton((int)Buttons.RoomCode).gameObject.BindEvent(OnRoomCodeButtonClicked);
        BindSettings(GetContentPanel<UI_Settings>(nameof(Buttons.Settings)));
        GetButton((int)Buttons.Settings).gameObject.BindEvent(_ => ShowContentPanel(nameof(Buttons.Settings)));
        GetButton((int)Buttons.LeaveGame).gameObject.BindEvent(OnLeaveGameButtonClicked);

        ApplyJoinCodeState();
    }

    private void OnEnable()
    {
        ApplyJoinCodeState();
    }

    private void Update()
    {
        RefreshRoomCodeVisibility();
    }

    private void BindDrawFace(UI_DrawFace drawFace)
    {
        _drawFace = drawFace;
    }

    private void BindSettings(UI_Settings settings)
    {
        _settings = settings;
    }

    public void HideSubMenus()
    {
        foreach (GameObject panel in GetContentPanels())
            panel.SetActive(false);

        RefreshRoomCodeVisibility();
    }

    public bool CloseActiveSubMenu()
    {
        if (!HasActiveSubMenu())
            return false;

        HideSubMenus();
        return true;
    }

    private bool HasActiveSubMenu()
    {
        return IsDrawFaceVisible || IsSettingsVisible || IsRoomCodeVisible;
    }

    private void OnRoomCodeButtonClicked(PointerEventData eventData)
    {
        ShowContentPanel(nameof(Buttons.RoomCode));
    }

    private T GetContentPanel<T>(string contentName) where T : UI_Base
    {
        GameObject panel = GetContentPanelObject(contentName, typeof(T).Name);
        T menu = panel.GetorAddComponent<T>();
        panel.SetActive(false);
        return menu;
    }

    private GameObject GetContentPanelObject(string contentName, string prefabName = null)
    {
        foreach (GameObject panel in GetContentPanels())
        {
            if (panel.name == contentName)
                return panel;
        }

        GameObject createdPanel = Managers.Resource.Instantiate($"{ReusableObjectPath}/{prefabName}", GetContentLayout());
        createdPanel.name = contentName;
        StretchToParent(createdPanel.transform as RectTransform);
        _contentPanels[contentName] = createdPanel;
        return createdPanel;
    }

    private IEnumerable<GameObject> GetContentPanels()
    {
        Transform contentLayout = GetContentLayout();
        for (int i = 0; i < contentLayout.childCount; i++)
        {
            GameObject panel = contentLayout.GetChild(i).gameObject;
            _contentPanels[panel.name] = panel;
        }

        return _contentPanels.Values;
    }

    private Transform GetContentLayout()
    {
        return Util.FindChild(gameObject, ContentLayoutName, true).transform;
    }

    private void ShowContentPanel(string contentName)
    {
        foreach (GameObject panel in GetContentPanels())
            panel.SetActive(panel.name == contentName);

        RefreshRoomCodeVisibility();
    }

    private bool IsContentPanelVisible(string contentName)
    {
        foreach (GameObject panel in GetContentPanels())
        {
            if (panel.name == contentName)
                return panel.activeSelf;
        }

        return false;
    }

    private void RefreshRoomCodeVisibility()
    {
        bool shouldShowCode = IsRoomCodeVisible && Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (_isCodeVisible == shouldShowCode)
            return;

        _isCodeVisible = shouldShowCode;
        ApplyJoinCodeState();
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
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
        TextMeshProUGUI codeText = Get<TextMeshProUGUI>((int)Texts.Code);

        string joinCode = Managers.LobbySession.CurrentJoinCode;
        bool canRevealCode = !string.IsNullOrWhiteSpace(joinCode);

        if (codeText != null)
            codeText.text = _isCodeVisible && canRevealCode ? joinCode : MaskedCodeText;
    }
}
