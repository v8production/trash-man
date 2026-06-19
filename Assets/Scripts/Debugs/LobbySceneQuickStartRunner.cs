using System.Collections;
using UnityEngine;

public class LobbySceneQuickStartRunner : MonoBehaviour
{
    [SerializeField] private bool _runOnStart = true;
    [SerializeField] private bool _editorOnly = true;
    [SerializeField] private float _initialDelaySeconds = 1f;

    private bool _started;

    private void Start()
    {
        if (!_runOnStart || _started)
            return;

        _started = true;
        StartCoroutine(RunQuickStartCoroutine());
    }

    private IEnumerator RunQuickStartCoroutine()
    {
        if (_editorOnly && !Application.isEditor)
            yield break;

        if (_initialDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_initialDelaySeconds);

        if (Managers.LobbySession.HasJoinedLobbySession || Managers.LobbySession.HasPendingSteamLobbyJoin)
            yield break;

        Managers.LobbySession.BootstrapLocalHostLobby();
    }
}
