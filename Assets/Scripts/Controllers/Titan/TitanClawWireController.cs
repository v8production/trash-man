using System.Collections.Generic;
using UnityEngine;

public sealed class TitanClawWireController : MonoBehaviour
{
    [Header("Claw References")]
    [SerializeField] private Transform wireAnchor;
    [SerializeField] private Transform clawMount;
    [SerializeField] private string clawPrefabPath = "Prefabs/Claw";
    [SerializeField] private GameObject clawPrefab;

    [Header("Launch")]
    [SerializeField] private Vector3 launchLocalDirection = Vector3.right;
    [SerializeField] private float launchSpeed = 6f;
    [SerializeField] private float maxChainLength = 3f;
    [SerializeField] private float wireAnchorBackwardOffset = 0.05f;

    [Header("Retract")]
    [SerializeField] private float retractSpeed = 5f;
    [SerializeField] private float recoverDistance = 0.2f;

    [Header("Chain Mesh")]
    [SerializeField] private string chainPrefabPath = "Prefabs/Chain";
    [SerializeField] private float linkSpacing = 0.02f;
    [SerializeField] private int maxLinkCount = 100;
    [SerializeField] private Vector3 linkRotationOffsetEuler = Vector3.zero;

    private TitanClawWirePhase phase = TitanClawWirePhase.Idle;

    private GameObject spawnedClaw;
    private Rigidbody clawBody;
    private GameObject chainPrefab;
    private Transform chainRoot;
    private readonly List<Transform> chainLinks = new();
    private Renderer[] mountedClawRenderers;
    private bool[] mountedClawRendererStates;
    private Collider[] mountedClawColliders;
    private bool[] mountedClawColliderStates;
    private Vector3 mountedClawOriginalLocalPosition;
    private Quaternion mountedClawOriginalLocalRotation;
    private Vector3 mountedClawOriginalLocalScale;
    private bool hasMountedClawOriginalPose;

    private float currentLength;

    public bool CanLaunch => phase == TitanClawWirePhase.Idle;

    private void Awake()
    {
        ResolveReferences();
        EnsureChainPool();
        SetMountedClawVisible(true);
        HideChain();
    }

    public void TickServer(float dt)
    {
        switch (phase)
        {
            case TitanClawWirePhase.Launching:
                TickLaunching(dt);
                break;

            case TitanClawWirePhase.TetheredRetracting:
                TickTetheredRetracting(dt);
                break;
        }
    }

    private void TickLaunching(float dt)
    {
        if (clawBody == null)
        {
            FinishRetract();
            return;
        }

        currentLength = Mathf.Max(currentLength, maxChainLength);
        IntegrateClawMotion(dt, currentLength);
        AlignClawToVelocity();

        if (Vector3.Distance(GetAnchorPosition(), clawBody.position) >= currentLength - 0.01f)
            SetPhase(TitanClawWirePhase.TetheredRetracting);
    }

    private void TickTetheredRetracting(float dt)
    {
        if (clawBody == null)
        {
            FinishRetract();
            return;
        }

        IntegrateClawMotion(dt, currentLength);
        currentLength = Mathf.Max(0f, currentLength - retractSpeed * dt);
        ApplyLengthConstraint(currentLength);
        AlignClawToVelocity();

        if (currentLength <= recoverDistance || Vector3.Distance(GetAnchorPosition(), clawBody.position) <= recoverDistance)
            FinishRetract();
    }

    private void Update()
    {
        if (phase == TitanClawWirePhase.Idle || spawnedClaw == null)
        {
            HideChain();
            return;
        }

        Vector3 anchor = GetAnchorPosition();
        Vector3 clawPosition = spawnedClaw.transform.position;

        float ropeLength = phase == TitanClawWirePhase.Launching
            ? Vector3.Distance(anchor, clawPosition)
            : currentLength;
        RenderChain(anchor, clawPosition, ropeLength);
    }

