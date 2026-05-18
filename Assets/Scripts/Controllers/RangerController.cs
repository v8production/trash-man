using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RangerController : MonoBehaviour
{
    private const string RangerColorMaterialName = "Ranger Color_Mat";
    private const string RangerFaceMaterialName = "Ranger Face_Mat";
    private const string ImportedRangerFaceMaterialName = "Ranger_Face";

    [Header("Actions (Player Map)")]
    [SerializeField] private string moveActionName = "Move";


    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotateLerpSpeed = 12f;

    [Header("Material Debug")]
    [SerializeField] private bool _hasNetworkedColorOverride;
    [SerializeField] private Color _rangerColor = Color.white;
    [SerializeField] private Color _faceColor = Color.white;

    private CharacterController _characterController;
    private LobbyCameraController _cameraController;
    private InputAction _moveAction;
    private MaterialPropertyBlock _materialPropertyBlock;
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Vector2 _moveInput;
    private bool _initialized;

    Animator Anim;
    public event System.Action<Define.RangerAnimState> EmotionRequested;

    private Define.RangerAnimState _animState;
    public Define.RangerAnimState AnimState
    {
        get { return _animState; }
        set
        {
            if (EqualityComparer<Define.RangerAnimState>.Default.Equals(_animState, value))
                return;
            _animState = value;

            if (Anim != null)
                Anim.CrossFade(_animState.ToString(), 0.1f);
        }
    }

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (_initialized)
            return;

        _characterController = GetComponent<CharacterController>();
        Anim = GetComponentInChildren<Animator>();
        _renderers = GetComponentsInChildren<Renderer>(true);

        InputActionMap playerMap = Managers.Input.PlayerMap;
        if (playerMap != null)
            _moveAction = playerMap.FindAction(moveActionName, false);

        AnimState = Define.RangerAnimState.Idle00;
        RangerFaceTextureStore.ApplyTo(gameObject);
        RefreshMaterialDebugValues();
        _initialized = true;
    }

    public void ApplyNetworkedColors(Color color, bool hasNetworkedColorOverride)
    {
        Init();

        _hasNetworkedColorOverride = hasNetworkedColorOverride;
        if (_hasNetworkedColorOverride)
        {
            _rangerColor = color;
            _faceColor = color;
        }

        ApplyColorPresentation();
        RefreshMaterialDebugValues();
    }

    public void ApplyDefaultFaceTexture()
    {
        Init();
        RangerFaceTextureStore.ApplyDefaultTo(gameObject);
        RefreshMaterialDebugValues();
    }

    public void ApplySavedFaceTexture()
    {
        Init();
        RangerFaceTextureStore.ApplyTo(gameObject);
        RefreshMaterialDebugValues();
    }

    public void ApplyFaceTexture(Texture texture)
    {
        Init();
        RangerFaceTextureStore.ApplyTextureTo(gameObject, texture);
        RefreshMaterialDebugValues();
    }

    private void Update()
    {
        if (!_initialized)
            Init();

        if (Managers.Input.Mode != Define.InputMode.Player)
        {
            _moveInput = Vector2.zero;
            return;
        }

        UpdateInput();
        Define.RangerAnimState requestedEmotion;
        bool hasEmotionInput = TryGetEmotionInput(out requestedEmotion);

        Vector3 moveDirection = GetCameraRelativeDirectionOnPlane(_moveInput);
        Vector3 planarVelocity = moveDirection * moveSpeed;
        _characterController.Move(planarVelocity * Time.deltaTime);

        UpdateRotation(moveDirection);

        if (hasEmotionInput)
        {
            PlayEmotion(requestedEmotion);
            EmotionRequested?.Invoke(requestedEmotion);
            return;
        }

        bool isMoving = _moveInput.sqrMagnitude > 0.0001f;
        if (isMoving)
        {
            AnimState = Define.RangerAnimState.Walk01;
            return;
        }

        if (IsEmotionState(AnimState) && !IsCurrentAnimationFinished())
            return;

        AnimState = Define.RangerAnimState.Idle00;
    }

    private void UpdateInput()
    {
        _moveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);
    }

    private bool TryGetEmotionInput(out Define.RangerAnimState emotionState)
    {
        emotionState = Define.RangerAnimState.Idle00;

        if (Managers.Input.WasDigitPressedThisFrame(1))
        {
            emotionState = Define.RangerAnimState.Emote00;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(2))
        {
            emotionState = Define.RangerAnimState.Emote01;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(3))
        {
            emotionState = Define.RangerAnimState.Emote02;
            return true;
        }

        return false;
    }

    private void PlayEmotion(Define.RangerAnimState emotionState)
    {
        _animState = emotionState;

        if (Anim != null)
            Anim.CrossFade(emotionState.ToString(), 0.1f, 0, 0f);
    }

    private bool IsCurrentAnimationFinished()
    {
        if (Anim == null || Anim.IsInTransition(0))
            return false;

        AnimatorStateInfo stateInfo = Anim.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(AnimState.ToString()) && stateInfo.normalizedTime >= 1f;
    }

    public static bool IsEmotionState(Define.RangerAnimState state)
    {
        return state == Define.RangerAnimState.Emote00
            || state == Define.RangerAnimState.Emote01
            || state == Define.RangerAnimState.Emote02;
    }

    private void ApplyColorPresentation()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        if (_materialPropertyBlock == null)
            _materialPropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer targetRenderer = _renderers[i];
            if (targetRenderer == null)
                continue;

            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
                continue;

            for (int m = 0; m < sharedMaterials.Length; m++)
            {
                Material material = sharedMaterials[m];
                if (material == null)
                    continue;

                bool isRangerColorMaterial = IsRangerColorMaterial(material);
                bool isFaceMaterial = IsFaceMaterial(material);
                if (!isRangerColorMaterial && !isFaceMaterial)
                    continue;

                if (!_hasNetworkedColorOverride)
                {
                    targetRenderer.SetPropertyBlock(null, m);
                    continue;
                }

                Color targetColor = isFaceMaterial ? _faceColor : _rangerColor;
                targetRenderer.GetPropertyBlock(_materialPropertyBlock, m);
                _materialPropertyBlock.SetColor("_Color", targetColor);
                _materialPropertyBlock.SetColor("_BaseColor", targetColor);
                targetRenderer.SetPropertyBlock(_materialPropertyBlock, m);
            }
        }
    }

    private void RefreshMaterialDebugValues()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>(true);

        bool foundRangerColor = false;
        bool foundFace = false;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer targetRenderer = _renderers[i];
            if (targetRenderer == null)
                continue;

            Material[] sharedMaterials = targetRenderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
                continue;

            for (int m = 0; m < sharedMaterials.Length; m++)
            {
                Material material = sharedMaterials[m];
                if (material == null)
                    continue;

                if (IsRangerColorMaterial(material) && !_hasNetworkedColorOverride)
                {
                    _rangerColor = ReadMaterialColor(material);
                    foundRangerColor = true;
                }

                if (!IsFaceMaterial(material))
                    continue;

                if (!_hasNetworkedColorOverride)
                    _faceColor = ReadMaterialColor(material);

                foundFace = true;

                if (foundRangerColor && foundFace)
                    return;
            }
        }
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return Color.white;
    }

    private static bool IsRangerColorMaterial(Material material)
    {
        return material.name.StartsWith(RangerColorMaterialName);
    }

    private static bool IsFaceMaterial(Material material)
    {
        string materialName = material.name;
        return materialName.StartsWith(RangerFaceMaterialName)
            || materialName == ImportedRangerFaceMaterialName
            || materialName.StartsWith(ImportedRangerFaceMaterialName + " ");
    }

    private void UpdateRotation(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.0001f)
            currentForward = moveDirection;

        Quaternion currentRotation = Quaternion.LookRotation(currentForward.normalized, Vector3.up);
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotateLerpSpeed * Time.deltaTime);
    }

    private Vector3 GetCameraRelativeDirectionOnPlane(Vector2 moveInput)
    {
        if (_cameraController == null)
            _cameraController = GetMainCameraController();

        Vector3 forward;
        Vector3 right;

        if (_cameraController != null)
        {
            forward = Vector3.ProjectOnPlane(_cameraController.transform.forward, Vector3.up);
            right = Vector3.ProjectOnPlane(_cameraController.transform.right, Vector3.up);
        }
        else
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        }

        if (forward.sqrMagnitude > 0.0001f)
            forward.Normalize();
        else
            forward = Vector3.forward;

        if (right.sqrMagnitude > 0.0001f)
            right.Normalize();
        else
            right = Vector3.right;

        Vector3 direction = (right * moveInput.x) + (forward * moveInput.y);
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private static LobbyCameraController GetMainCameraController()
    {
        LobbyCameraController cameraController = UnityEngine.Object.FindAnyObjectByType<LobbyCameraController>();
        if (cameraController != null)
            return cameraController;

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.GetComponent<LobbyCameraController>() : null;
    }
}
