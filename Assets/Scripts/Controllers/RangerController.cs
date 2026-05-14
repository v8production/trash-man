using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RangerController : MonoBehaviour
{
    [Header("Actions (Player Map)")]
    [SerializeField] private string moveActionName = "Move";


    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotateLerpSpeed = 12f;

    private CharacterController _characterController;
    private LobbyCameraController _cameraController;
    private InputAction _moveAction;
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

        InputActionMap playerMap = Managers.Input.PlayerMap;
        if (playerMap != null)
            _moveAction = playerMap.FindAction(moveActionName, false);

        AnimState = Define.RangerAnimState.Idle_00;
        RangerFaceTextureStore.ApplyTo(gameObject);
        _initialized = true;
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
            AnimState = Define.RangerAnimState.Walk_00;
            return;
        }

        if (IsEmotionState(AnimState) && !IsCurrentAnimationFinished())
            return;

        AnimState = Define.RangerAnimState.Idle_00;
    }

    private void UpdateInput()
    {
        _moveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);
    }

    private bool TryGetEmotionInput(out Define.RangerAnimState emotionState)
    {
        emotionState = Define.RangerAnimState.Idle_00;

        if (Managers.Input.WasDigitPressedThisFrame(1))
        {
            emotionState = Define.RangerAnimState.Emotion_01;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(2))
        {
            emotionState = Define.RangerAnimState.Emotion_02;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(3))
        {
            emotionState = Define.RangerAnimState.Emotion_03;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(4))
        {
            emotionState = Define.RangerAnimState.Emotion_04;
            return true;
        }
        else if (Managers.Input.WasDigitPressedThisFrame(5))
        {
            emotionState = Define.RangerAnimState.Emotion_05;
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
        return state == Define.RangerAnimState.Emotion_01
            || state == Define.RangerAnimState.Emotion_02
            || state == Define.RangerAnimState.Emotion_03
            || state == Define.RangerAnimState.Emotion_04
            || state == Define.RangerAnimState.Emotion_05;
    }

    private void UpdateRotation(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.0001f)
            currentForward = moveDirection;

        Vector3 lerpedForward = Vector3.Lerp(currentForward.normalized, moveDirection.normalized, rotateLerpSpeed * Time.deltaTime);
        if (lerpedForward.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(lerpedForward.normalized, Vector3.up);
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

