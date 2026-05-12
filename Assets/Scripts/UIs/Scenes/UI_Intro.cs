using TMPro;
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
        Quit,
    }

    enum Texts
    {
        NewGame,
        JoinGame,
        EnterCode,
        Quit,
    }

    private bool _isTransitioning;

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.NewGame).gameObject.BindEvent(OnNewGameButtonClicked);
        GetButton((int)Buttons.JoinGame).gameObject.BindEvent(OnJoinGameCodeButtonClicked);
        GetButton((int)Buttons.EnterCode).gameObject.BindEvent(OnEnterCodeButtonClicked);
        GetButton((int)Buttons.Quit).gameObject.BindEvent(OnQuitButtonClicked);

        TextMeshProUGUI joinGameText = Get<TextMeshProUGUI>((int)Texts.JoinGame);
        if (joinGameText != null)
            joinGameText.text = "Join Game";

        TextMeshProUGUI enterCodeText = Get<TextMeshProUGUI>((int)Texts.EnterCode);
        if (enterCodeText != null)
            enterCodeText.text = "Enter Code";
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

    private void OnQuitButtonClicked(PointerEventData eventData)
    {
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
