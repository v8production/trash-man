using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GameDevCameraController : MonoBehaviour
{
    [SerializeField] private Vector3 _targetOffset = new(0f, 1.5f, 0f);
    [SerializeField] private float _distance = 6f;
    [SerializeField] private float _yaw = 0f;
    [SerializeField] private float _pitch = 20f;
    [SerializeField] private float _mouseSensitivity = 3f;
    [SerializeField] private float _zoomSensitivity = 5f;
    [SerializeField] private float _minPitch = -20f;
    [SerializeField] private float _maxPitch = 70f;
    [SerializeField] private float _minDistance = 2f;
    [SerializeField] private float _maxDistance = 15f;

    private const float MouseDeltaScale = 0.02f;
    private const float ScrollDeltaScale = 1f / 120f;

    private void OnEnable()
    {
        SnapToTarget();
    }

    private void LateUpdate()
    {
        Transform target = transform.parent;
        if (target == null)
            return;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            _yaw += mouseDelta.x * _mouseSensitivity * MouseDeltaScale;
            _pitch -= mouseDelta.y * _mouseSensitivity * MouseDeltaScale;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        float scroll = mouse != null ? mouse.scroll.ReadValue().y * ScrollDeltaScale : 0f;
        if (scroll != 0f)
            _distance = Mathf.Clamp(_distance - scroll * _zoomSensitivity, _minDistance, _maxDistance);

        SnapToTarget();
    }

    private void SnapToTarget()
    {
        Transform target = transform.parent;
        if (target == null)
            return;

        _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

        Vector3 targetPosition = target.position + _targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);

        transform.position = targetPosition - orbitRotation * Vector3.forward * _distance;
        transform.rotation = Quaternion.LookRotation(targetPosition - transform.position, Vector3.up);
    }
}
