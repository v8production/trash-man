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
        Join,
        Quit,
    }

    enum Texts
    {
        NewGame,
        Join,
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
        GetButton((int)Buttons.Join).gameObject.BindEvent(OnJoinButtonClicked);
        GetButton((int)Buttons.Quit).gameObject.BindEvent(OnQuitButtonClicked);
    }

    private void OnNewGameButtonClicked(PointerEventData eventData)
    {
        if (_isTransitioning)
            return;

        _isTransitioning = true;
        Managers.Scene.LoadLobbyAsHost();
    }

    private void OnJoinButtonClicked(PointerEventData eventData)
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
