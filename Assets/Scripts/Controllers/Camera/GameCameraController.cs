using UnityEngine;

[DisallowMultipleComponent]
public class GameCameraController : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private Transform _titanTarget;
    [SerializeField] private BossController _bossTarget;

    [Header("Framing")]
    [SerializeField] private Vector3 _titanPivotOffset = new(0f, 1f, 0f);
    [SerializeField] private bool _useDynamicTitanCenter = true;
    [SerializeField] private Vector3 _dynamicTitanCenterOffset = Vector3.zero;
    [SerializeField] private Vector3 _lookPointOffset = new(0f, -0.15f, 0f);
    [SerializeField] private Vector3 _bossLookOffset = new(0f, 0.5f, 0f);
    [SerializeField] private float _followDistance = 2f;
    [SerializeField] private float _heightOffset = 1.1f;
    [SerializeField] private float _lookAtBossWeight = 0f;

    [Header("Smoothing")]
    [SerializeField] private float _followLerpSpeed = 8f;
    [SerializeField] private float _rotationLerpSpeed = 10f;

    [Header("Fallback")]
    [SerializeField] private Vector3 _fallbackForward = Vector3.forward;
    [SerializeField] private float _minimumPlanarDistance = 0.25f;

    private TitanRigRuntime _cachedTitanRuntime;
    private Renderer[] _cachedTitanRenderers = System.Array.Empty<Renderer>();

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

        if (!TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation))
            return;

        float followT = 1f - Mathf.Exp(-_followLerpSpeed * Time.deltaTime);
        float rotationT = 1f - Mathf.Exp(-_rotationLerpSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followT);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
    }

    public void SetTargets(Transform titanTarget, BossController bossTarget)
    {
        _titanTarget = titanTarget;
        _bossTarget = bossTarget;
        _cachedTitanRuntime = null;
        _cachedTitanRenderers = System.Array.Empty<Renderer>();

        if (TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation))
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
        }
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

        if (_bossTarget == null)
            _bossTarget = FindAnyObjectByType<BossController>();
    }

    private bool TryBuildCameraPose(out Vector3 desiredPosition, out Quaternion desiredRotation)
    {
        desiredPosition = transform.position;
        desiredRotation = transform.rotation;

        if (_titanTarget == null)
            return false;

        Vector3 titanPivot = ResolveTitanPivot();
        Vector3 titanForward = ResolvePlanarForward(titanPivot);
        Vector3 desiredLookPoint = ResolveLookPoint(titanPivot);

        desiredPosition = titanPivot - titanForward * _followDistance + Vector3.up * _heightOffset;

        Vector3 lookDirection = desiredLookPoint - desiredPosition;
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = titanForward;

        desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
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

    private Vector3 ResolvePlanarForward(Vector3 titanPivot)
    {
        if (_bossTarget != null)
        {
            Vector3 bossPivot = _bossTarget.transform.position + _bossLookOffset;
            Vector3 planarToBoss = Vector3.ProjectOnPlane(bossPivot - titanPivot, Vector3.up);
            if (planarToBoss.sqrMagnitude >= _minimumPlanarDistance * _minimumPlanarDistance)
                return planarToBoss.normalized;
        }

        Vector3 titanForward = Vector3.ProjectOnPlane(_titanTarget.forward, Vector3.up);
        if (titanForward.sqrMagnitude > 0.0001f)
            return titanForward.normalized;

        Vector3 fallbackForward = Vector3.ProjectOnPlane(_fallbackForward, Vector3.up);
        return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
    }

    private Vector3 ResolveLookPoint(Vector3 titanPivot)
    {
        if (_bossTarget == null)
            return titanPivot + _lookPointOffset;

        Vector3 bossPivot = _bossTarget.transform.position + _bossLookOffset;
        float bossWeight = Mathf.Clamp01(_lookAtBossWeight);
        return Vector3.Lerp(titanPivot, bossPivot, bossWeight) + _lookPointOffset;
    }
}
