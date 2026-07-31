using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitanDevController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _turnSpeed = 720f;

    private Transform _cameraTransform;

    private void Update()
    {
        Transform cameraTransform = ResolveCameraTransform();
        Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : Vector3.forward;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 right = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up) : Vector3.right;
        if (right.sqrMagnitude <= 0.0001f)
            right = Vector3.Cross(Vector3.up, forward);
        right.Normalize();

        Vector2 moveInput = Managers.Input.ReadRangerMoveInput();
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        if (moveDirection.sqrMagnitude <= 0f)
            return;

        moveDirection.Normalize();
        transform.position += moveDirection * (_moveSpeed * Time.deltaTime);

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
    }

    private Transform ResolveCameraTransform()
    {
        if (_cameraTransform != null)
            return _cameraTransform;

        GameDevCameraController devCamera = GetComponentInChildren<GameDevCameraController>();
        if (devCamera != null)
        {
            _cameraTransform = devCamera.transform;
            return _cameraTransform;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            _cameraTransform = mainCamera.transform;

        return _cameraTransform;
    }
}
