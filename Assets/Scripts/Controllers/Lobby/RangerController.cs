using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RangerController : MonoBehaviour
{
    private const string RangerColorMaterialName = "Ranger Color_Mat";
    private const string RangerFaceMaterialName = "Ranger Face_Mat";
    private const string ImportedRangerFaceMaterialName = "Ranger_Face";
    private const string UpperBodyEmoteLayerName = "UpperBody";
    private const string UpperBodyIdleStateName = "UpperBodyIdle";
    private const string UpperBodyEmoteStatePrefix = "UpperBody";
    private const float EmotionCrossFadeDuration = 0.1f;
    private const float UpperBodyEmoteFallbackDuration = 2f;

    [Header("Actions (Player Map)")]
    [SerializeField] private string moveActionName = "Move";


    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float mouseYawSensitivity = 0.12f;

    [Header("Material Debug")]
    [SerializeField] private bool _hasNetworkedColorOverride;
    [SerializeField] private Color _rangerColor = Color.white;
    [SerializeField] private Color _faceColor = Color.white;
    [SerializeField] private float _faceEmissive = 1f;

    private CharacterController _characterController;
    private InputAction _moveAction;
    private MaterialPropertyBlock _materialPropertyBlock;
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Vector2 _moveInput;
    private bool _initialized;
    private bool _isSeated;
    private Define.RangerAnimState _seatedAnimState = Define.RangerAnimState.Sit00;
    private int _upperBodyEmoteLayerIndex = -1;
    private bool _upperBodyEmoteActive;
    private float _upperBodyEmoteEndTime;

    Animator Anim;
    public event System.Action<Define.RangerAnimState> EmotionRequested;
    public event System.Action<Define.RangerAnimState> SitAnimationRequested;
    public event System.Action StandUpAnimationRequested;

    private Define.RangerAnimState _animState;
    public bool IsSeated => _isSeated;

    public bool IsSeatedAt(Transform chairTransform)
    {
        return _isSeated && transform.parent == chairTransform;
    }

    public Define.RangerAnimState AnimState
    {
        get { return _animState; }
        set
        {
            if (EqualityComparer<Define.RangerAnimState>.Default.Equals(_animState, value))
                return;

            _animState = value;

            if (Anim != null)
            Anim.CrossFade(_animState.ToString(), EmotionCrossFadeDuration);
        }
    }

    private void Awake()
    {
        Init();
    }

    private void OnDisable()
    {
        _moveInput = Vector2.zero;
        StopMovementAnimation();
    }

    private void Init()
    {
        if (_initialized)
            return;

        _characterController = GetComponent<CharacterController>();
        Anim = GetComponentInChildren<Animator>();
        _upperBodyEmoteLayerIndex = ResolveUpperBodyEmoteLayerIndex();
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
        ApplyNetworkedColors(color, color, hasNetworkedColorOverride, 3f);
    }

    public void ApplyNetworkedColors(Color rangerColor, Color faceColor, bool hasNetworkedColorOverride)
    {
        ApplyNetworkedColors(rangerColor, faceColor, hasNetworkedColorOverride, 3f);
    }

    public void ApplyNetworkedColors(Color rangerColor, Color faceColor, bool hasNetworkedColorOverride, float faceEmissive)
    {
        Init();

        _hasNetworkedColorOverride = hasNetworkedColorOverride;
        if (_hasNetworkedColorOverride)
        {
            _rangerColor = rangerColor;
            _faceColor = faceColor;
            _faceEmissive = faceEmissive;
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

        if (_isSeated)
        {
            _moveInput = Vector2.zero;

            UpdateUpperBodyEmoteLayer();

            if (Managers.Input.Mode == Define.InputMode.Player && TryGetSeatedUpperBodyEmotionInput(out Define.RangerAnimState seatedEmotionState))
            {
                PlaySeatedUpperBodyEmotion(seatedEmotionState);
                EmotionRequested?.Invoke(seatedEmotionState);
            }

            return;
        }

        if (Managers.Input.Mode != Define.InputMode.Player)
        {
            _moveInput = Vector2.zero;
            StopMovementAnimation();
            return;
        }

        UpdateInput();
        UpdateMouseYaw();
        Define.RangerAnimState requestedEmotion;
        bool hasEmotionInput = TryGetEmotionInput(out requestedEmotion);

        Vector3 moveDirection = GetTransformRelativeDirectionOnPlane(_moveInput);
        Vector3 planarVelocity = moveDirection * moveSpeed;
        _characterController.Move(planarVelocity * Time.deltaTime);

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

        if (IsEmotionState(AnimState))
            return;

        AnimState = Define.RangerAnimState.Idle00;
    }

    private void StopMovementAnimation()
    {
        if (IsEmotionState(AnimState))
            return;

        AnimState = Define.RangerAnimState.Idle00;
    }

    public void Sit(Transform chairTransform, Vector3 localPosition, Quaternion localRotation, Define.RangerAnimState seatedAnimState)
    {
        _isSeated = true;
        _seatedAnimState = seatedAnimState;
        _moveInput = Vector2.zero;
        transform.SetParent(chairTransform);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        _animState = _seatedAnimState;
        if (Anim != null)
            Anim.CrossFade(_animState.ToString(), EmotionCrossFadeDuration, 0, 0f);
        StopUpperBodyEmoteLayer();
        SitAnimationRequested?.Invoke(_seatedAnimState);
    }

    public void StandUp()
    {
        if (!_isSeated)
            return;

        _isSeated = false;
        _seatedAnimState = Define.RangerAnimState.Sit00;
        _moveInput = Vector2.zero;
        transform.SetParent(null, true);
        StopUpperBodyEmoteLayer();
        AnimState = Define.RangerAnimState.Idle00;
        StandUpAnimationRequested?.Invoke();
    }

    public void StandUp(Vector3 worldPosition)
    {
        StandUp(worldPosition, transform.rotation);
    }

    public void StandUp(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (!_isSeated)
            return;

        _isSeated = false;
        _seatedAnimState = Define.RangerAnimState.Sit00;
        _moveInput = Vector2.zero;

        bool wasCharacterControllerEnabled = _characterController != null && _characterController.enabled;
        if (wasCharacterControllerEnabled)
            _characterController.enabled = false;

        transform.SetParent(null, true);
        transform.position = worldPosition;
        transform.rotation = worldRotation;

        if (wasCharacterControllerEnabled)
            _characterController.enabled = true;

        StopUpperBodyEmoteLayer();
        AnimState = Define.RangerAnimState.Idle00;
        StandUpAnimationRequested?.Invoke();
    }

    public void PlayReplicatedSitAnimation(Define.RangerAnimState seatedAnimState)
    {
        _isSeated = true;
        _seatedAnimState = seatedAnimState;
        _animState = _seatedAnimState;

        if (Anim != null)
            Anim.CrossFade(_animState.ToString(), EmotionCrossFadeDuration, 0, 0f);

        StopUpperBodyEmoteLayer();
    }

    public void PlayReplicatedStandUpAnimation()
    {
        _isSeated = false;
        _seatedAnimState = Define.RangerAnimState.Sit00;
        _moveInput = Vector2.zero;
        StopUpperBodyEmoteLayer();
        AnimState = Define.RangerAnimState.Idle00;
    }

    public void PlayReplicatedEmotion(Define.RangerAnimState emotionState)
    {
        if (IsSitState(_animState) && IsSeatedUpperBodyEmotionState(emotionState))
        {
            PlaySeatedUpperBodyEmotion(emotionState);
            return;
        }

        PlayFullBodyEmotion(emotionState);
    }

    public void RefreshUpperBodyEmoteLayer()
    {
        UpdateUpperBodyEmoteLayer();
    }

    public void StopUpperBodyEmoteLayer()
    {
        if (Anim == null || _upperBodyEmoteLayerIndex < 0)
            return;

        _upperBodyEmoteActive = false;
        _upperBodyEmoteEndTime = 0f;
        Anim.Play(GetUpperBodyIdleStatePath(), _upperBodyEmoteLayerIndex, 0f);
        Anim.SetLayerWeight(_upperBodyEmoteLayerIndex, 0f);
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

    private bool TryGetSeatedUpperBodyEmotionInput(out Define.RangerAnimState emotionState)
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
        if (_isSeated)
        {
            PlaySeatedUpperBodyEmotion(emotionState);
            return;
        }

        PlayFullBodyEmotion(emotionState);
    }

    private void PlayFullBodyEmotion(Define.RangerAnimState emotionState)
    {
        _animState = emotionState;

        if (Anim != null)
            Anim.CrossFade(emotionState.ToString(), EmotionCrossFadeDuration, 0, 0f);
    }

    private void PlaySeatedUpperBodyEmotion(Define.RangerAnimState emotionState)
    {
        if (!IsSeatedUpperBodyEmotionState(emotionState))
            return;

        if (Anim == null)
            return;

        if (_upperBodyEmoteLayerIndex < 0)
            _upperBodyEmoteLayerIndex = ResolveUpperBodyEmoteLayerIndex();

        if (_upperBodyEmoteLayerIndex < 0)
            return;

        Anim.SetLayerWeight(_upperBodyEmoteLayerIndex, 1f);
        Anim.Play(GetUpperBodyEmoteStatePath(emotionState), _upperBodyEmoteLayerIndex, 0f);
        _upperBodyEmoteActive = true;
        _upperBodyEmoteEndTime = Time.time + GetAnimationClipLength(emotionState);
    }

    private void UpdateUpperBodyEmoteLayer()
    {
        if (!_upperBodyEmoteActive)
            return;

        if (Time.time < _upperBodyEmoteEndTime)
            return;

        StopUpperBodyEmoteLayer();
    }

    private int ResolveUpperBodyEmoteLayerIndex()
    {
        if (Anim == null)
            return -1;

        return Anim.GetLayerIndex(UpperBodyEmoteLayerName);
    }

    private static string GetUpperBodyEmoteStatePath(Define.RangerAnimState emotionState)
    {
        return $"{UpperBodyEmoteStatePrefix}{emotionState}";
    }

    private static string GetUpperBodyIdleStatePath()
    {
        return UpperBodyIdleStateName;
    }

    private float GetAnimationClipLength(Define.RangerAnimState state)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null)
            return UpperBodyEmoteFallbackDuration;

        string clipName = state.ToString();
        AnimationClip[] clips = Anim.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == clipName)
                return Mathf.Max(clip.length, 0.1f);
        }

        return UpperBodyEmoteFallbackDuration;
    }

    public static bool IsEmotionState(Define.RangerAnimState state)
    {
        return state == Define.RangerAnimState.Emote00
            || state == Define.RangerAnimState.Emote01
            || state == Define.RangerAnimState.Emote02;
    }

    public static bool IsSitState(Define.RangerAnimState state)
    {
        return state == Define.RangerAnimState.Sit00
            || state == Define.RangerAnimState.Sit01
            || state == Define.RangerAnimState.Sit02;
    }

    public static bool IsSeatedUpperBodyEmotionState(Define.RangerAnimState state)
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

                if (isRangerColorMaterial)
                {
                    _materialPropertyBlock.SetColor("_Color", targetColor);
                    _materialPropertyBlock.SetColor("_BaseColor", targetColor);
                    _materialPropertyBlock.SetColor("_1st_ShadeColor", targetColor);
                }
                else
                {
                    _materialPropertyBlock.SetColor("_Color", targetColor);
                    _materialPropertyBlock.SetColor("_BaseColor", targetColor);
                    _materialPropertyBlock.SetFloat("_emissive", _faceEmissive);

                }

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

        if (material.HasProperty("_1st_ShadeColor"))
            return material.GetColor("_1st_ShadeColor");

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

    private void UpdateMouseYaw()
    {
        Vector2 lookInput = Managers.Input.ReadPlayerLookInput();
        if (Mathf.Abs(lookInput.x) <= 0.0001f)
            return;

        transform.Rotate(Vector3.up, lookInput.x * mouseYawSensitivity, Space.World);
    }

    private Vector3 GetTransformRelativeDirectionOnPlane(Vector2 moveInput)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            forward.Normalize();
        else
            forward = Vector3.forward;

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (right.sqrMagnitude > 0.0001f)
            right.Normalize();
        else
            right = Vector3.right;

        Vector3 direction = (right * moveInput.x) + (forward * moveInput.y);
        return Vector3.ClampMagnitude(direction, 1f);
    }
}
