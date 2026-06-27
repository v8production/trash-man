using UnityEngine;
using UnityEngine.UI;

public class UI_KioskScreen : UI_Base, ILobbyWorldButtonInteractionTarget
{
    private enum Buttons
    {
        RoleSelectButton,
    }

    private enum Images
    {
        Image,
    }

    [SerializeField] private float _interactionTriggerDistance = 2.5f;

    private Button _roleSelectButton;
    private InteractionGuideController _interactionGuideController;
    private bool _isBound;
    private bool _isInitialized;

    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();
    float ILobbyWorldButtonInteractionTarget.ProximitySqrDistance => GetInteractionSqrDistance();
    int ILobbyWorldButtonInteractionTarget.InteractionPriority => 1;

    public override void Init()
    {
        if (_isInitialized)
            return;

        Bind<Button>(typeof(Buttons));
        Bind<Image>(typeof(Images));
        _roleSelectButton = GetButton((int)Buttons.RoleSelectButton);

        if (_roleSelectButton == null)
            _roleSelectButton = GetComponentInChildren<Button>(true);

        _interactionGuideController = GetComponentInParent<InteractionGuideController>();
        _interactionGuideController.SetVisible(false);
        BindButtonIfNeeded();
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            Init();

        LobbyWorldButtonInteractionRegistry.Register(this);
    }

    private void OnDisable()
    {
        LobbyWorldButtonInteractionRegistry.Unregister(this);
        _interactionGuideController.SetVisible(false);
    }

    private void Update()
    {
        RefreshHighlightVisibility();
        TryHandleDirectInteraction();
    }

    private void OnDestroy()
    {
        UnbindButton();
        LobbyWorldButtonInteractionRegistry.Unregister(this);
    }

    private void RefreshHighlightVisibility()
    {
        _interactionGuideController.SetVisible(IsWithinOutlineDistance());
    }

    private bool IsWithinOutlineDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.HasJoinedLobbySession)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        return _interactionGuideController.IsWithinTriggerDistance(rangerTransform);
    }

    private void BindButtonIfNeeded()
    {
        if (_isBound || _roleSelectButton == null)
            return;

        _roleSelectButton.onClick.AddListener(NotifyRoleSelectButtonClicked);
        _isBound = true;
    }

    private void UnbindButton()
    {
        if (!_isBound || _roleSelectButton == null)
            return;

        _roleSelectButton.onClick.RemoveListener(NotifyRoleSelectButtonClicked);
        _isBound = false;
    }

    private bool IsWithinInteractionDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.HasJoinedLobbySession)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        float triggerDistance = Mathf.Max(0.1f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private void TryHandleDirectInteraction()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame() && !Managers.Input.WasInteractKeyPressedThisFrame())
            return;

        NotifyRoleSelectButtonClicked();
    }

    private void NotifyRoleSelectButtonClicked()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!LobbyWorldButtonInteractionRegistry.CanInteract(this))
            return;

        if (Managers.Scene.CurrentScene is LobbyScene lobbyScene)
            lobbyScene.RequestShowRoleSelectMenu();
    }

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
    }
}
