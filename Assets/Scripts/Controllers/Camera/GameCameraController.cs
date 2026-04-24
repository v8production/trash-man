using UnityEngine;

[DisallowMultipleComponent]
public class GameCameraController : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private Transform _titanTarget;
    [SerializeField] private BossController _bossTarget;

    [Header("Framing")]
    [SerializeField] private Vector3 _titanPivotOffset = new(0f, 0.2f, 0f);
    [SerializeField] private Vector3 _bossLookOffset = new(0f, 0.2f, 0f);
    [SerializeField] private float _followDistance = 1f;
    [SerializeField] private float _heightOffset = 1f;
    [SerializeField] private float _lookAtBossWeight = 4f;

    [Header("Smoothing")]
    [SerializeField] private float _followLerpSpeed = 8f;
    [SerializeField] private float _rotationLerpSpeed = 10f;

    [Header("Fallback")]
    [SerializeField] private Vector3 _fallbackForward = Vector3.forward;
    [SerializeField] private float _minimumPlanarDistance = 0.25f;

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

        Vector3 titanPivot = _titanTarget.position + _titanPivotOffset;
        Vector3 titanForward = ResolvePlanarForward(titanPivot);
        Vector3 desiredLookPoint = ResolveLookPoint(titanPivot);

        desiredPosition = titanPivot - titanForward * _followDistance + Vector3.up * _heightOffset;

        Vector3 lookDirection = desiredLookPoint - desiredPosition;
        if (lookDirection.sqrMagnitude <= 0.0001f)
            lookDirection = titanForward;

        desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return true;
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
            return titanPivot;

        Vector3 bossPivot = _bossTarget.transform.position + _bossLookOffset;
        float bossWeight = Mathf.Clamp01(_lookAtBossWeight);
        return Vector3.Lerp(titanPivot, bossPivot, bossWeight);
    }
}
