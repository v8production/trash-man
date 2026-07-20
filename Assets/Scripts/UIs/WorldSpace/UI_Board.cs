using UnityEngine;
using UnityEngine.UI;

public class UI_Board : UI_Base, ILobbyWorldButtonInteractionTarget
{
    private enum Buttons
    {
        BoardButton,
    }

    private enum Images
    {
        Board,
    }

    [SerializeField] private float _interactionTriggerDistance = 5.0f;

    private Button _boardButton;
    private Image _boardImage;
    private InteractionGuideController _interactionGuideController;
    private bool _isBound;
    private bool _isInitialized;

    bool ILobbyWorldButtonInteractionTarget.IsInteractionFeedbackAvailable => true;
    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();

    public override void Init()
    {
        if (_isInitialized)
            return;

        Bind<Button>(typeof(Buttons));
        Bind<Image>(typeof(Images));
        _boardButton = GetButton((int)Buttons.BoardButton);
        _boardImage = GetImage((int)Images.Board);

        if (_boardButton == null)
            _boardButton = GetComponentInChildren<Button>(true);

        _interactionGuideController = GetComponent<InteractionGuideController>();
        if (_interactionGuideController == null)
            _interactionGuideController = GetComponentInParent<InteractionGuideController>();

        ApplyBoardSprite();
        BindButtonIfNeeded();
        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
            Init();

        BoardDrawingSurface.Changed += ApplyBoardSprite;
        ApplyBoardSprite();
    }

    private void OnDisable()
    {
        BoardDrawingSurface.Changed -= ApplyBoardSprite;
    }

    private void Update()
    {
        TryHandleDirectInteraction();
    }

    private void OnDestroy()
    {
        UnbindButton();
    }

    private void ApplyBoardSprite()
    {
        if (_boardImage == null)
            return;

        _boardImage.sprite = BoardDrawingSurface.Sprite;
        _boardImage.preserveAspect = true;
    }

    private void BindButtonIfNeeded()
    {
        if (_isBound || _boardButton == null)
            return;

        _boardButton.onClick.AddListener(NotifyBoardButtonClicked);
        _isBound = true;
    }

    private void UnbindButton()
    {
        if (!_isBound || _boardButton == null)
            return;

        _boardButton.onClick.RemoveListener(NotifyBoardButtonClicked);
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

        NotifyBoardButtonClicked();
    }

    private void NotifyBoardButtonClicked()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (_interactionGuideController == null || !_interactionGuideController.CanInteractFromLocalView())
            return;

        if (Managers.Scene.CurrentScene is LobbyScene lobbyScene)
            lobbyScene.RequestShowBoardMenu();
    }
}
