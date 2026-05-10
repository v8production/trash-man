using Unity.VisualScripting;
using UnityEngine;

public class FootAttachmentController : MonoBehaviour
{
    private const string LogPrefix = "[TitanFootAttachment]";

    [Header("Foot References")]
    [SerializeField] private Transform footTransform;
    [SerializeField] private Transform bottomProbe;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask attachableGroundLayers = 1 << 6;
    [SerializeField] private float probeRadius = 0.18f;
    [SerializeField] private float probeDistance = 0.8f;
    [SerializeField] private float probeStartOffset = 0.05f;
    [SerializeField] private float maxAttachSurfaceDistance = 0.5f;
    [SerializeField] private float footSurfaceNormalToleranceDegrees = 35f;
    [SerializeField] private bool drawDebugGizmos;
    [SerializeField] private bool logAttachDetachTransitions = true;

    protected TitanBaseLegRoleController.LegSide side;
    private bool attachHeld;
    private bool isAttached;
    private Vector3 attachedWorldPosition;
    private Quaternion attachedWorldRotation;
    private Collider attachedCollider;

    public TitanBaseLegRoleController.LegSide Side => side;
    public bool IsAttached => isAttached;
    public bool AttachHeld => attachHeld;
    public Vector3 AttachedWorldPosition => attachedWorldPosition;
    public Quaternion AttachedWorldRotation => attachedWorldRotation;
    public Collider AttachedCollider => attachedCollider;
    public Transform FootTransform => footTransform;
    public Transform BottomProbe => bottomProbe != null ? bottomProbe : footTransform;

    private void LateUpdate()
    {
        RefreshAttachmentState();
    }

    public void SetAttachHeld(bool held)
    {
        if (attachHeld == held)
        {
            return;
        }

        attachHeld = held;
        if (!attachHeld)
        {
            Detach();
        }
    }

    public Vector3 GetCurrentContactPoint()
    {
        Transform probe = BottomProbe;
        return probe != null ? probe.position : Vector3.zero;
    }

    public bool TryGetGroundHit(out RaycastHit hit)
    {
        hit = default;
        Transform probe = BottomProbe;
        if (probe == null)
        {
            return false;
        }

        Vector3 origin = probe.position + (Vector3.up * probeStartOffset);
        float castDistance = probeStartOffset + probeDistance;

        // Use SphereCastAll so we can skip self-colliders reliably.
        RaycastHit[] hits = Physics.SphereCastAll(origin, probeRadius, Vector3.down, castDistance, attachableGroundLayers, QueryTriggerInteraction.Ignore);
        if (TrySelectBestGroundHit(hits, out hit))
        {
            return true;
        }

        return false;
    }

    private bool TrySelectBestGroundHit(RaycastHit[] hits, out RaycastHit hit)
    {
        hit = default;
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Transform root = transform.root;
        float bestDist = float.PositiveInfinity;
        int bestIndex = -1;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null)
            {
                continue;
            }

            // Skip any collider that belongs to this character.
            if (root != null && c.transform.IsChildOf(root))
            {
                continue;
            }

            float d = hits[i].distance;
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        hit = hits[bestIndex];
        return hit.collider != null;
    }

    public void RefreshAttachmentState()
    {
        if (!attachHeld)
        {
            return;
        }

        if (isAttached)
        {
            return;
        }

        if (!TryGetGroundHit(out RaycastHit hit) || !IsProbeCloseToSurface(hit) || !IsFootParallelToSurface(hit.normal))
        {
            return;
        }

        Attach(hit);
    }

    public void Attach(in RaycastHit hit)
    {
        Transform probe = BottomProbe;
        Transform foot = footTransform;
        if (probe == null || foot == null)
        {
            return;
        }

        attachedWorldPosition = GetCurrentContactPoint();
        attachedWorldRotation = foot.rotation;
        attachedCollider = hit.collider;
        isAttached = true;

        int layer = hit.collider.gameObject.layer;
        string layerName = LayerMask.LayerToName(layer);
        string layerLabel = string.IsNullOrWhiteSpace(layerName) ? layer.ToString() : $"{layerName}({layer})";
        if (logAttachDetachTransitions)
        {
            Debug.Log($"{InputDebug.Prefix} {LogPrefix} ATTACH SUCCESS side={side} layer={layerLabel} collider={hit.collider.name} point={hit.point} normal={hit.normal} storedPos={attachedWorldPosition} storedRot={attachedWorldRotation.eulerAngles}");
        }
    }

    public void Detach()
    {
        if (logAttachDetachTransitions)
        {
            Debug.Log($"{InputDebug.Prefix} {LogPrefix} side={side} DETACH attached={isAttached} collider={attachedCollider?.name ?? "<none>"} storedPos={attachedWorldPosition} storedRot={attachedWorldRotation.eulerAngles}");
        }
        isAttached = false;
        attachedCollider = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Transform probe = BottomProbe;
        if (probe == null)
        {
            return;
        }

        Vector3 origin = probe.position + (Vector3.up * probeStartOffset);
        Vector3 end = origin + (Vector3.down * (probeStartOffset + probeDistance));

        Gizmos.color = isAttached ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(origin, probeRadius);
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, probeRadius);

        if (isAttached)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(attachedWorldPosition, probeRadius);
        }
    }

    private bool IsProbeCloseToSurface(in RaycastHit hit)
    {
        Transform probe = BottomProbe;
        if (probe == null || hit.collider == null)
        {
            return false;
        }

        float maxDistance = Mathf.Max(0f, maxAttachSurfaceDistance);
        return Vector3.Distance(probe.position, hit.point) <= maxDistance;
    }

    private Vector3 GetSoleNormal()
    {
        Transform foot = footTransform;
        Transform probe = bottomProbe;
        if (foot == null)
        {
            return Vector3.zero;
        }

        if (probe != null && probe.localPosition.sqrMagnitude > 0.000001f)
        {
            return (-foot.TransformDirection(probe.localPosition)).normalized;
        }

        return foot.up;
    }

    private float GetFootSurfaceNormalAngle(Vector3 surfaceNormal)
    {
        Vector3 soleNormal = GetSoleNormal();
        if (soleNormal.sqrMagnitude < 0.0001f || surfaceNormal.sqrMagnitude < 0.0001f)
        {
            return 180f;
        }

        Vector3 normal = surfaceNormal.normalized;
        return Mathf.Min(
            Vector3.Angle(soleNormal, normal),
            Vector3.Angle(-soleNormal, normal));
    }

    private bool IsFootParallelToSurface(Vector3 surfaceNormal)
    {
        return GetFootSurfaceNormalAngle(surfaceNormal) <= footSurfaceNormalToleranceDegrees;
    }
}
