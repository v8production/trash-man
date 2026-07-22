using UnityEngine;
using UnityEngine.Serialization;

public readonly struct FootGroundContact
{
    public readonly Collider Collider;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly float Distance;

    public FootGroundContact(
        Collider collider,
        Vector3 point,
        Vector3 normal,
        float distance)
    {
        Collider = collider;
        Point = point;
        Normal = normal;
        Distance = distance;
    }
}

public class FootAttachmentController : MonoBehaviour
{
    [SerializeField] private Transform footTransform;

    [Header("Ground Discovery")]
    [SerializeField] private Transform bottomProbe;

    [Header("Authoritative Sole Geometry")]
    [SerializeField] private Transform[] soleContactPoints;
    [FormerlySerializedAs("soleColliders")]
    [SerializeField] private Collider[] penetrationDiagnosticColliders;

    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeStartOffset = 0.08f;
    [SerializeField] private float probeDistance = 0.15f;
    [SerializeField] private float probeRadius = 0.04f;
    [SerializeField, Range(0f, 89f)] private float maxGroundSlope = 60f;

    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private Vector3[] localSolePoints;
    private int cachedSolePointCount;
    private Rigidbody ownerRigidbody;
    private Transform ownerRoot;

    protected TitanBaseLegRoleController.LegSide side;

    public TitanBaseLegRoleController.LegSide Side => side;
    public Transform FootTransform => footTransform;
    public Transform BottomProbe => bottomProbe != null ? bottomProbe : footTransform;
    public int SoleContactPointCount => cachedSolePointCount;
    public Collider[] PenetrationDiagnosticColliders => penetrationDiagnosticColliders;

    protected virtual void Awake()
    {
        ownerRigidbody = GetComponentInParent<Rigidbody>();
        ownerRoot = ownerRigidbody != null ? ownerRigidbody.transform : transform.root;
        ResolveSerializedSoleWitnessesIfNeeded();
        CacheSolePoints();
        ValidateDiagnosticColliderAuthoring();
    }

    private void OnValidate()
    {
        ResolveSerializedSoleWitnessesIfNeeded();
        CacheSolePoints();
        ValidateSoleAuthoring();
        ValidateDiagnosticColliderAuthoring();
    }

    public void RebuildCachedSoleGeometry()
    {
        CacheSolePoints();
    }

    public bool TryGetGroundContact(Vector3 up, out FootGroundContact contact)
    {
        contact = default;
        Transform probe = BottomProbe;
        if (probe == null)
        {
            return false;
        }

        Vector3 normalizedUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        Vector3 origin = probe.position + normalizedUp * probeStartOffset;
        float castDistance = probeStartOffset + probeDistance;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            -normalizedUp,
            groundHits,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;
        bool found = false;
        float minimumSlopeDot = Mathf.Cos(maxGroundSlope * Mathf.Deg2Rad);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (IsOwnedCollider(hit.collider, hit.rigidbody))
            {
                continue;
            }

            float slopeDot = Vector3.Dot(hit.normal.normalized, normalizedUp);
            if (slopeDot < minimumSlopeDot)
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestHit = hit;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        Vector3 contactPoint = TryRefineGroundPoint(normalizedUp, bestHit.collider, out Vector3 refinedPoint)
            ? refinedPoint
            : bestHit.point;
        contact = new FootGroundContact(
            bestHit.collider,
            contactPoint,
            bestHit.normal,
            bestHit.distance);
        return true;
    }

    public bool TryGetGroundContactRobust(
        Vector3 up,
        bool hasExpectedSurface,
        Vector3 expectedSurfacePoint,
        Collider expectedCollider,
        out FootGroundContact contact)
    {
        if (TryGetGroundContact(up, out contact))
        {
            return true;
        }

        if (!hasExpectedSurface)
        {
            return false;
        }

        Transform probe = BottomProbe;
        if (probe == null)
        {
            return false;
        }

        Vector3 normalizedUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        float surfaceHeight = Vector3.Dot(expectedSurfacePoint, normalizedUp);
        float recoveryRayStartHeight = probeStartOffset + probeRadius + 0.05f;
        Vector3 rayOrigin = Vector3.ProjectOnPlane(probe.position, normalizedUp)
            + normalizedUp * (surfaceHeight + recoveryRayStartHeight);
        float rayDistance = recoveryRayStartHeight + probeDistance + probeRadius;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            -normalizedUp,
            groundHits,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;
        bool found = false;
        float minimumSlopeDot = Mathf.Cos(maxGroundSlope * Mathf.Deg2Rad);
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || IsOwnedCollider(hit.collider, hit.rigidbody))
                {
                    continue;
                }

