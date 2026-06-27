using UnityEngine;
using UnityEngine.UI;

public class UI_WhiteBoard : UI_Base, ILobbyWorldButtonInteractionTarget
{
    private enum Buttons
    {
        WhiteBoardButton,
    }

    private enum Images
    {
        Image,
    }

    [SerializeField] private float _interactionTriggerDistance = 5.0f;

    private Button _whiteBoardButton;
    private Image _boardImage;
    private OutlineController _outlineController;
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
        _whiteBoardButton = GetButton((int)Buttons.WhiteBoardButton);
        _boardImage = GetImage((int)Images.Image);

        if (_whiteBoardButton == null)
            _whiteBoardButton = GetComponentInChildren<Button>(true);

        _outlineController = GetComponentInParent<OutlineController>();
        _outlineController.SetVisible(false);
        ApplyBoardSprite();
        BindButtonIfNeeded();
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            Init();

        LobbyWorldButtonInteractionRegistry.Register(this);
        WhiteBoardDrawingSurface.Changed += ApplyBoardSprite;
        ApplyBoardSprite();
    }

    private void OnDisable()
    {
        LobbyWorldButtonInteractionRegistry.Unregister(this);
        WhiteBoardDrawingSurface.Changed -= ApplyBoardSprite;
        _outlineController.SetVisible(false);
    }

    private void Update()
    {
        RefreshHighlightVisibility();
        TryHandleDirectClick();
    }

    private void OnDestroy()
    {
        UnbindButton();
        LobbyWorldButtonInteractionRegistry.Unregister(this);
    }

    private void ApplyBoardSprite()
    {
        if (_boardImage == null)
            return;

        _boardImage.sprite = WhiteBoardDrawingSurface.Sprite;
        _boardImage.preserveAspect = true;
    }

    private void RefreshHighlightVisibility()
    {
        _outlineController.SetVisible(IsWithinOutlineDistance());
    }

    private bool IsWithinOutlineDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.HasJoinedLobbySession)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        return _outlineController.IsWithinTriggerDistance(rangerTransform);
    }

    private void BindButtonIfNeeded()
    {
        if (_isBound || _whiteBoardButton == null)
            return;

        _whiteBoardButton.onClick.AddListener(NotifyWhiteBoardButtonClicked);
        _isBound = true;
    }

    private void UnbindButton()
    {
        if (!_isBound || _whiteBoardButton == null)
            return;

        _whiteBoardButton.onClick.RemoveListener(NotifyWhiteBoardButtonClicked);
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

    private void TryHandleDirectClick()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame())
            return;

        NotifyWhiteBoardButtonClicked();
    }

    private void NotifyWhiteBoardButtonClicked()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!IsWithinInteractionDistance())
            return;

        if (Managers.Scene.CurrentScene is LobbyScene lobbyScene)
            lobbyScene.RequestShowWhiteBoardMenu();
    }

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
    }
}
