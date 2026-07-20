using System.Collections.Generic;
using UnityEngine;

public class InteractionGuideController : MonoBehaviour
{
    private const uint OutlineRenderingLayerMask = 1 << 1;
    private const string InteractionGuidePrefabName = "UI_InteractionGuide";

    [SerializeField] private float _outlineTriggerDistance = 5.0f;
    private readonly List<RenderingLayerState> _renderingLayerStates = new();
    private UI_InteractionGuide _interactionGuide;
    private ILobbyWorldButtonInteractionTarget _interactionTarget;
    private bool _isOutlineVisible = true;
    private bool _isGuideVisible = true;

    private void Awake()
    {
        CacheLayerStates();
        SetVisible(false);
    }

    private void Update()
    {
        RefreshVisibility();
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (_isGuideVisible && _interactionGuide != null)
            _interactionGuide.SetScreenCenter();
    }

    public void SetVisible(bool visible)
    {
        SetVisible(visible, visible);
    }

    public void SetVisible(bool outlineVisible, bool guideVisible)
    {
        guideVisible &= outlineVisible;

        if (_isOutlineVisible == outlineVisible && _isGuideVisible == guideVisible)
            return;

        if (_isOutlineVisible != outlineVisible)
        {
            _isOutlineVisible = outlineVisible;
            for (int i = 0; i < _renderingLayerStates.Count; i++)
            {
                RenderingLayerState renderingLayerState = _renderingLayerStates[i];
                renderingLayerState.Renderer.renderingLayerMask = outlineVisible ? renderingLayerState.OriginRenderingLayerMask | OutlineRenderingLayerMask : renderingLayerState.OriginRenderingLayerMask;
            }
        }

        if (_isGuideVisible != guideVisible)
        {
            _isGuideVisible = guideVisible;
            SetGuideVisible(guideVisible);
        }
    }

    public bool IsWithinTriggerDistance(Transform target)
    {
        if (target == null)
            return false;

        if (target.TryGetComponent(out RangerController rangerController) && rangerController.IsSeated)
            return false;

        float triggerDistance = Mathf.Max(0f, _outlineTriggerDistance);
        return (target.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    public bool CanInteractFromLocalView()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        ILobbyWorldButtonInteractionTarget interactionTarget = GetInteractionTarget();
        if (interactionTarget == null || !interactionTarget.IsInteractionFeedbackAvailable || !interactionTarget.IsProximityInteractable)
            return false;

        return IsHitByCenterCameraRay();
    }

    private void RefreshVisibility()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
        {
            SetVisible(false);
            return;
        }

        ILobbyWorldButtonInteractionTarget interactionTarget = GetInteractionTarget();
        if (interactionTarget == null || !interactionTarget.IsInteractionFeedbackAvailable)
        {
            SetVisible(false);
            return;
        }

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
        {
            SetVisible(false);
            return;
        }

        bool outlineVisible = IsWithinTriggerDistance(rangerTransform);
        SetVisible(outlineVisible, outlineVisible && interactionTarget.IsProximityInteractable && IsHitByCenterCameraRay());
    }

    private bool IsHitByCenterCameraRay()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float triggerDistance = Mathf.Max(0f, _outlineTriggerDistance);
        RaycastHit[] hits = Physics.RaycastAll(ray, triggerDistance, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsOwnTransform(hits[i].transform))
                return true;
        }

        for (int i = 0; i < _renderingLayerStates.Count; i++)
        {
            Bounds bounds = _renderingLayerStates[i].Renderer.bounds;
            if (bounds.IntersectRay(ray, out float distance) && distance <= triggerDistance)
                return true;
        }

        return false;
    }

    private bool IsOwnTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        if (hitTransform.TryGetComponent(out InteractionGuideController hitController) && hitController == this)
            return true;

        return hitTransform.GetComponentInParent<InteractionGuideController>() == this;
    }

    private ILobbyWorldButtonInteractionTarget GetInteractionTarget()
    {
        if (_interactionTarget != null)
            return _interactionTarget;

        _interactionTarget = GetComponent<ILobbyWorldButtonInteractionTarget>();
        if (_interactionTarget != null)
            return _interactionTarget;

        _interactionTarget = GetComponentInChildren<ILobbyWorldButtonInteractionTarget>(true);
        return _interactionTarget;
    }

    private void CacheLayerStates()
    {
        _renderingLayerStates.Clear();

        Renderer[] targets = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            Renderer targetRenderer = targets[i];
            _renderingLayerStates.Add(new RenderingLayerState(targetRenderer, targetRenderer.renderingLayerMask));
        }
    }

    private void SetGuideVisible(bool visible)
    {
        if (visible)
        {
            EnsureInteractionGuide();
            _interactionGuide.SetScreenCenter();
            _interactionGuide.Show();
            return;
        }

        if (_interactionGuide != null)
            _interactionGuide.Hide();
    }

    private void EnsureInteractionGuide()
    {
        if (_interactionGuide != null)
            return;

        _interactionGuide = Managers.UI.CreateSceneUI<UI_InteractionGuide>(InteractionGuidePrefabName);
        _interactionGuide.SetScreenCenter();
    }

    private readonly struct RenderingLayerState
    {
        public readonly Renderer Renderer;
        public readonly uint OriginRenderingLayerMask;

        public RenderingLayerState(Renderer renderer, uint originRenderingLayerMask)
        {
            Renderer = renderer;
            OriginRenderingLayerMask = originRenderingLayerMask;
        }
    }

}

public interface ILobbyWorldButtonInteractionTarget
{
    bool IsInteractionFeedbackAvailable { get; }
    bool IsProximityInteractable { get; }
}
