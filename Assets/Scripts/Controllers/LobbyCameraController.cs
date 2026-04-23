using UnityEngine;

public class LobbyCameraController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _pivotOffset = new(0f, 1.6f, 0f);
    [SerializeField] private float _distance = 5.5f;
    [SerializeField] private float _mouseSensitivity = 0.12f;
    [SerializeField] private float _followLerpSpeed = 12f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 65f;
    [SerializeField] private bool _lockCursor = false;

    private float _yaw;
    private float _pitch = 18f;

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

        _yaw += mouseX * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - mouseY * _mouseSensitivity, _minPitch, _maxPitch);

        Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pivot = _target.position + _pivotOffset;
        Vector3 desiredPosition = pivot + orbitRotation * (Vector3.back * _distance);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-_followLerpSpeed * Time.deltaTime)
        );
        transform.LookAt(pivot);
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }
}
