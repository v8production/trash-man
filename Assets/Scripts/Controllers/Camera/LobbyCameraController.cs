using UnityEngine;

public class LobbyCameraController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _firstPersonLocalPosition = new(0f, 1.55f, 0.08f);
    [SerializeField] private Vector3 _initialViewEulerAngles = Vector3.zero;
    [SerializeField] private float _mouseSensitivity = 0.12f;
    [SerializeField] private float _minPitch = -75f;
    [SerializeField] private float _maxPitch = 75f;
    [SerializeField] private bool _lockCursor = false;

    private float _yaw;
    private float _pitch;
    private AudioListener _audioListener;

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
        _pitch = Mathf.Clamp(_pitch - lookInput.y * _mouseSensitivity, _minPitch, _maxPitch);

        transform.localPosition = _firstPersonLocalPosition;
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        _target = target;
        transform.SetParent(_target, false);
        ClaimAudioListener();
        SnapToTarget();
    }

    public void SetFirstPersonLocalPosition(Vector3 localPosition)
    {
        _firstPersonLocalPosition = localPosition;
        SnapToTarget();
    }

    public void SmoothNextTargetTeleport(Vector3 previousTargetWorldPosition)
    {
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

        transform.localPosition = _firstPersonLocalPosition;
        transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
