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
        DiscordConnect,
        NewGame,
        Join,
        Quit,
    }

    enum Texts
    {
        DiscordConnect,
        NewGame,
        Join,
        Quit,
    }

    public override void Init()
    {
        base.Init();
        Bind<Image>(typeof(Images));
        base.Init();
        Bind<Button>(typeof(Buttons));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetButton((int)Buttons.DiscordConnect).gameObject.BindEvent(OnDiscordConnectButtonClicked);
        GetButton((int)Buttons.NewGame).gameObject.BindEvent(OnNewGameButtonClicked);
        GetButton((int)Buttons.Join).gameObject.BindEvent(OnJoinButtonClicked);
        GetButton((int)Buttons.Quit).gameObject.BindEvent(OnQuitButtonClicked);
    }

    private void OnDiscordConnectButtonClicked(PointerEventData eventData)
    {
        Debug.Log("Discord Connect Click");
    }

    private void OnNewGameButtonClicked(PointerEventData eventData)
    {
        Debug.Log("New Game Click");
    }

    private void OnJoinButtonClicked(PointerEventData eventData)
    {
        Debug.Log("Join Click");
    }

    private void OnQuitButtonClicked(PointerEventData eventData)
    {
        Debug.Log("Quit Click");
    }
}
