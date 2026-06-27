using System.Collections.Generic;
using UnityEngine;

public class OutlineController : MonoBehaviour
{
    private const int OutlineLayer = 3;

    [SerializeField] private float _outlineTriggerDistance = 5.0f;

    private readonly List<LayerState> _layerStates = new();
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
        for (int i = 0; i < _layerStates.Count; i++)
        {
            LayerState layerState = _layerStates[i];
            layerState.GameObject.layer = visible ? OutlineLayer : layerState.OriginLayer;
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
        _layerStates.Clear();

        Transform[] targets = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            GameObject targetObject = targets[i].gameObject;
            _layerStates.Add(new LayerState(targetObject, targetObject.layer));
        }
    }

    private readonly struct LayerState
    {
        public readonly GameObject GameObject;
        public readonly int OriginLayer;

        public LayerState(GameObject gameObject, int originLayer)
        {
            GameObject = gameObject;
            OriginLayer = originLayer;
        }
    }

}
