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
    [SerializeField] private float launchSpeed = 10f;
    [SerializeField] private float maxChainLength = 3f;
    [SerializeField] private float hangDuration = 0.35f;
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
    private readonly List<Vector3> clawPath = new();
    private Renderer[] mountedClawRenderers;
    private bool[] mountedClawRendererStates;
    private Collider[] mountedClawColliders;
    private bool[] mountedClawColliderStates;
    private Vector3 mountedClawOriginalLocalPosition;
    private Quaternion mountedClawOriginalLocalRotation;
    private Vector3 mountedClawOriginalLocalScale;
    private bool hasMountedClawOriginalPose;

    private float hangTimer;
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

            case TitanClawWirePhase.HitBlocked:
                TickHanging(dt);
                break;

            case TitanClawWirePhase.Retracting:
                TickRetracting(dt);
                break;
        }
    }

    private void TickLaunching(float dt)
    {
        ApplyMaxLengthConstraint();
        AlignClawToVelocity();

        currentLength = Vector3.Distance(GetAnchorPosition(), clawBody.position);

        if (currentLength >= maxChainLength * 0.98f)
        {
            SetPhase(TitanClawWirePhase.HitBlocked);
            hangTimer = 0f;
        }
    }

    private void TickHanging(float dt)
    {
        ApplyMaxLengthConstraint();
        AlignClawToVelocity();

        hangTimer += dt;
        currentLength = Vector3.Distance(GetAnchorPosition(), clawBody.position);

        if (hangTimer >= hangDuration)
            BeginRetract();
    }

    private void TickRetracting(float dt)
    {
        if (clawBody == null)
        {
            FinishRetract();
            return;
        }

        Vector3 anchor = GetAnchorPosition();

        Vector3 previous = clawBody.position;
        Vector3 next = Vector3.MoveTowards(
            previous,
            anchor,
            retractSpeed * dt
        );

        clawBody.MovePosition(next);
        AlignClawTowards(previous - next);
        currentLength = Vector3.Distance(anchor, next);

        if (currentLength <= recoverDistance)
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

        if (phase == TitanClawWirePhase.Retracting)
        {
            RenderChain(anchor, clawPosition);
            return;
        }

        AddClawPathPoint(clawPosition);
        RenderChain(anchor, clawPosition, clawPath);
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
        clawBody.useGravity = true;
        clawBody.linearVelocity = launchDirection * launchSpeed;
        clawBody.angularVelocity = Vector3.zero;

        SetPhase(TitanClawWirePhase.Launching);
        hangTimer = 0f;
        currentLength = 0f;
        ResetClawPath(GetAnchorPosition(), spawnedClaw.transform.position);

        ShowChain();
        return true;
    }

    private void BeginRetract()
    {
        if (clawBody == null)
        {
            FinishRetract();
            return;
        }

        SetPhase(TitanClawWirePhase.Retracting);

        clawBody.useGravity = false;
        clawBody.linearVelocity = Vector3.zero;
        clawBody.angularVelocity = Vector3.zero;
    }

    private void FinishRetract()
    {
        if (spawnedClaw != null)
            Destroy(spawnedClaw);

        spawnedClaw = null;
        clawBody = null;
        SetPhase(TitanClawWirePhase.Idle);
        currentLength = 0f;
        clawPath.Clear();

        HideChain();
    }

    private void ResetClawPath(Vector3 anchor, Vector3 clawPosition)
    {
        clawPath.Clear();
        clawPath.Add(anchor);

        if (Vector3.Distance(anchor, clawPosition) > 0.01f)
            clawPath.Add(clawPosition);
    }

    private void AddClawPathPoint(Vector3 clawPosition)
    {
        if (clawPath.Count == 0)
        {
            clawPath.Add(GetAnchorPosition());
            clawPath.Add(clawPosition);
            return;
        }

        int lastIndex = clawPath.Count - 1;
        float minSpacing = Mathf.Max(0.01f, linkSpacing * 0.5f);

        if (Vector3.Distance(clawPath[lastIndex], clawPosition) <= minSpacing)
        {
            clawPath[lastIndex] = clawPosition;
            return;
        }

        clawPath.Add(clawPosition);
    }

    private void ApplyMaxLengthConstraint()
    {
        if (clawBody == null)
            return;

        Vector3 anchor = GetAnchorPosition();
        Vector3 toClaw = clawBody.position - anchor;
        float distance = toClaw.magnitude;

        if (distance <= maxChainLength || distance <= 0.001f)
            return;

        Vector3 dir = toClaw / distance;

        clawBody.position = anchor + dir * maxChainLength;

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

    private void RenderChain(Vector3 start, Vector3 end)
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
        int count = Mathf.Clamp(Mathf.FloorToInt(distance / linkSpacing) + 1, 1, maxLinkCount);

        SetVisibleLinkCount(count);

        for (int i = 0; i < count; i++)
        {
            float offset = count > 1 ? distance * i / (count - 1) : 0f;
            Vector3 pos = start + dir * offset;

            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            rot *= Quaternion.Euler(linkRotationOffsetEuler);

            if ((i & 1) == 1)
                rot *= Quaternion.Euler(0f, 0f, 90f);

            Transform link = chainLinks[i];
            link.SetPositionAndRotation(pos, rot);
        }
    }

    private void RenderChain(Vector3 start, Vector3 end, List<Vector3> path)
    {
        EnsureChainPool();

        if (path.Count < 2)
        {
            RenderChain(start, end);
            return;
        }

        path[0] = start;
        path[^1] = end;

        float pathLength = GetPathLength(path);
        if (pathLength <= 0.01f)
        {
            SetVisibleLinkCount(0);
            return;
        }

        int count = Mathf.Clamp(Mathf.FloorToInt(pathLength / linkSpacing) + 1, 1, maxLinkCount);
        SetVisibleLinkCount(count);

        for (int i = 0; i < count; i++)
        {
            float distance = count > 1 ? pathLength * i / (count - 1) : 0f;
            GetPointOnPath(path, distance, out Vector3 pos, out Vector3 tangent);

            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
            rot *= Quaternion.Euler(linkRotationOffsetEuler);

            if ((i & 1) == 1)
                rot *= Quaternion.Euler(0f, 0f, 90f);

            Transform link = chainLinks[i];
            link.SetPositionAndRotation(pos, rot);
        }
    }

    private static float GetPathLength(List<Vector3> path)
    {
        float length = 0f;

        for (int i = 1; i < path.Count; i++)
            length += Vector3.Distance(path[i - 1], path[i]);

        return length;
    }

    private static void GetPointOnPath(List<Vector3> path, float distance, out Vector3 point, out Vector3 tangent)
    {
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = path[i - 1];
            Vector3 to = path[i];
            Vector3 segment = to - from;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.001f)
                continue;

            if (distance <= segmentLength)
            {
                tangent = segment / segmentLength;
                point = Vector3.Lerp(from, to, distance / segmentLength);
                return;
            }

            distance -= segmentLength;
        }

        point = path[^1];
        Vector3 fallback = path[^1] - path[^2];
        tangent = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
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
            if (phase == TitanClawWirePhase.Launching || phase == TitanClawWirePhase.HitBlocked)
                AddClawPathPoint(snapshot.ClawPosition);
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
        clawPath.Clear();

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
    Retracting = 3,
}

public struct TitanClawWireSnapshot
{
    public TitanClawWirePhase Phase;
    public float CurrentLength;
    public Vector3 ClawPosition;
    public Quaternion ClawRotation;
}
