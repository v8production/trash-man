using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_GameOver : UI_Menu
{
    private const float PausedTimeScale = 0f;
    private const float RunningTimeScale = 1f;

    enum Buttons
    {
        BackToLobby,
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Time.timeScale = PausedTimeScale;
    }

    public static void ResumeGameTime()
    {
        Time.timeScale = RunningTimeScale;
    }

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));

        GetButton((int)Buttons.BackToLobby).gameObject.BindEvent(OnBackToLobbyButtonClicked);
    }

    private void OnBackToLobbyButtonClicked(PointerEventData eventData)
    {
        ResumeGameTime();
        if (!LobbyNetworkPlayer.RequestLoadLobbyFromLocalPlayer())
            Managers.Scene.LoadScene(Define.Scene.Lobby);
    }

    private void OnDestroy()
    {
        ResumeGameTime();
    }
}
