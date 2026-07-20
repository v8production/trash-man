using UnityEngine;

public class ChairController : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    [SerializeField] private float _interactionTriggerDistance = 1.5f;
    [SerializeField] private Vector3 _seatedLocalPosition = new(0.35f, 0f, 0f);
    [SerializeField] private Vector3 _seatedLocalRotation = new(0f, 90f, 0f);
    [SerializeField] private Define.RangerAnimState[] _rangerSitAnimations = { Define.RangerAnimState.Sit00 };

    private Vector3 _rangerPositionBeforeInteraction;
    private Quaternion _rangerRotationBeforeInteraction;
    private LobbyCameraController.ViewRotation _cameraViewRotationBeforeInteraction;
    private bool _hasRangerPositionBeforeInteraction;
    private Vector3 _cachedRangerPositionBeforeInteraction;
    private Quaternion _cachedRangerRotationBeforeInteraction;
    private LobbyCameraController.ViewRotation _cachedCameraViewRotationBeforeInteraction;
    private bool _hasCachedRangerPositionBeforeInteraction;
    private InteractionGuideController _interactionGuideController;

    bool ILobbyWorldButtonInteractionTarget.IsInteractionFeedbackAvailable => IsLocalRangerSeatedAtThis() || !TryGetOccupant(out _);
    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();

    private void Awake()
    {
        _interactionGuideController = GetComponent<InteractionGuideController>();
        if (_interactionGuideController == null)
            _interactionGuideController = GetComponentInParent<InteractionGuideController>();
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
        _cachedRangerRotationBeforeInteraction = rangerTransform.rotation;
        _cachedCameraViewRotationBeforeInteraction = CaptureLobbyCameraViewRotation();
        _hasCachedRangerPositionBeforeInteraction = true;
    }

    private void TryHandleDirectInteraction()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (IsLocalRangerSeatedAtThis())
        {
            if (Managers.Input.WasLeftMousePressedThisFrame() || Managers.Input.WasInteractKeyPressedThisFrame())
                HandleChairClicked();

            return;
        }

        if (_interactionGuideController == null || !_interactionGuideController.CanInteractFromLocalView())
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

        RangerController rangerController = rangerTransform.GetComponent<RangerController>();
        if (rangerController != null && rangerController.IsSeatedAt(transform))
            return true;

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private bool IsLocalRangerSeatedAtThis()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        RangerController rangerController = rangerTransform.GetComponent<RangerController>();
        return rangerController != null && rangerController.IsSeatedAt(transform);
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
                    rangerController.StandUp(_rangerPositionBeforeInteraction, _rangerRotationBeforeInteraction);
                    RestoreLobbyCameraViewRotation(_cameraViewRotationBeforeInteraction);
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
            _rangerRotationBeforeInteraction = _hasCachedRangerPositionBeforeInteraction
                ? _cachedRangerRotationBeforeInteraction
                : rangerTransform.rotation;
            _cameraViewRotationBeforeInteraction = _hasCachedRangerPositionBeforeInteraction
                ? _cachedCameraViewRotationBeforeInteraction
                : CaptureLobbyCameraViewRotation();
            _hasRangerPositionBeforeInteraction = true;
            Define.RangerAnimState sitAnimation = _rangerSitAnimations != null && _rangerSitAnimations.Length > 0
                ? _rangerSitAnimations[Random.Range(0, _rangerSitAnimations.Length)]
                : Define.RangerAnimState.Sit00;
            rangerController.Sit(transform, _seatedLocalPosition, Quaternion.Euler(_seatedLocalRotation), sitAnimation);
        }
    }

    private static LobbyCameraController.ViewRotation CaptureLobbyCameraViewRotation()
    {
        LobbyCameraController cameraController = FindAnyObjectByType<LobbyCameraController>();
        if (cameraController == null)
            return default;

        return cameraController.CaptureViewRotation();
    }

    private static void RestoreLobbyCameraViewRotation(LobbyCameraController.ViewRotation viewRotation)
    {
        LobbyCameraController cameraController = FindAnyObjectByType<LobbyCameraController>();
        if (cameraController == null)
            return;

        cameraController.RestoreViewRotation(viewRotation);
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
