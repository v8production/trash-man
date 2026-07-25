using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_GameMenu : UI_Scene
{
    private const int CanvasOrder = 50;
    private const string ContentLayoutName = "ContentLayout";
    private const string ReusableObjectPath = "UIs/Objects";

    private UI_Settings _settings;
    private UI_Controls _controls;
    private UI_Roles _roles;
    private readonly Dictionary<string, GameObject> _contentPanels = new();

    enum Images
    {
        Background,
    }

    enum Buttons
    {
        Settings,
        Controls,
        Roles,
        AbortMission,
        LeaveGame,
    }

    public override void Init()
    {
        base.Init();
        Managers.UI.ShowCanvas(gameObject, CanvasOrder);
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));

        BindSettings(GetContentPanel<UI_Settings>(nameof(Buttons.Settings)));
        GetButton((int)Buttons.Settings).gameObject.BindEvent(_ => ShowContentPanel(nameof(Buttons.Settings)));
        BindControls(GetContentPanel<UI_Controls>(nameof(Buttons.Controls)));
        GetButton((int)Buttons.Controls).gameObject.BindEvent(_ => ShowContentPanel(nameof(Buttons.Controls)));
        BindRoles(GetContentPanel<UI_Roles>(nameof(Buttons.Roles)));
        GetButton((int)Buttons.Roles).gameObject.BindEvent(_ => ShowContentPanel(nameof(Buttons.Roles)));
        GetButton((int)Buttons.AbortMission).gameObject.BindEvent(OnAbortMissionButtonClicked);
        GetButton((int)Buttons.LeaveGame).gameObject.BindEvent(OnLeaveGameButtonClicked);
    }

    private void OnEnable()
    {
    }

    private void OnAbortMissionButtonClicked(PointerEventData eventData)
    {
        UI_Victory.ResumeGameTime();
        UI_GameOver.ResumeGameTime();
        if (!LobbyNetworkPlayer.RequestLoadLobbyFromLocalPlayer())
            Managers.Scene.LoadScene(Define.Scene.Lobby);
    }

    private void OnLeaveGameButtonClicked(PointerEventData eventData)
    {
        Managers.LobbySession.QuitCurrentRoom();
        Managers.Scene.LoadScene(Define.Scene.Intro);
    }

    private void BindSettings(UI_Settings settings)
    {
        _settings = settings;
    }

    private void BindControls(UI_Controls controls)
    {
        _controls = controls;
    }

    private void BindRoles(UI_Roles roles)
    {
        _roles = roles;
        _roles.CaptureCurrentRoleMapping();
    }

    public void HideSubMenus()
    {
        foreach (GameObject panel in GetContentPanels())
            panel.SetActive(false);
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
        return _settings != null && _settings.gameObject.activeSelf
            || _controls != null && _controls.gameObject.activeSelf
            || _roles != null && _roles.gameObject.activeSelf;
    }

    private T GetContentPanel<T>(string contentName) where T : UI_Base
    {
        GameObject panel = GetContentPanelObject(contentName, typeof(T).Name);
        T menu = panel.GetorAddComponent<T>();
        panel.SetActive(false);
        return menu;
    }

    private GameObject GetContentPanelObject(string contentName, string prefabName)
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
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
