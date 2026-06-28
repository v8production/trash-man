using System.Collections.Generic;
using UnityEngine;

public class InteractionGuideController : MonoBehaviour
{
    private const uint OutlineRenderingLayerMask = 1 << 1;
    private const string InteractionGuidePrefabName = "UI_InteractionGuide";

    [SerializeField] private float _outlineTriggerDistance = 5.0f;
    [SerializeField] private Vector3 _interactionGuideLocalPosition = Vector3.zero;

    private readonly List<RenderingLayerState> _renderingLayerStates = new();
    private UI_InteractionGuide _interactionGuide;
    private bool _isVisible = true;

    private void Awake()
    {
        CacheLayerStates();
        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (_isVisible && _interactionGuide != null)
            _interactionGuide.SetWorldPosition(GetInteractionGuideWorldPosition());
    }

    public void SetVisible(bool visible)
    {
        if (_isVisible == visible)
            return;

        _isVisible = visible;
        for (int i = 0; i < _renderingLayerStates.Count; i++)
        {
            RenderingLayerState renderingLayerState = _renderingLayerStates[i];
            renderingLayerState.Renderer.renderingLayerMask = visible ? renderingLayerState.OriginRenderingLayerMask | OutlineRenderingLayerMask : renderingLayerState.OriginRenderingLayerMask;
        }

        SetGuideVisible(visible);
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
        _interactionGuide.SetWorldPosition(GetInteractionGuideWorldPosition());
    }

    private Vector3 GetInteractionGuideWorldPosition()
    {
        return transform.TransformPoint(_interactionGuideLocalPosition);
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
