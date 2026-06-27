using UnityEngine;

public class ChairController : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    [SerializeField] private float _interactionTriggerDistance = 1.5f;
    [SerializeField] private Vector3 _seatedLocalPosition = new(0.35f, 0f, 0f);
    [SerializeField] private Vector3 _seatedLocalRotation = new(0f, 90f, 0f);
    [SerializeField] private Define.RangerAnimState _rangerSitAnimation = Define.RangerAnimState.Sit00;

    private OutlineController _outlineController;

    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();
    float ILobbyWorldButtonInteractionTarget.ProximitySqrDistance => GetInteractionSqrDistance();
    int ILobbyWorldButtonInteractionTarget.InteractionPriority => 0;

    private void Awake()
    {
        _outlineController = GetComponent<OutlineController>();
        _outlineController.SetVisible(false);
    }

    private void OnEnable()
    {
        LobbyWorldButtonInteractionRegistry.Register(this);
    }

    private void OnDisable()
    {
        LobbyWorldButtonInteractionRegistry.Unregister(this);
        _outlineController.SetVisible(false);
    }

    private void Update()
    {
        RefreshHighlightVisibility();
        TryHandleDirectInteraction();
    }

    private void RefreshHighlightVisibility()
    {
        _outlineController.SetVisible(IsWithinOutlineDistance());
    }

    private bool IsWithinOutlineDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (TryGetOccupant(out _))
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        return _outlineController.IsWithinTriggerDistance(rangerTransform);
    }

    private void TryHandleDirectInteraction()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!LobbyWorldButtonInteractionRegistry.CanInteract(this))
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame() && !Managers.Input.WasInteractKeyPressedThisFrame())
            return;

        HandleChairClicked();
    }

    private bool IsWithinInteractionDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
    }

    private void HandleChairClicked()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return;

        RangerController rangerController = rangerTransform.GetComponent<RangerController>();
        if (rangerController != null)
        {
            if (rangerController.IsSeatedAt(transform))
            {
                rangerController.StandUp();
                return;
            }

            if (TryGetOccupant(out RangerController seatedRanger) && seatedRanger != rangerController)
                return;

            rangerController.Sit(transform, _seatedLocalPosition, Quaternion.Euler(_seatedLocalRotation), _rangerSitAnimation);
        }
    }

    private bool TryGetOccupant(out RangerController occupant)
    {
        RangerController[] rangers = FindObjectsByType<RangerController>();
        for (int i = 0; i < rangers.Length; i++)
        {
            RangerController ranger = rangers[i];
            if (ranger != null && ranger.IsSeatedAt(transform))
            {
                occupant = ranger;
                return true;
            }
        }

        occupant = null;
        return false;
    }
}
