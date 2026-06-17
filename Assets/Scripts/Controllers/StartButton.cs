using UnityEngine;

public class StartButton : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    [SerializeField] private float _outlineTriggerDistance = 3.5f;
    [SerializeField] private float _interactionTriggerDistance = 1.5f;

    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();
    float ILobbyWorldButtonInteractionTarget.ProximitySqrDistance => GetInteractionSqrDistance();
    int ILobbyWorldButtonInteractionTarget.InteractionPriority => 0;

    private void OnEnable()
    {
        LobbyWorldButtonInteractionRegistry.Register(this);
    }

    private void OnDisable()
    {
        LobbyWorldButtonInteractionRegistry.Unregister(this);
    }

    private void Update()
    {
        TryHandleDirectClick();
    }

    private void TryHandleDirectClick()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!LobbyWorldButtonInteractionRegistry.CanInteract(this))
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame())
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

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform))
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
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
