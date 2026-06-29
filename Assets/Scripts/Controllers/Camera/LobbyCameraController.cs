using UnityEngine;

public class LobbyCameraController : MonoBehaviour
{
    private const int WallLayer = 7;

    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _pivotOffset = new(0f, 1.6f, 0f);
    [SerializeField] private float _distance = 3f;
    [SerializeField] private float _minDistance = 1.5f;
    [SerializeField] private float _maxDistance = 6f;
    [SerializeField] private float _scrollZoomSensitivity = 100f;
    [SerializeField] private float _mouseSensitivity = 0.12f;
    [SerializeField] private float _followLerpSpeed = 12f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 65f;
    [SerializeField] private bool _lockCursor = false;
    [SerializeField] private float _collisionDistanceScale = 0.8f;

    private float _yaw;
    private float _pitch = 18f;
    private Vector3 _teleportSmoothedPivot;
    private bool _isSmoothingTeleport;
    private readonly int _cameraBlockMask = 1 << WallLayer;
    private AudioListener _audioListener;

    private void Awake()
    {
        _audioListener = GetComponent<AudioListener>();
        ClaimAudioListener();
    }

    private void OnEnable()
    {
        ClaimAudioListener();
    }

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = Mathf.Clamp(euler.x, _minPitch, _maxPitch);

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
        float mouseX = lookInput.x;
        float mouseY = lookInput.y;
        float scrollY = Managers.Input.ReadMouseScrollY();

        _distance = Mathf.Clamp(_distance - scrollY * _scrollZoomSensitivity, _minDistance, _maxDistance);
        _yaw += mouseX * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - mouseY * _mouseSensitivity, _minPitch, _maxPitch);

        Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = _target.position + _pivotOffset;
        if (_isSmoothingTeleport)
        {
            _teleportSmoothedPivot = Vector3.Lerp(
                _teleportSmoothedPivot,
                pivot,
                1f - Mathf.Exp(-_followLerpSpeed * Time.deltaTime)
            );

            if ((_teleportSmoothedPivot - pivot).sqrMagnitude <= 0.0001f)
                _isSmoothingTeleport = false;

            pivot = _teleportSmoothedPivot;
        }

        Vector3 cameraOffset = orbitRotation * (Vector3.back * _distance);
        Vector3 desiredPosition = ResolveCameraPosition(pivot, cameraOffset);
        Vector3 smoothedPosition = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-_followLerpSpeed * Time.deltaTime)
        );

        transform.position = ResolveCameraPosition(pivot, smoothedPosition - pivot);
        transform.LookAt(pivot);
    }

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        _target = target;
        _isSmoothingTeleport = false;
        ClaimAudioListener();
    }

    public void SmoothNextTargetTeleport(Vector3 previousTargetWorldPosition)
    {
        _teleportSmoothedPivot = previousTargetWorldPosition + _pivotOffset;
        _isSmoothingTeleport = true;
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

    private Vector3 ResolveCameraPosition(Vector3 pivot, Vector3 cameraOffset)
    {
        if (cameraOffset.sqrMagnitude <= Define.epsilon)
            return pivot;

        RaycastHit[] hits = Physics.RaycastAll(pivot, cameraOffset, cameraOffset.magnitude, _cameraBlockMask);
        if (hits.Length == 0)
            return pivot + cameraOffset;

        float closestDistance = cameraOffset.magnitude;
        for (int i = 0; i < hits.Length; i++)
        {
            closestDistance = Mathf.Min(closestDistance, hits[i].distance);
        }

        return pivot + cameraOffset.normalized * (closestDistance * _collisionDistanceScale);
    }
}