    public bool TryLaunch(TitanStat stat)
    {
        if (!CanLaunch)
            return false;

        ResolveReferences();

        if (clawMount == null)
        {
            Debug.LogWarning("[TitanClawWire] Cannot launch: clawMount is missing.", this);
            return false;
        }

        GameObject source = clawPrefab != null ? clawPrefab : clawMount.gameObject;
        Vector3 launchDirection = GetLaunchDirection();
        spawnedClaw = Instantiate(source, clawMount.position, Quaternion.LookRotation(launchDirection, Vector3.up));
        spawnedClaw.name = $"{source.name}_Launched";
        spawnedClaw.SetActive(true);

        clawBody = spawnedClaw.GetComponent<Rigidbody>();
        if (clawBody == null)
            clawBody = spawnedClaw.AddComponent<Rigidbody>();

        clawBody.isKinematic = false;
        clawBody.useGravity = false;
        clawBody.linearVelocity = launchDirection * launchSpeed;
        clawBody.angularVelocity = Vector3.zero;

        SetPhase(TitanClawWirePhase.Launching);
        currentLength = maxChainLength;
        ShowChain();
        return true;
    }

    private void FinishRetract()
    {
        if (spawnedClaw != null)
            Destroy(spawnedClaw);

        spawnedClaw = null;
        clawBody = null;
        SetPhase(TitanClawWirePhase.Idle);
        currentLength = 0f;
        HideChain();
    }

    private void IntegrateClawMotion(float dt, float allowedLength)
    {
        clawBody.linearVelocity += Physics.gravity * dt;
        clawBody.position += clawBody.linearVelocity * dt;
        ApplyLengthConstraint(allowedLength);
    }

    private void ApplyLengthConstraint(float allowedLength)
    {
        if (clawBody == null)
            return;

        allowedLength = Mathf.Max(0f, allowedLength);
        Vector3 anchor = GetAnchorPosition();
        Vector3 toClaw = clawBody.position - anchor;
        float distance = toClaw.magnitude;

        if (distance <= allowedLength || distance <= 0.001f)
            return;

        Vector3 dir = toClaw / distance;

        clawBody.position = anchor + dir * allowedLength;

        float outwardSpeed = Vector3.Dot(clawBody.linearVelocity, dir);
        if (outwardSpeed > 0f)
            clawBody.linearVelocity -= dir * outwardSpeed;
    }

    private void AlignClawToVelocity()
    {
        if (clawBody == null)
            return;

        AlignClawTowards(clawBody.linearVelocity);
    }

    private void AlignClawTowards(Vector3 direction)
    {
        if (clawBody == null || direction.sqrMagnitude <= 0.0001f)
            return;

        clawBody.MoveRotation(Quaternion.LookRotation(direction.normalized, Vector3.up));
    }

    private Vector3 GetLaunchDirection()
    {
        Transform basis = clawMount != null ? clawMount : transform;
        Vector3 dir = basis.TransformDirection(launchLocalDirection);

        if (dir.sqrMagnitude < 0.001f)
            dir = basis.forward;

        return dir.normalized;
    }

    private Vector3 GetAnchorPosition()
    {
        if (wireAnchor != null)
            return wireAnchor.position - GetLaunchDirection() * wireAnchorBackwardOffset;

        if (clawMount != null)
            return clawMount.position - GetLaunchDirection() * wireAnchorBackwardOffset;

        return transform.position;
    }

    private void ResolveReferences()
    {
        if (clawMount == null && Managers.TitanRig != null)
            clawMount = Managers.TitanRig.Claw;

        if (wireAnchor == null)
            wireAnchor = clawMount;

        if (clawPrefab == null)
            clawPrefab = Resources.Load<GameObject>(NormalizeResourcesPath(clawPrefabPath));

        ResolveMountedClawVisuals();
    }

