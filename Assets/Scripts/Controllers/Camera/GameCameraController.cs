using UnityEngine;

[DisallowMultipleComponent]
public class GameCameraController : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private Transform _titanTarget;

    [Header("Framing")]
    [SerializeField] private Vector3 _titanPivotOffset = new(0f, 1f, 0f);
    [SerializeField] private bool _useDynamicTitanCenter = true;
    [SerializeField] private Vector3 _dynamicTitanCenterOffset = Vector3.zero;
    [SerializeField] private float _followDistance = 2f;
    [SerializeField] private float _minFollowDistance = 0.75f;
    [SerializeField] private float _maxFollowDistance = 6f;
    [SerializeField] private float _zoomSensitivity = 100f;
    [SerializeField] private float _heightOffset = 1.1f;

    [Header("Torso Look")]
    [SerializeField] private float _yawSensitivity = 0.12f;
    [SerializeField] private float _pitchSensitivity = 0.12f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 60f;

    [Header("Fallback")]
    [SerializeField] private Vector3 _fallbackForward = Vector3.forward;

    private TitanRigRuntime _cachedTitanRuntime;
    private Renderer[] _cachedTitanRenderers = System.Array.Empty<Renderer>();
    private float _cameraYaw;
    private float _cameraPitch;
    private bool _hasCameraAngles;
    private TorsoCameraStatePayload _lastPublishedCameraState;
    private bool _hasLastPublishedCameraState;
    private float _shakeTimeRemaining;
    private float _shakeDuration;
    private float _shakeAmplitude;

    public static void ShakeActiveCamera(float amplitude, float duration)
    {
        Camera mainCamera = Camera.main;
        GameCameraController controller = mainCamera != null ? mainCamera.GetComponent<GameCameraController>() : null;
        if (controller != null)
            controller.Shake(amplitude, duration);
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation))
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ApplyTorsoCameraInput();

        if (!TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation))
            return;

        transform.position = desiredPosition + GetShakeOffset();
        transform.rotation = desiredRotation;
    }

    public void SetTargets(Transform titanTarget, BossController _)
    {
        _titanTarget = titanTarget;
        _cachedTitanRuntime = null;
        _cachedTitanRenderers = System.Array.Empty<Renderer>();
        _hasCameraAngles = false;

        if (TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation))
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
        }
    }

    public void Shake(float amplitude, float duration)
    {
        _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
        _shakeDuration = Mathf.Max(0.01f, duration);
        _shakeTimeRemaining = Mathf.Max(_shakeTimeRemaining, duration);
    }

    private Vector3 GetShakeOffset()
    {
        if (_shakeTimeRemaining <= 0f)
            return Vector3.zero;

        _shakeTimeRemaining = Mathf.Max(0f, _shakeTimeRemaining - Time.deltaTime);
        float normalizedTime = _shakeTimeRemaining / _shakeDuration;
        float strength = _shakeAmplitude * normalizedTime * normalizedTime;
        return transform.right * Random.Range(-strength, strength) + transform.up * Random.Range(-strength, strength);
    }

    private void ResolveReferences()
    {
        if (_titanTarget == null)
        {
            Transform movementRoot = Managers.TitanRig.MovementRoot;
            if (movementRoot != null)
            {
                _titanTarget = movementRoot;
            }
            else
            {
                TitanController titanController = FindAnyObjectByType<TitanController>();
                if (titanController != null)
                    _titanTarget = titanController.transform;
            }
        }
    }

    private bool TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation)
    {
        desiredPosition = transform.position;
        desiredRotation = transform.rotation;

        if (_titanTarget == null)
            return false;

        Vector3 titanPivot = ResolveTitanPivot() + Vector3.up * _heightOffset;
        EnsureCameraAnglesInitialized();

        Quaternion orbitRotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
        Vector3 cameraOffset = orbitRotation * (Vector3.back * _followDistance);

        desiredPosition = titanPivot + cameraOffset;
        desiredRotation = Quaternion.LookRotation(titanPivot - desiredPosition, Vector3.up);
        return true;
    }

    private Vector3 ResolveTitanPivot()
    {
        if (_useDynamicTitanCenter && TryGetTitanVisualCenter(out Vector3 visualCenter))
            return visualCenter + _dynamicTitanCenterOffset;

        return _titanTarget.position + _titanPivotOffset;
    }

    private bool TryGetTitanVisualCenter(out Vector3 visualCenter)
    {
        visualCenter = Vector3.zero;

        TitanRigRuntime runtime = Managers.TitanRig.Runtime;
        if (runtime == null)
            runtime = _titanTarget.GetComponentInParent<TitanRigRuntime>();

        if (runtime == null)
            return false;

        CacheTitanRenderers(runtime);

        bool hasBounds = false;
        Bounds titanBounds = default;

        for (int i = 0; i < _cachedTitanRenderers.Length; i++)
        {
            Renderer titanRenderer = _cachedTitanRenderers[i];
            if (titanRenderer == null || !titanRenderer.enabled || !titanRenderer.gameObject.activeInHierarchy)
                continue;

            if (titanRenderer.GetComponentInParent<CameraBoundsIgnore>() != null)
                continue;

            if (!hasBounds)
            {
                titanBounds = titanRenderer.bounds;
                hasBounds = true;
                continue;
            }

            titanBounds.Encapsulate(titanRenderer.bounds);
        }

        if (!hasBounds)
            return false;

        visualCenter = titanBounds.center;
        return true;
    }

    private void CacheTitanRenderers(TitanRigRuntime runtime)
    {
        if (_cachedTitanRuntime == runtime && _cachedTitanRenderers.Length > 0)
            return;

        _cachedTitanRuntime = runtime;
        _cachedTitanRenderers = runtime.GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void ApplyTorsoCameraInput()
    {
        if (IsLocalTorsoActive())
        {
            Vector2 mouseDelta = Managers.Input.ReadTorsoLookInput();
            float scrollInput = Managers.Input.ReadMouseScrollY();

            ApplyCameraDelta(mouseDelta, scrollInput);
            EnsureCameraAnglesInitialized();
            PublishLocalCameraState();
            return;
        }

        if (Managers.TitanRole.TryGetTorsoCameraState(out TorsoCameraStatePayload cameraState))
        {
            _cameraYaw = cameraState.Yaw;
            _cameraPitch = Mathf.Clamp(cameraState.Pitch, _minPitch, _maxPitch);
            _followDistance = Mathf.Clamp(cameraState.Distance, _minFollowDistance, _maxFollowDistance);
            _hasCameraAngles = true;
            return;
        }
    }

    private static bool IsLocalTorsoActive()
    {
        LobbyNetworkPlayer localPlayer = LobbyNetworkPlayer.FindLocalOwnedPlayer();
        return localPlayer != null
            && localPlayer.TryGetActiveTitanRole(out Define.TitanRole activeRole)
            && activeRole == Define.TitanRole.Torso;
    }

    private void PublishLocalCameraState()
    {
        TorsoCameraStatePayload cameraState = new(_cameraYaw, _cameraPitch, _followDistance);
        if (_hasLastPublishedCameraState && _lastPublishedCameraState.Equals(cameraState))
            return;

        if (!LobbyNetworkPlayer.TryPublishLocalTorsoCameraState(cameraState))
            return;

        _lastPublishedCameraState = cameraState;
        _hasLastPublishedCameraState = true;
    }

    private void ApplyCameraDelta(Vector2 mouseDelta, float scrollInput)
    {
        if (mouseDelta.sqrMagnitude > 0f)
        {
            EnsureCameraAnglesInitialized();
            _cameraYaw += mouseDelta.x * _yawSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch - mouseDelta.y * _pitchSensitivity, _minPitch, _maxPitch);
        }

        if (!Mathf.Approximately(scrollInput, 0f))
            _followDistance = Mathf.Clamp(_followDistance - scrollInput * _zoomSensitivity, _minFollowDistance, _maxFollowDistance);
    }

    private void EnsureCameraAnglesInitialized()
    {
        if (_hasCameraAngles)
            return;

        Vector3 initialForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (initialForward.sqrMagnitude <= 0.0001f)
            initialForward = ResolveFallbackForward();

        _cameraYaw = Mathf.Atan2(initialForward.x, initialForward.z) * Mathf.Rad2Deg;
        _cameraPitch = Mathf.Clamp(transform.eulerAngles.x > 180f ? transform.eulerAngles.x - 360f : transform.eulerAngles.x, _minPitch, _maxPitch);
        _hasCameraAngles = true;
    }

    private Vector3 ResolveFallbackForward()
    {
        Vector3 titanForward = Vector3.zero;
        if (_titanTarget != null)
            titanForward = Vector3.ProjectOnPlane(_titanTarget.forward, Vector3.up);

        if (titanForward.sqrMagnitude > 0.0001f)
            return titanForward.normalized;

        Vector3 fallbackForward = Vector3.ProjectOnPlane(_fallbackForward, Vector3.up);
        return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
    }
}
