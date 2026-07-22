using UnityEngine;

public class LobbyCameraController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _firstPersonLocalPosition = new(0f, 1.75f, 0.08f);
    [SerializeField] private Vector3 _seatedFirstPersonLocalPosition = new(0f, 1.55f, -0.2f);
    [SerializeField] private Vector3 _initialViewEulerAngles = Vector3.zero;
    [SerializeField] private float _mouseSensitivity = 0.12f;
    [SerializeField] private float _minPitch = -75f;
    [SerializeField] private float _maxPitch = 75f;
    [SerializeField] private bool _lockCursor = false;

    private float _yaw;
    private float _pitch;
    private AudioListener _audioListener;
    private RangerController _targetRanger;

    private void Awake()
    {
        ApplyInitialViewAngles();
        _audioListener = GetComponent<AudioListener>();
        ClaimAudioListener();
    }

    private void OnEnable()
    {
        ClaimAudioListener();
    }

    private void Start()
    {
        if (_lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        Vector2 lookInput = Managers.Input.ReadPlayerLookInput();
        bool isTargetSeated = IsTargetSeated();
        if (isTargetSeated)
            _yaw += lookInput.x * _mouseSensitivity;
        else
            _yaw = _initialViewEulerAngles.y;

        _pitch = Mathf.Clamp(_pitch - lookInput.y * _mouseSensitivity, _minPitch, _maxPitch);

        transform.localPosition = GetCurrentLocalPosition();
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);

        if (_targetRanger != null)
            _targetRanger.SetSeatedLookRotation(isTargetSeated ? _yaw : 0f, isTargetSeated ? _pitch : 0f);
    }

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        _target = target;
        _targetRanger = _target != null ? _target.GetComponent<RangerController>() : null;
        transform.SetParent(_target, false);
        ClaimAudioListener();
        SnapToTarget();
    }
    public void SmoothNextTargetTeleport()
    {
        SnapToTarget();
    }

    public ViewRotation CaptureViewRotation()
    {
        return new ViewRotation(_yaw, _pitch);
    }

    public void RestoreViewRotation(ViewRotation viewRotation)
    {
        _yaw = viewRotation.Yaw;
        _pitch = Mathf.Clamp(viewRotation.Pitch, _minPitch, _maxPitch);
        SnapToTarget();
    }

    private void ClaimAudioListener()
    {
        if (_audioListener == null)
            _audioListener = GetComponent<AudioListener>();

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
                continue;

            listener.enabled = listener == _audioListener;
        }
    }

    private void ApplyInitialViewAngles()
    {
        _yaw = _initialViewEulerAngles.y;
        _pitch = Mathf.Clamp(_initialViewEulerAngles.x, _minPitch, _maxPitch);
    }

    private void SnapToTarget()
    {
        if (_target == null)
            return;

        transform.localPosition = GetCurrentLocalPosition();
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private Vector3 GetCurrentLocalPosition()
    {
        if (IsTargetSeated())
            return _seatedFirstPersonLocalPosition;

        return _firstPersonLocalPosition;
    }

    private bool IsTargetSeated()
    {
        return _targetRanger != null && RangerController.IsSitState(_targetRanger.AnimState);
    }

    public readonly struct ViewRotation
    {
        public readonly float Yaw;
        public readonly float Pitch;

        public ViewRotation(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
        }
    }
}
