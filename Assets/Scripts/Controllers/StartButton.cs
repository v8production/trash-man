using UnityEngine;
using System.Collections.Generic;

public class StartButton : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    private const string OutlineShaderPass = "SRPDEFAULTUNLIT";
    private const string RimLightProperty = "_RimLight";

    [SerializeField] private float _outlineTriggerDistance = 5.0f;
    [SerializeField] private float _interactionTriggerDistance = 1.5f;

    private readonly List<HighlightMaterialState> _highlightMaterials = new();
    private bool _isHighlightVisible = true;

    bool ILobbyWorldButtonInteractionTarget.IsProximityInteractable => IsWithinInteractionDistance();
    float ILobbyWorldButtonInteractionTarget.ProximitySqrDistance => GetInteractionSqrDistance();
    int ILobbyWorldButtonInteractionTarget.InteractionPriority => 0;

    private void Awake()
    {
        CacheHighlightMaterials();
        SetHighlightVisible(false);
    }

    private void OnEnable()
    {
        LobbyWorldButtonInteractionRegistry.Register(this);
    }

    private void OnDisable()
    {
        LobbyWorldButtonInteractionRegistry.Unregister(this);
        SetHighlightVisible(false);
    }

    private void Update()
    {
        RefreshHighlightVisibility();
        TryHandleDirectClick();
    }

    private void CacheHighlightMaterials()
    {
        _highlightMaterials.Clear();

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
                if (material == null)
                    continue;

                if (!HasHighlightProperties(material))
                    continue;

                _highlightMaterials.Add(new HighlightMaterialState(material));
            }
        }
    }

    private static bool HasHighlightProperties(Material material)
    {
        return material.HasProperty(RimLightProperty);
    }

    private void RefreshHighlightVisibility()
    {
        SetHighlightVisible(IsWithinOutlineDistance());
    }

    private bool IsWithinOutlineDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform))
            return false;

        float triggerDistance = Mathf.Max(0f, _outlineTriggerDistance);
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

    private void TryHandleDirectClick()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!LobbyWorldButtonInteractionRegistry.CanInteract(this))
            return;

        if (!Managers.Input.WasLeftMousePressedThisFrame())
            return;

        HandleStartButtonClicked();
    }

    private bool IsWithinInteractionDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform))
            return false;

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform))
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
    }

    private void HandleStartButtonClicked()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return;

        if (!Managers.TitanRole.CanStartGameWithAllRolesAssigned(out string roleError))
        {
            string label = string.IsNullOrWhiteSpace(roleError) ? "role requirements" : roleError;
            Managers.Toast.EnqueueMessage($"Cannot start game: {label}", 2.8f);
            return;
        }

        if (!LobbyNetworkPlayer.RequestLoadGameFromLocalPlayer())
            Managers.Scene.LoadScene(Define.Scene.Game);
    }

    private sealed class HighlightMaterialState
    {
        private readonly Material _material;
        private readonly float _rimLight;

        public HighlightMaterialState(Material material)
        {
            _material = material;
            _rimLight = material.GetFloat(RimLightProperty);
        }

        public void Apply(bool visible)
        {
            _material.SetShaderPassEnabled(OutlineShaderPass, visible);
            _material.SetFloat(RimLightProperty, visible ? Mathf.Max(1f, _rimLight) : 0f);
        }
    }
}
