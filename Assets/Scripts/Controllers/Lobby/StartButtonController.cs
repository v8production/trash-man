using UnityEngine;

public class StartButtonController : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    [SerializeField] private float _interactionTriggerDistance = 1.5f;

    private InteractionGuideController _interactionGuideController;

    bool ILobbyWorldButtonInteractionTarget.IsInteractionFeedbackAvailable => true;
    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();

    private void Awake()
    {
        _interactionGuideController = GetComponent<InteractionGuideController>();
        if (_interactionGuideController == null)
            _interactionGuideController = GetComponentInParent<InteractionGuideController>();
    }


    private void Update()
    {
        TryHandleDirectInteraction();
    }

    private void TryHandleDirectInteraction()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (_interactionGuideController == null || !_interactionGuideController.CanInteractFromLocalView())
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame() && !Managers.Input.WasInteractKeyPressedThisFrame())
            return;

        HandleStartButtonClicked();
    }

    private bool IsWithinInteractionDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform))
            return false;

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private void HandleStartButtonClicked()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!Managers.TitanRole.CanStartGameWithAllRolesAssigned(out string roleError))
        {
            string label = string.IsNullOrWhiteSpace(roleError) ? "role requirements" : roleError;
            Managers.Toast.EnqueueMessage($"Cannot start game: {label}", 2.8f);
            return;
        }

        if (!LobbyNetworkPlayer.RequestLoadGameFromLocalPlayer())
            Managers.Scene.LoadScene(Define.Scene.Game);
    }
}