                if (pass == 0 && (expectedCollider == null || hit.collider != expectedCollider || !expectedCollider.enabled))
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal.normalized, normalizedUp) < minimumSlopeDot || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }

            if (found)
            {
                contact = new FootGroundContact(bestHit.collider, bestHit.point, bestHit.normal, bestHit.distance);
                return true;
            }
        }

        return false;
    }

    private bool TryRefineGroundPoint(Vector3 normalizedUp, Collider expectedCollider, out Vector3 point)
    {
        point = default;
        Transform probe = BottomProbe;
        if (probe == null || expectedCollider == null)
        {
            return false;
        }

        float probeHeight = Vector3.Dot(probe.position, normalizedUp);
        Vector3 origin = Vector3.ProjectOnPlane(probe.position, normalizedUp)
            + normalizedUp * (probeHeight + probeStartOffset + probeRadius + 0.02f);
        float distance = probeStartOffset + probeDistance + probeRadius + 0.04f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            -normalizedUp,
            groundHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider != expectedCollider || hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            point = hit.point;
            found = true;
        }

        return found;
    }

    public Vector3 GetSolePointWorld(int index)
    {
        if (footTransform == null || index < 0 || index >= cachedSolePointCount)
        {
            return footTransform != null ? footTransform.position : transform.position;
        }

        return footTransform.TransformPoint(localSolePoints[index]);
    }

    public float GetMinimumSignedSoleGap(Vector3 groundPlanePoint, Vector3 groundUp)
    {
        Vector3 normalizedUp = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
        int count = Mathf.Max(1, cachedSolePointCount);
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = cachedSolePointCount > 0 ? GetSolePointWorld(i) : BottomProbe.position;
            minimum = Mathf.Min(minimum, Vector3.Dot(point - groundPlanePoint, normalizedUp));
        }

        return minimum;
    }

    public float GetMaximumSignedSoleGap(Vector3 groundPlanePoint, Vector3 groundUp)
    {
        Vector3 normalizedUp = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
        int count = Mathf.Max(1, cachedSolePointCount);
        float maximum = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = cachedSolePointCount > 0 ? GetSolePointWorld(i) : BottomProbe.position;
            maximum = Mathf.Max(maximum, Vector3.Dot(point - groundPlanePoint, normalizedUp));
        }

        return maximum;
    }

    public float GetSoleDepthBelowPivot(Vector3 groundUp)
    {
        Vector3 normalizedUp = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
        Vector3 pivot = footTransform != null ? footTransform.position : transform.position;
        int count = Mathf.Max(1, cachedSolePointCount);
        float depth = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = cachedSolePointCount > 0 ? GetSolePointWorld(i) : BottomProbe.position;
            depth = Mathf.Max(depth, Vector3.Dot(pivot - point, normalizedUp));
        }

        return depth;
    }

    public Vector3 ComputePivotTargetForGroundPlane(
        Vector3 desiredPivotTarget,
        Vector3 groundPlanePoint,
        Vector3 groundUp,
        float clearance)
    {
        Vector3 normalizedUp = groundUp.sqrMagnitude > 0.0001f ? groundUp.normalized : Vector3.up;
        float desiredPivotHeight = Vector3.Dot(groundPlanePoint, normalizedUp)
            + GetSoleDepthBelowPivot(normalizedUp)
            + clearance;
        return Vector3.ProjectOnPlane(desiredPivotTarget, normalizedUp) + normalizedUp * desiredPivotHeight;
    }

    public bool TryGetMaximumSolePenetration(
        Collider groundCollider,
        out float maximumPenetration,
        out Vector3 separationDirection)
    {
        maximumPenetration = 0f;
        separationDirection = Vector3.zero;
        if (groundCollider == null || penetrationDiagnosticColliders == null)
        {
            return false;
        }

        Vector3 up = Vector3.up;
        bool found = false;
        for (int i = 0; i < penetrationDiagnosticColliders.Length; i++)
        {
            Collider diagnosticCollider = penetrationDiagnosticColliders[i];
            if (diagnosticCollider == null || !diagnosticCollider.enabled)
            {
                continue;
            }

            if (!Physics.ComputePenetration(
                    diagnosticCollider,
                    diagnosticCollider.transform.position,
                    diagnosticCollider.transform.rotation,
                    groundCollider,
                    groundCollider.transform.position,
                    groundCollider.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                continue;
            }

            if (Vector3.Dot(direction, up) <= 0.1f || distance <= maximumPenetration)
            {
                continue;
            }

            maximumPenetration = distance;
            separationDirection = direction;
            found = true;
        }

        return found;
    }

    private void CacheSolePoints()
    {
        if (footTransform == null)
        {
            cachedSolePointCount = 0;
            return;
        }

        int sourceCount = soleContactPoints != null ? soleContactPoints.Length : 0;
        if (localSolePoints == null || localSolePoints.Length != sourceCount)
        {
            localSolePoints = new Vector3[sourceCount];
        }

        cachedSolePointCount = 0;
        for (int i = 0; i < sourceCount; i++)
        {
            Transform point = soleContactPoints[i];
            if (point == null)
            {
                continue;
            }

            localSolePoints[cachedSolePointCount++] = footTransform.InverseTransformPoint(point.position);
        }

        if (cachedSolePointCount < 4 && bottomProbe != null && Application.isPlaying)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[{nameof(FootAttachmentController)}] {name} needs at least four authoritative sole contact points. Falling back to BottomProbe for runtime safety.", this);
#endif
        }
    }

    private void ResolveSerializedSoleWitnessesIfNeeded()
    {
        if (footTransform == null || HasFourValidSoleWitnesses())
        {
            return;
        }

        string prefix = this is TitanRightFootAttachmentController ? "Right" : "Left";
        Transform searchRoot = ownerRoot != null ? ownerRoot : transform.root;
        Transform[] resolved =
        {
            FindSoleWitness(searchRoot, footTransform, $"{prefix}SoleHeelLeft"),
            FindSoleWitness(searchRoot, footTransform, $"{prefix}SoleHeelRight"),
            FindSoleWitness(searchRoot, footTransform, $"{prefix}SoleToeLeft"),
            FindSoleWitness(searchRoot, footTransform, $"{prefix}SoleToeRight"),
        };

        for (int i = 0; i < resolved.Length; i++)
        {
            if (resolved[i] == null)
            {
                return;
            }
        }

        soleContactPoints = resolved;
    }

    private bool HasFourValidSoleWitnesses()
    {
        if (soleContactPoints == null || soleContactPoints.Length < 4)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < soleContactPoints.Length; i++)
        {
            if (soleContactPoints[i] != null)
            {
                validCount++;
            }
        }

        return validCount >= 4;
    }

    private static Transform FindSoleWitness(Transform searchRoot, Transform preferredRoot, string witnessName)
    {
        Transform found = FindChildByName(preferredRoot, witnessName);
        return found != null ? found : FindChildByName(searchRoot, witnessName);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void ValidateSoleAuthoring()
    {
        if (footTransform == null || soleContactPoints == null || soleContactPoints.Length == 0)
        {
            return;
        }

        Vector3 up = Vector3.up;
        float minForward = float.PositiveInfinity;
        float maxForward = float.NegativeInfinity;
        float minRight = float.PositiveInfinity;
        float maxRight = float.NegativeInfinity;
        for (int i = 0; i < soleContactPoints.Length; i++)
        {
            Transform point = soleContactPoints[i];
            if (point == null)
            {
                if (Application.isPlaying)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[{nameof(FootAttachmentController)}] {name} has a null sole witness.", this);
#endif
                }
                continue;
            }

            Vector3 local = footTransform.InverseTransformPoint(point.position);
            minForward = Mathf.Min(minForward, local.z);
            maxForward = Mathf.Max(maxForward, local.z);
            minRight = Mathf.Min(minRight, local.x);
            maxRight = Mathf.Max(maxRight, local.x);
        }

        if (maxForward - minForward <= 0.001f || maxRight - minRight <= 0.001f)
        {
            if (Application.isPlaying)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[{nameof(FootAttachmentController)}] {name} sole witnesses must span front/back and left/right extents.", this);
#endif
            }
        }

        if (penetrationDiagnosticColliders != null)
        {
            Transform root = ownerRoot != null ? ownerRoot : transform.root;
            for (int i = 0; i < penetrationDiagnosticColliders.Length; i++)
            {
                Collider collider = penetrationDiagnosticColliders[i];
                if (collider != null && root != null && !collider.transform.IsChildOf(root))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[{nameof(FootAttachmentController)}] {collider.name} is not under the owning Titan hierarchy.", this);
#endif
                }
            }
        }

        if (bottomProbe != null && cachedSolePointCount > 0)
        {
            float probeGap = Vector3.Dot(bottomProbe.position - footTransform.position, up) + GetSoleDepthBelowPivot(up);
            if (probeGap > 0.01f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[{nameof(FootAttachmentController)}] {name} BottomProbe is materially above the authoritative sole ({probeGap:0.0000}m).", this);
#endif
            }
        }
    }

    private void ValidateDiagnosticColliderAuthoring()
    {
        if (penetrationDiagnosticColliders == null)
        {
            return;
        }

        Rigidbody owningBody = ownerRigidbody != null ? ownerRigidbody : GetComponentInParent<Rigidbody>();
        for (int i = 0; i < penetrationDiagnosticColliders.Length; i++)
        {
            Collider diagnosticCollider = penetrationDiagnosticColliders[i];
            if (diagnosticCollider == null
                || diagnosticCollider.isTrigger
                || diagnosticCollider.attachedRigidbody != owningBody)
            {
                continue;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[FootAttachmentController] Penetration diagnostic collider '{diagnosticCollider.name}' is an enabled non-trigger collider attached to the movement Rigidbody, so it participates in the Titan compound collision body. Use existing intentional physical foot colliders only, or make diagnostics trigger-only/detached.", diagnosticCollider);
#endif
        }
    }

    private bool IsOwnedCollider(Collider hitCollider, Rigidbody hitRigidbody)
    {
        if (ownerRigidbody != null && (hitRigidbody == ownerRigidbody || hitCollider.attachedRigidbody == ownerRigidbody))
        {
            return true;
        }

        return ownerRoot != null && hitCollider.transform.IsChildOf(ownerRoot);
    }

}
