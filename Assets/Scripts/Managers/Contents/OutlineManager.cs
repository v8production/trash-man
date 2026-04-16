using System;
using System.Collections.Generic;
using UnityEngine;

public class OutlineManager
{
    private const string OutlineShaderName = "Custom/ObjectOutline";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    private readonly Dictionary<int, OutlineTarget> _targets = new();

    public void Init()
    {
        Clear();
    }

    public void Clear()
    {
        List<int> keys = new List<int>(_targets.Keys);
        for (int i = 0; i < keys.Count; i++)
            UnregisterTarget(keys[i]);

        _targets.Clear();
    }

    public void RegisterTarget(Component owner, Renderer[] renderers, Color color, float width = 0.01f, bool visible = true)
    {
        if (owner == null || renderers == null || renderers.Length == 0)
            return;

        int key = GetOwnerKey(owner);
        UnregisterTarget(key);

        Shader outlineShader = Shader.Find(OutlineShaderName);
        if (outlineShader == null)
        {
            Debug.LogWarning($"[OutlineManager] Outline shader '{OutlineShaderName}' not found for {owner.name}.");
            return;
        }

        Material outlineMaterial = new Material(outlineShader);
        outlineMaterial.SetFloat(OutlineWidthId, Mathf.Max(0f, width));

        OutlineTarget target = new OutlineTarget(owner, renderers, outlineMaterial, color, visible);
        target.Attach();
        _targets[key] = target;
    }

    public void UnregisterTarget(Component owner)
    {
        if (owner == null)
            return;

        UnregisterTarget(GetOwnerKey(owner));
    }

    public void SetVisible(Component owner, bool visible)
    {
        if (owner == null)
            return;

        int key = GetOwnerKey(owner);
        if (!_targets.TryGetValue(key, out OutlineTarget target))
            return;

        target.SetVisible(visible);
        _targets[key] = target;
    }

    public void SetColor(Component owner, Color color)
    {
        if (owner == null)
            return;

        int key = GetOwnerKey(owner);
        if (!_targets.TryGetValue(key, out OutlineTarget target))
            return;

        target.SetColor(color);
        _targets[key] = target;
    }

    private static int GetOwnerKey(Component owner)
    {
        return owner != null ? owner.GetEntityId().GetHashCode() : 0;
    }

    private void UnregisterTarget(int key)
    {
        if (!_targets.TryGetValue(key, out OutlineTarget target))
            return;

        target.Detach();
        _targets.Remove(key);
    }

    private struct OutlineTarget
    {
        private readonly Component _owner;
        private readonly Renderer[] _renderers;
        private Material _outlineMaterial;
        private Color _outlineColor;
        private bool _isVisible;

        public OutlineTarget(Component owner, Renderer[] renderers, Material outlineMaterial, Color outlineColor, bool isVisible)
        {
            _owner = owner;
            _renderers = renderers;
            _outlineMaterial = outlineMaterial;
            _outlineColor = outlineColor;
            _isVisible = isVisible;
        }

        public void Attach()
        {
            if (_outlineMaterial == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                    continue;

                Material[] existingMaterials = targetRenderer.sharedMaterials;
                Material[] updatedMaterials = new Material[existingMaterials.Length + 1];
                Array.Copy(existingMaterials, updatedMaterials, existingMaterials.Length);
                updatedMaterials[updatedMaterials.Length - 1] = _outlineMaterial;
                targetRenderer.sharedMaterials = updatedMaterials;
            }

            ApplyColor();
        }

        public void Detach()
        {
            if (_outlineMaterial == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer targetRenderer = _renderers[i];
                if (targetRenderer == null)
                    continue;

                Material[] existingMaterials = targetRenderer.sharedMaterials;
                int outlineIndex = -1;
                for (int j = 0; j < existingMaterials.Length; j++)
                {
                    if (existingMaterials[j] == _outlineMaterial)
                    {
                        outlineIndex = j;
                        break;
                    }
                }

                if (outlineIndex < 0)
                    continue;

                Material[] updatedMaterials = new Material[existingMaterials.Length - 1];
                int writeIndex = 0;
                for (int j = 0; j < existingMaterials.Length; j++)
                {
                    if (j == outlineIndex)
                        continue;

                    updatedMaterials[writeIndex++] = existingMaterials[j];
                }

                targetRenderer.sharedMaterials = updatedMaterials;
            }

            if (_owner != null)
                UnityEngine.Object.Destroy(_outlineMaterial);

            _outlineMaterial = null;
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            ApplyColor();
        }

        public void SetColor(Color color)
        {
            _outlineColor = color;
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_outlineMaterial == null)
                return;

            Color appliedColor = _outlineColor;
            if (!_isVisible)
                appliedColor.a = 0f;

            _outlineMaterial.SetColor(OutlineColorId, appliedColor);
        }
    }
}
