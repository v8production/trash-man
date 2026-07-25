using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Intro : UI_Scene
{

    enum Images
    {
        Background,
        Logo,
    }

    enum Buttons
    {
        NewGame,
        JoinGame,
        EnterCode,
        Settings,
        Quit,
    }

    private bool _isTransitioning;

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.NewGame).gameObject.BindEvent(OnNewGameButtonClicked);
        GetButton((int)Buttons.JoinGame).gameObject.BindEvent(OnJoinGameCodeButtonClicked);
        GetButton((int)Buttons.EnterCode).gameObject.BindEvent(OnEnterCodeButtonClicked);
        GetButton((int)Buttons.Settings).gameObject.BindEvent(OnSettingsButtonClicked);
        GetButton((int)Buttons.Quit).gameObject.BindEvent(OnQuitButtonClicked);
    }

    private void OnNewGameButtonClicked(PointerEventData eventData)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        Managers.Scene.LoadLobbyAsHost();
    }

    private void OnJoinGameCodeButtonClicked(PointerEventData eventData)
    {
        if (_isTransitioning)
            return;

        if (!Managers.Steam.IsInitialized)
        {
            Managers.Toast.EnqueueMessage($"Steam is not initialized.\n{Managers.Steam.LastInitError}", 3f);
            return;
        }

        Managers.LobbySession.OpenSteamFriendsOverlay();
    }

    private void OnEnterCodeButtonClicked(PointerEventData eventData)
    {
        if (_isTransitioning)
            return;

        if (FindAnyObjectByType<UI_EnterCode>() != null)
            return;

        Managers.UI.ShowSceneUI<UI_EnterCode>(nameof(UI_EnterCode));
    }

    private void OnSettingsButtonClicked(PointerEventData eventData)
    {
        if (FindAnyObjectByType<UI_IntroSettings>() != null)
            return;

        Managers.UI.ShowSceneUI<UI_IntroSettings>(nameof(UI_IntroSettings));
    }

    private void OnQuitButtonClicked(PointerEventData eventData)
    {
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            EditorApplication.ExitPlaymode();
            return;
        }
#endif

        Application.Quit();
    }

    public void StartJoinTransition(string joinCode)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        Managers.Scene.LoadLobbyByCode(joinCode);
    }
}
