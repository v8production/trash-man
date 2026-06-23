using UnityEngine;
using System.Collections.Generic;

public class ChairController : MonoBehaviour, ILobbyWorldButtonInteractionTarget
{
    private const string OutlineShaderPass = "SRPDEFAULTUNLIT";

    [SerializeField] private float _outlineTriggerDistance = 5.0f;
    [SerializeField] private float _interactionTriggerDistance = 1.5f;
    [SerializeField] private Vector3 _seatedLocalPosition = new(0.35f, 0f, 0f);
    [SerializeField] private Vector3 _seatedLocalRotation = new(0f, 90f, 0f);
    [SerializeField] private Define.RangerAnimState _rangerSitAnimation = Define.RangerAnimState.Sit00;

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

                _highlightMaterials.Add(new HighlightMaterialState(material));
            }
        }
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

        if (TryGetOccupant(out _))
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
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

        HandleChairClicked();
    }

    private bool IsWithinInteractionDistance()
    {
        if (Managers.Input.Mode != Define.InputMode.Player)
            return false;

        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return false;

        float triggerDistance = Mathf.Max(0f, _interactionTriggerDistance);
        return (rangerTransform.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance;
    }

    private float GetInteractionSqrDistance()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return float.PositiveInfinity;

        return (rangerTransform.position - transform.position).sqrMagnitude;
    }

    private void HandleChairClicked()
    {
        if (!Managers.LobbySession.TryGetLocalRangerTransform(out Transform rangerTransform) || rangerTransform == null)
            return;

        RangerController rangerController = rangerTransform.GetComponent<RangerController>();
        if (rangerController != null)
        {
            if (rangerController.IsSeatedAt(transform))
            {
                rangerController.StandUp();
                return;
            }

            if (TryGetOccupant(out RangerController seatedRanger) && seatedRanger != rangerController)
                return;

            rangerController.Sit(transform, _seatedLocalPosition, Quaternion.Euler(_seatedLocalRotation), _rangerSitAnimation);
        }
    }

    private bool TryGetOccupant(out RangerController occupant)
    {
        RangerController[] rangers = FindObjectsByType<RangerController>();
        for (int i = 0; i < rangers.Length; i++)
        {
            RangerController ranger = rangers[i];
            if (ranger != null && ranger.IsSeatedAt(transform))
            {
                occupant = ranger;
                return true;
            }
        }

        occupant = null;
        return false;
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
