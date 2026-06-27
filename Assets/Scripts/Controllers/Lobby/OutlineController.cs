using System.Collections.Generic;
using UnityEngine;

public class OutlineController : MonoBehaviour
{
    private const uint OutlineRenderingLayerMask = 1 << 1;

    [SerializeField] private float _outlineTriggerDistance = 5.0f;

    private readonly List<RenderingLayerState> _renderingLayerStates = new();
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
    }

    public bool IsWithinTriggerDistance(Transform target)
    {
        if (target == null)
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
