using System.Collections.Generic;
using UnityEngine;

public class OutlineController : MonoBehaviour
{
    private const string OutlineShaderPass = "SRPDEFAULTUNLIT";

    [SerializeField] private float _outlineTriggerDistance = 5.0f;

    private readonly List<Material> _outlineMaterials = new();
    private bool _isVisible = true;

    private void Awake()
    {
        CacheOutlineMaterials();
        SetVisible(false);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (_outlineMaterials.Count == 0)
            CacheOutlineMaterials();

        if (_isVisible == visible)
            return;

        _isVisible = visible;
        for (int i = 0; i < _outlineMaterials.Count; i++)
            _outlineMaterials[i].SetShaderPassEnabled(OutlineShaderPass, visible);
    }

    public bool IsWithinTriggerDistance(Transform target)
    {
        if (target == null)
            return false;

        float triggerDistance = Mathf.Max(0f, _outlineTriggerDistance);
        return (target.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private void CacheOutlineMaterials()
    {
        _outlineMaterials.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
                continue;

            Material[] materials = targetRenderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material != null)
                    _outlineMaterials.Add(material);
            }
        }
    }
}
