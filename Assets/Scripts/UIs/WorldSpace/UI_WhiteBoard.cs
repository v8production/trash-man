using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UI_WhiteBoard : UI_Base, ILobbyWorldButtonInteractionTarget
{
    private const string OutlineShaderPass = "SRPDEFAULTUNLIT";

    private enum Buttons
    {
        WhiteBoardButton,
    }

    private enum Images
    {
        Image,
    }

    [SerializeField] private float _interactionTriggerDistance = 5.0f;
    [SerializeField] private float _outlineTriggerDistance = 10.0f;

    private Button _whiteBoardButton;
    private Image _boardImage;
    private readonly List<HighlightMaterialState> _highlightMaterials = new();
    private bool _isBound;
    private bool _isInitialized;
    private bool _isHighlightVisible = true;

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

        CacheHighlightMaterials();
        SetHighlightVisible(false);
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
        SetHighlightVisible(false);
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

    private void CacheHighlightMaterials()
    {
        _highlightMaterials.Clear();

        Renderer[] renderers = GetComponentsInParent<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null)
                    continue;

                _highlightMaterials.Add(new HighlightMaterialState(material));
            }
        }
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
        if (_highlightMaterials.Count == 0)
            CacheHighlightMaterials();

        SetHighlightVisible(IsWithinOutlineDistance());
    }

    private bool IsWithinOutlineDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.HasJoinedLobbySession)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        float triggerDistance = Mathf.Max(0.1f, _outlineTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private void SetHighlightVisible(bool visible)
    {
        if (_isHighlightVisible == visible)
            return;

        _isHighlightVisible = visible;
        for (int i = 0; i < _highlightMaterials.Count; i++)
            _highlightMaterials[i].Apply(visible);
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

    private sealed class HighlightMaterialState
    {
        private readonly Material _material;

        public HighlightMaterialState(Material material)
        {
            _material = material;
        }

        public void Apply(bool visible)
        {
            _material.SetShaderPassEnabled(OutlineShaderPass, visible);
        }
    }
}