    private void ResolveMountedClawVisuals()
    {
        if (clawMount == null || hasMountedClawOriginalPose)
            return;

        mountedClawOriginalLocalPosition = clawMount.localPosition;
        mountedClawOriginalLocalRotation = clawMount.localRotation;
        mountedClawOriginalLocalScale = clawMount.localScale;

        mountedClawRenderers = clawMount.GetComponentsInChildren<Renderer>(true);
        mountedClawRendererStates = new bool[mountedClawRenderers.Length];
        for (int i = 0; i < mountedClawRenderers.Length; i++)
            mountedClawRendererStates[i] = mountedClawRenderers[i].enabled;

        mountedClawColliders = clawMount.GetComponentsInChildren<Collider>(true);
        mountedClawColliderStates = new bool[mountedClawColliders.Length];
        for (int i = 0; i < mountedClawColliders.Length; i++)
            mountedClawColliderStates[i] = mountedClawColliders[i].enabled;

        hasMountedClawOriginalPose = true;
    }

    private void EnsureChainPool()
    {
        if (chainRoot == null)
        {
            GameObject root = new GameObject("RightClaw_ChainMesh");
            chainRoot = root.transform;
            chainRoot.SetParent(transform, false);
        }

        string resourcePath = NormalizeResourcesPath(chainPrefabPath);
        if (chainPrefab == null)
            chainPrefab = Resources.Load<GameObject>(resourcePath);

        if (chainPrefab == null)
        {
            Debug.LogWarning($"[TitanClawWire] Chain prefab not found at Resources/{resourcePath}", this);
            return;
        }

        while (chainLinks.Count < maxLinkCount)
        {
            GameObject link = Instantiate(chainPrefab, chainRoot);
            link.SetActive(false);

            foreach (Collider col in link.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            chainLinks.Add(link.transform);
        }
    }

    private static string NormalizeResourcesPath(string path)
    {
        const string resourcesPrefix = "Assets/Resources/";

        if (string.IsNullOrWhiteSpace(path))
            return "Prefabs/Chain";

        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith(resourcesPrefix))
            normalized = normalized[resourcesPrefix.Length..];

        if (normalized.EndsWith(".prefab"))
            normalized = normalized[..^".prefab".Length];

        return normalized;
    }

