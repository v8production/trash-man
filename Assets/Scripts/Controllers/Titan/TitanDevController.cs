using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitanDevController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _turnSpeed = 720f;

    private Transform _cameraTransform;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        Time.timeScale = 1f;
        Managers.Input.SetMode(Define.InputMode.Player);
    }

    private void FixedUpdate()
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

        Vector2 moveInput = Managers.Input.ReadTitanDevMoveInput();
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        if (moveDirection.sqrMagnitude <= 0f)
            return;

        moveDirection.Normalize();
        Vector3 nextPosition = transform.position + moveDirection * (_moveSpeed * Time.fixedDeltaTime);

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.fixedDeltaTime);

        if (_rigidbody != null)
        {
            _rigidbody.MovePosition(nextPosition);
            _rigidbody.MoveRotation(nextRotation);
            return;
        }

        transform.SetPositionAndRotation(nextPosition, nextRotation);
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
