using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_GameOver : UI_Scene
{
    private const float PausedTimeScale = 0f;
    private const float RunningTimeScale = 1f;

    enum Buttons
    {
        BackToLobby,
    }

    public void Open()
    {
        SetAnimatorUpdateMode(AnimatorUpdateMode.UnscaledTime);
        gameObject.SetActive(true);
        Time.timeScale = PausedTimeScale;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        ResumeGameTime();
    }

    public static void ResumeGameTime()
    {
        Time.timeScale = RunningTimeScale;
    }

    public override void Init()
    {
        base.Init();
        Bind<Button>(typeof(Buttons));
        SetAnimatorUpdateMode(AnimatorUpdateMode.UnscaledTime);

        GetButton((int)Buttons.BackToLobby).gameObject.BindEvent(OnBackToLobbyButtonClicked);
    }

    private void SetAnimatorUpdateMode(AnimatorUpdateMode updateMode)
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].updateMode = updateMode;
    }

    private void OnBackToLobbyButtonClicked(PointerEventData eventData)
    {
        Close();
        if (!LobbyNetworkPlayer.RequestLoadLobbyFromLocalPlayer())
            Managers.Scene.LoadScene(Define.Scene.Lobby);
    }

    private void OnDestroy()
    {
        ResumeGameTime();
    }
}