    private void RenderChain(Vector3 start, Vector3 end, float ropeLength)
    {
        EnsureChainPool();

        Vector3 delta = end - start;
        float distance = delta.magnitude;

        if (distance <= 0.01f)
        {
            SetVisibleLinkCount(0);
            return;
        }

        Vector3 dir = delta / distance;
        float visibleLength = Mathf.Max(distance, ropeLength);
        float slack = Mathf.Max(0f, ropeLength - distance);
        float sag = slack * 0.5f + Mathf.Min(distance * 0.02f, 0.05f);
        int count = Mathf.Clamp(Mathf.FloorToInt(visibleLength / linkSpacing) + 1, 1, maxLinkCount);

        SetVisibleLinkCount(count);

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            Vector3 pos = GetSaggedRopePoint(start, end, sag, t);
            Vector3 tangent = GetSaggedRopeTangent(start, end, sag, t);

            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
            rot *= Quaternion.Euler(linkRotationOffsetEuler);

            if ((i & 1) == 1)
                rot *= Quaternion.Euler(0f, 0f, 90f);

            Transform link = chainLinks[i];
            link.SetPositionAndRotation(pos, rot);
        }
    }

    private static Vector3 GetSaggedRopePoint(Vector3 start, Vector3 end, float sag, float t)
    {
        Vector3 straight = Vector3.Lerp(start, end, t);
        return straight + Vector3.down * (Mathf.Sin(t * Mathf.PI) * sag);
    }

    private static Vector3 GetSaggedRopeTangent(Vector3 start, Vector3 end, float sag, float t)
    {
        Vector3 tangent = end - start;
        tangent += Vector3.down * (Mathf.Cos(t * Mathf.PI) * Mathf.PI * sag);
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
    }

    private void ShowChain()
    {
        if (chainRoot != null)
            chainRoot.gameObject.SetActive(true);
    }

    private void HideChain()
    {
        SetVisibleLinkCount(0);

        if (chainRoot != null)
            chainRoot.gameObject.SetActive(false);
    }

    private void SetVisibleLinkCount(int count)
    {
        for (int i = 0; i < chainLinks.Count; i++)
            chainLinks[i].gameObject.SetActive(i < count);
    }

    public TitanClawWireSnapshot GetSnapshot()
    {
        return new TitanClawWireSnapshot
        {
            Phase = phase,
            CurrentLength = currentLength,
            ClawPosition = spawnedClaw != null ? spawnedClaw.transform.position : GetAnchorPosition(),
            ClawRotation = spawnedClaw != null ? spawnedClaw.transform.rotation : Quaternion.identity,
        };
    }

    public void ApplySnapshot(TitanClawWireSnapshot snapshot)
    {
        ResolveReferences();

        if (snapshot.Phase == TitanClawWirePhase.Idle)
        {
            ApplyIdleSnapshot();
            return;
        }

        EnsureSnapshotClaw(snapshot);

        SetPhase(snapshot.Phase);
        currentLength = snapshot.CurrentLength;

        if (spawnedClaw != null)
        {
            spawnedClaw.transform.SetPositionAndRotation(snapshot.ClawPosition, snapshot.ClawRotation);
            ShowChain();
        }
    }

    private void ApplyIdleSnapshot()
    {
        if (spawnedClaw != null)
            Destroy(spawnedClaw);

        spawnedClaw = null;
        clawBody = null;
        SetPhase(TitanClawWirePhase.Idle);
        currentLength = 0f;

        HideChain();
    }

    private void EnsureSnapshotClaw(TitanClawWireSnapshot snapshot)
    {
        if (spawnedClaw == null)
        {
            GameObject source = clawPrefab != null ? clawPrefab : clawMount != null ? clawMount.gameObject : null;
            if (source == null)
                return;

            spawnedClaw = Instantiate(source, snapshot.ClawPosition, snapshot.ClawRotation);
            spawnedClaw.name = $"{source.name}_RemoteLaunched";
            spawnedClaw.SetActive(true);
        }

        clawBody = spawnedClaw.GetComponent<Rigidbody>();
        if (clawBody != null)
        {
            clawBody.isKinematic = true;
            clawBody.useGravity = false;
            clawBody.linearVelocity = Vector3.zero;
            clawBody.angularVelocity = Vector3.zero;
        }
    }

    private void SetMountedClawVisible(bool visible)
    {
        if (clawMount == null || !hasMountedClawOriginalPose)
            return;

        clawMount.localPosition = mountedClawOriginalLocalPosition;
        clawMount.localRotation = mountedClawOriginalLocalRotation;
        clawMount.localScale = visible ? mountedClawOriginalLocalScale : Vector3.zero;

        if (mountedClawRenderers != null)
        {
            for (int i = 0; i < mountedClawRenderers.Length; i++)
            {
                if (mountedClawRenderers[i] != null)
                    mountedClawRenderers[i].enabled = visible && mountedClawRendererStates[i];
            }
        }

        if (mountedClawColliders != null)
        {
            for (int i = 0; i < mountedClawColliders.Length; i++)
            {
                if (mountedClawColliders[i] != null)
                    mountedClawColliders[i].enabled = visible && mountedClawColliderStates[i];
            }
        }
    }

    private void SetPhase(TitanClawWirePhase nextPhase)
    {
        phase = nextPhase;
        SetMountedClawVisible(nextPhase == TitanClawWirePhase.Idle);
    }
}

public enum TitanClawWirePhase
{
    Idle = 0,
    Launching = 1,
    HitBlocked = 2,
    TetheredRetracting = 3,
    Retracting = 3,
}

public struct TitanClawWireSnapshot
{
    public TitanClawWirePhase Phase;
    public float CurrentLength;
    public Vector3 ClawPosition;
    public Quaternion ClawRotation;
}
