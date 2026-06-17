using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_GameMenu : UI_Menu
{

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
        GetButton((int)Buttons.Settings).gameObject.BindEvent(OnControlsButtonClicked);
        GetButton((int)Buttons.TempButton).gameObject.BindEvent(OnTempButtonClicked);
        GetButton((int)Buttons.TempButton2).gameObject.BindEvent(OnTempButtonClicked);
        GetButton((int)Buttons.LeaveGame).gameObject.BindEvent(OnLeaveGameButtonClicked);
    }

    private void OnDestroy()
    {
    }

    private void OnEnable()
    {
    }

    private void OnSettingsButtonClicked(PointerEventData eventData)
    {
        Managers.Toast.EnqueueMessage("System settings UI is not ready yet.", 2.5f);
    }

    private void OnControlsButtonClicked(PointerEventData eventData)
    {
        Managers.Toast.EnqueueMessage("Controls UI is not ready yet.", 2.5f);
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
}
