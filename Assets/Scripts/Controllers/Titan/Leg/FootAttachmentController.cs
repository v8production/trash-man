using UnityEngine;

public sealed class FootAttachmentController : MonoBehaviour
{
    private const string LogPrefix = "[TitanFootAttachment]";

    [Header("Role")]
    [SerializeField] private TitanBaseLegRoleController.LegSide side;

    [Header("Foot References")]
    [SerializeField] private Transform footTransform;
    [SerializeField] private Transform bottomProbe;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask attachableGroundLayers = 1 << 6;
    [SerializeField] private float probeRadius = 0.08f;
    [SerializeField] private float probeDistance = 0.18f;
    [SerializeField] private float probeStartOffset = 0.05f;
    [SerializeField] private bool drawDebugGizmos;

    private bool detachHeld;
    private bool isAttached;
    private Vector3 attachedWorldPosition;
    private Quaternion attachedWorldRotation;
    private Collider attachedCollider;

    public TitanBaseLegRoleController.LegSide Side => side;
    public bool IsAttached => isAttached;
    public bool DetachHeld => detachHeld;
    public Vector3 AttachedWorldPosition => attachedWorldPosition;
    public Quaternion AttachedWorldRotation => attachedWorldRotation;
    public Collider AttachedCollider => attachedCollider;
    public Transform FootTransform => footTransform;
    public Transform BottomProbe => bottomProbe != null ? bottomProbe : footTransform;

    private void LateUpdate()
    {
        RefreshAttachmentState();
    }

    public void SetDetachHeld(bool held)
    {
        if (detachHeld == held)
        {
            return;
        }

        InputDebug.Log($"{LogPrefix} side={side} detachHeld {detachHeld} -> {held} attached={isAttached} contact={GetCurrentContactPoint()} collider={attachedCollider?.name ?? "<none>"}");
        detachHeld = held;
        if (detachHeld)
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
        return Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, castDistance, attachableGroundLayers, QueryTriggerInteraction.Ignore)
               && hit.collider != null;
    }

    public void RefreshAttachmentState()
    {
        if (detachHeld)
        {
            InputDebug.Log($"{LogPrefix} side={side} reattach blocked: detach held.");
            return;
        }

        if (isAttached)
        {
            return;
        }

        if (!TryGetGroundHit(out RaycastHit hit))
        {
            InputDebug.Log($"{LogPrefix} side={side} no valid ground hit for attach. probe={GetCurrentContactPoint()}");
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

        attachedWorldPosition = hit.point;
        attachedWorldRotation = foot.rotation;
        attachedCollider = hit.collider;
        isAttached = true;
        InputDebug.Log($"{LogPrefix} side={side} attached collider={hit.collider.name} point={hit.point} normal={hit.normal} storedPos={attachedWorldPosition} storedRot={attachedWorldRotation.eulerAngles}");
    }

    public void Detach()
    {
        InputDebug.Log($"{LogPrefix} side={side} detach attached={isAttached} collider={attachedCollider?.name ?? "<none>"} storedPos={attachedWorldPosition} storedRot={attachedWorldRotation.eulerAngles}");
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
}
