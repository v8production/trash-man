using UnityEngine;

public class ChairController : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    [SerializeField] private float _interactionTriggerDistance = 1.5f;
    [SerializeField] private Vector3 _seatedLocalPosition = new(0.35f, 0f, 0f);
    [SerializeField] private Vector3 _seatedLocalRotation = new(0f, 90f, 0f);
    [SerializeField] private Define.RangerAnimState[] _rangerSitAnimations = { Define.RangerAnimState.Sit00 };

    private Vector3 _rangerPositionBeforeInteraction;
    private bool _hasRangerPositionBeforeInteraction;
    private Vector3 _cachedRangerPositionBeforeInteraction;
    private bool _hasCachedRangerPositionBeforeInteraction;

    bool ILobbyWorldButtonInteractionTarget.IsInteractionFeedbackAvailable => !TryGetOccupant(out _);
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
        CacheRangerPositionBeforeInteraction();
        TryHandleDirectInteraction();
    }

    private void CacheRangerPositionBeforeInteraction()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
        {
            _hasCachedRangerPositionBeforeInteraction = false;
            return;
        }

        if (Managers.Input.WasLeftMousePressedThisFrame() || Managers.Input.WasInteractKeyPressedThisFrame())
            return;

        if (TryGetOccupant(out _))
        {
            _hasCachedRangerPositionBeforeInteraction = false;
            return;
        }

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
        {
            _hasCachedRangerPositionBeforeInteraction = false;
            return;
        }

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        if ((rangerTransform.position - transform.position).sqrMagnitude > triggerDistance * triggerDistance)
        {
            _hasCachedRangerPositionBeforeInteraction = false;
            return;
        }

        _cachedRangerPositionBeforeInteraction = rangerTransform.position;
        _hasCachedRangerPositionBeforeInteraction = true;
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
                if (_hasRangerPositionBeforeInteraction)
                {
                    SmoothLobbyCameraForTeleport(rangerTransform.position);
                    rangerController.StandUp(_rangerPositionBeforeInteraction);
                    _hasRangerPositionBeforeInteraction = false;
                    _hasCachedRangerPositionBeforeInteraction = false;
                    return;
                }

                rangerController.StandUp();
                return;
            }

            if (TryGetOccupant(out RangerController seatedRanger) && seatedRanger != rangerController)
                return;

            _rangerPositionBeforeInteraction = _hasCachedRangerPositionBeforeInteraction
                ? _cachedRangerPositionBeforeInteraction
                : rangerTransform.position;
            _hasRangerPositionBeforeInteraction = true;
            Define.RangerAnimState sitAnimation = _rangerSitAnimations != null && _rangerSitAnimations.Length > 0
                ? _rangerSitAnimations[Random.Range(0, _rangerSitAnimations.Length)]
                : Define.RangerAnimState.Sit00;
            rangerController.Sit(transform, _seatedLocalPosition, Quaternion.Euler(_seatedLocalRotation), sitAnimation);
        }
    }

    private static void SmoothLobbyCameraForTeleport(Vector3 previousRangerWorldPosition)
    {
        LobbyCameraController cameraController = Object.FindAnyObjectByType<LobbyCameraController>();
        if (cameraController != null)
            cameraController.SmoothNextTargetTeleport(previousRangerWorldPosition);
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
