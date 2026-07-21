using UnityEngine;

public readonly struct TitanStanceRootResult
{
    public TitanStanceRootResult(Vector3 rootPosition, bool feasible)
    {
        RootPosition = rootPosition;
        Feasible = feasible;
    }

    public Vector3 RootPosition { get; }
    public bool Feasible { get; }
}

public readonly struct TitanLegRootWorkspace
{
    public TitanLegRootWorkspace(Vector3 hipOffsetFromRoot, Vector3 footTarget, float maxReach)
    {
        HipOffsetFromRoot = hipOffsetFromRoot;
        FootTarget = footTarget;
        MaxReach = maxReach;
    }

    public Vector3 HipOffsetFromRoot { get; }
    public Vector3 FootTarget { get; }
    public float MaxReach { get; }
    public Vector3 Center => FootTarget - HipOffsetFromRoot;
}

public static class TitanStanceRootSolver
{
    private const float Epsilon = 0.00001f;

    public static TitanStanceRootResult SolveClosestSharedWorkspacePreserveHeight(
        Vector3 referenceRoot,
        Vector3 groundUp,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing)
    {
        Vector3 up = NormalizeUp(groundUp);
        float referenceHeight = Vector3.Dot(referenceRoot, up);
        if (!TryBuildDiskAtHeight(support, up, referenceHeight, out Vector3 supportCenter, out float supportRadius)
            || !TryBuildDiskAtHeight(swing, up, referenceHeight, out Vector3 swingCenter, out float swingRadius))
        {
            return new TitanStanceRootResult(referenceRoot, false);
        }

        Vector3 referencePlanar = Vector3.ProjectOnPlane(referenceRoot, up);
        if (TryFindClosestPointInDiskIntersection(referencePlanar, supportCenter, supportRadius, swingCenter, swingRadius, up, out Vector3 candidatePlanar))
        {
            return new TitanStanceRootResult(candidatePlanar + up * referenceHeight, true);
        }

        return new TitanStanceRootResult(referenceRoot, false);
    }

    public static TitanStanceRootResult SolveClosestSharedWorkspace(
        Vector3 referenceRoot,
        Vector3 groundUp,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing)
    {
        Vector3 up = NormalizeUp(groundUp);
        Vector3 centerA = support.Center;
        Vector3 centerB = swing.Center;
        float radiusA = Mathf.Max(0f, support.MaxReach);
        float radiusB = Mathf.Max(0f, swing.MaxReach);
        bool insideA = Vector3.Distance(referenceRoot, centerA) <= radiusA + Epsilon;
        bool insideB = Vector3.Distance(referenceRoot, centerB) <= radiusB + Epsilon;
        if (insideA && insideB)
        {
            return new TitanStanceRootResult(referenceRoot, true);
        }

        float distance = Vector3.Distance(centerA, centerB);
        if (distance <= Epsilon)
        {
            float radius = Mathf.Min(radiusA, radiusB);
            Vector3 projected = ProjectPointIntoBall(referenceRoot, centerA, radius);
            return new TitanStanceRootResult(projected, true);
        }

        if (distance > radiusA + radiusB + Epsilon)
        {
            return new TitanStanceRootResult(referenceRoot, false);
        }

        Vector3 best = referenceRoot;
        bool hasBest = false;
        TryKeepClosest(ProjectPointIntoBall(referenceRoot, centerA, radiusA), referenceRoot, up, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
        TryKeepClosest(ProjectPointIntoBall(referenceRoot, centerB, radiusB), referenceRoot, up, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);

        if (distance + Mathf.Min(radiusA, radiusB) <= Mathf.Max(radiusA, radiusB) + Epsilon)
        {
            Vector3 smallerCenter = radiusA <= radiusB ? centerA : centerB;
            float smallerRadius = Mathf.Min(radiusA, radiusB);
            TryKeepClosest(ProjectPointIntoBall(referenceRoot, smallerCenter, smallerRadius), referenceRoot, up, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
            return new TitanStanceRootResult(best, hasBest);
        }

        Vector3 delta = centerB - centerA;
        Vector3 axis = delta / distance;
        float x = (radiusA * radiusA - radiusB * radiusB + distance * distance) / (2f * distance);
        Vector3 circleCenter = centerA + axis * x;
        float circleRadius = Mathf.Sqrt(Mathf.Max(0f, radiusA * radiusA - x * x));
        Vector3 radial = Vector3.ProjectOnPlane(referenceRoot - circleCenter, axis);
        if (radial.sqrMagnitude <= Epsilon)
        {
            radial = Vector3.ProjectOnPlane(up, axis);
            if (radial.sqrMagnitude <= Epsilon)
            {
                radial = Vector3.ProjectOnPlane(Vector3.right, axis);
            }
        }

        Vector3 circleCandidate = radial.sqrMagnitude > Epsilon
            ? circleCenter + radial.normalized * circleRadius
            : circleCenter;
        TryKeepClosest(circleCandidate, referenceRoot, up, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);

        return new TitanStanceRootResult(best, hasBest);
    }

    public static TitanStanceRootResult SolveFixedPlanarHighest(
        Vector3 fixedRootPlanar,
        Vector3 currentRootPosition,
        Vector3 groundUp,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing)
    {
        Vector3 up = NormalizeUp(groundUp);
        if (!TryGetRootHeightIntervalAtFixedPlanar(fixedRootPlanar, up, support.HipOffsetFromRoot, support.FootTarget, support.MaxReach, out float supportMin, out float supportMax)
            || !TryGetRootHeightIntervalAtFixedPlanar(fixedRootPlanar, up, swing.HipOffsetFromRoot, swing.FootTarget, swing.MaxReach, out float swingMin, out float swingMax))
        {
            return new TitanStanceRootResult(currentRootPosition, false);
        }

        float minHeight = Mathf.Max(supportMin, swingMin);
        float maxHeight = Mathf.Min(supportMax, swingMax);
        if (minHeight > maxHeight + Epsilon)
        {
            return new TitanStanceRootResult(currentRootPosition, false);
        }

        return new TitanStanceRootResult(fixedRootPlanar + up * maxHeight, true);
    }

    public static bool HasFixedPlanarHeightOverlap(
        Vector3 fixedRootPlanar,
        Vector3 groundUp,
        TitanLegRootWorkspace support,
        TitanLegRootWorkspace swing)
    {
        Vector3 up = NormalizeUp(groundUp);
        if (!TryGetRootHeightIntervalAtFixedPlanar(fixedRootPlanar, up, support.HipOffsetFromRoot, support.FootTarget, support.MaxReach, out float supportMin, out float supportMax)
            || !TryGetRootHeightIntervalAtFixedPlanar(fixedRootPlanar, up, swing.HipOffsetFromRoot, swing.FootTarget, swing.MaxReach, out float swingMin, out float swingMax))
        {
            return false;
        }

        return Mathf.Max(supportMin, swingMin) <= Mathf.Min(supportMax, swingMax) + Epsilon;
    }

    public static bool TryGetRootHeightIntervalAtFixedPlanar(
        Vector3 fixedRootPlanar,
        Vector3 up,
        Vector3 hipOffsetFromRoot,
        Vector3 footTarget,
        float maxReach,
        out float minRootHeight,
        out float maxRootHeight)
    {
        Vector3 normalizedUp = NormalizeUp(up);
        Vector3 hipPlanar = fixedRootPlanar + Vector3.ProjectOnPlane(hipOffsetFromRoot, normalizedUp);
        Vector3 footPlanar = Vector3.ProjectOnPlane(footTarget, normalizedUp);
        float planarDistance = Vector3.Distance(hipPlanar, footPlanar);
        float verticalReachSquared = maxReach * maxReach - planarDistance * planarDistance;
        if (verticalReachSquared < -Epsilon)
        {
            minRootHeight = 0f;
            maxRootHeight = 0f;
            return false;
        }

        float verticalReach = Mathf.Sqrt(Mathf.Max(0f, verticalReachSquared));
        float intervalCenter = Vector3.Dot(footTarget, normalizedUp) - Vector3.Dot(hipOffsetFromRoot, normalizedUp);
        minRootHeight = intervalCenter - verticalReach;
        maxRootHeight = intervalCenter + verticalReach;
        return true;
    }

    public static TitanStanceRootResult SolveHighestDoubleSupport(
        Vector3 currentRootPosition,
        Vector3 groundUp,
        TitanLegRootWorkspace left,
        TitanLegRootWorkspace right)
    {
        Vector3 up = NormalizeUp(groundUp);
        Vector3 centerLeft = left.Center;
        Vector3 centerRight = right.Center;
        float radiusLeft = Mathf.Max(0f, left.MaxReach);
        float radiusRight = Mathf.Max(0f, right.MaxReach);
        float distance = Vector3.Distance(centerLeft, centerRight);

        if (distance <= Epsilon)
        {
            float radius = Mathf.Min(radiusLeft, radiusRight);
            return new TitanStanceRootResult(centerLeft + up * radius, true);
        }

        if (distance > radiusLeft + radiusRight + Epsilon)
        {
            return new TitanStanceRootResult(currentRootPosition, false);
        }

        if (distance + Mathf.Min(radiusLeft, radiusRight) <= Mathf.Max(radiusLeft, radiusRight) + Epsilon)
        {
            Vector3 center = radiusLeft <= radiusRight ? centerLeft : centerRight;
            float radius = Mathf.Min(radiusLeft, radiusRight);
            return new TitanStanceRootResult(center + up * radius, true);
        }

        Vector3 best = currentRootPosition;
        bool hasBest = false;
        TryKeepBest(centerLeft + up * radiusLeft, centerRight, radiusRight, up, ref best, ref hasBest);
        TryKeepBest(centerRight + up * radiusRight, centerLeft, radiusLeft, up, ref best, ref hasBest);

        Vector3 delta = centerRight - centerLeft;
        Vector3 axis = delta / distance;
        float x = (radiusLeft * radiusLeft - radiusRight * radiusRight + distance * distance) / (2f * distance);
        Vector3 circleCenter = centerLeft + axis * x;
        float circleRadius = Mathf.Sqrt(Mathf.Max(0f, radiusLeft * radiusLeft - x * x));
        Vector3 upwardInCirclePlane = Vector3.ProjectOnPlane(up, axis);
        Vector3 circleTop = upwardInCirclePlane.sqrMagnitude > Epsilon
            ? circleCenter + upwardInCirclePlane.normalized * circleRadius
            : circleCenter;
        TryKeepBest(circleTop, centerLeft, radiusLeft, up, ref best, ref hasBest, centerRight, radiusRight);

        return new TitanStanceRootResult(best, hasBest);
    }

    private static bool TryBuildDiskAtHeight(
        TitanLegRootWorkspace workspace,
        Vector3 up,
        float referenceHeight,
        out Vector3 diskCenter,
        out float diskRadius)
    {
        float verticalDelta = referenceHeight - Vector3.Dot(workspace.Center, up);
        float radius = Mathf.Max(0f, workspace.MaxReach);
        float planarRadiusSquared = radius * radius - verticalDelta * verticalDelta;
        if (planarRadiusSquared < -Epsilon)
        {
            diskCenter = default;
            diskRadius = 0f;
            return false;
        }

        diskCenter = Vector3.ProjectOnPlane(workspace.Center, up);
        diskRadius = Mathf.Sqrt(Mathf.Max(0f, planarRadiusSquared));
        return true;
    }

    private static bool TryFindClosestPointInDiskIntersection(
        Vector3 reference,
        Vector3 centerA,
        float radiusA,
        Vector3 centerB,
        float radiusB,
        Vector3 up,
        out Vector3 best)
    {
        best = reference;
        bool hasBest = false;
        TryKeepClosestPlanar(reference, reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
        TryKeepClosestPlanar(ProjectPointIntoDisk(reference, centerA, radiusA), reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
        TryKeepClosestPlanar(ProjectPointIntoDisk(reference, centerB, radiusB), reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);

        Vector3 delta = centerB - centerA;
        float distance = delta.magnitude;
        if (distance <= Epsilon)
        {
            Vector3 center = radiusA <= radiusB ? centerA : centerB;
            float radius = Mathf.Min(radiusA, radiusB);
            TryKeepClosestPlanar(ProjectPointIntoDisk(reference, center, radius), reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
            return hasBest;
        }

        if (distance + Mathf.Min(radiusA, radiusB) <= Mathf.Max(radiusA, radiusB) + Epsilon)
        {
            Vector3 center = radiusA <= radiusB ? centerA : centerB;
            float radius = Mathf.Min(radiusA, radiusB);
            TryKeepClosestPlanar(ProjectPointIntoDisk(reference, center, radius), reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
            return hasBest;
        }

        if (distance <= radiusA + radiusB + Epsilon)
        {
            Vector3 axis = delta / distance;
            float x = (radiusA * radiusA - radiusB * radiusB + distance * distance) / (2f * distance);
            float hSquared = radiusA * radiusA - x * x;
            if (hSquared >= -Epsilon)
            {
                Vector3 basePoint = centerA + axis * x;
                Vector3 perpendicular = Vector3.Cross(up, axis);
                if (perpendicular.sqrMagnitude > Epsilon)
                {
                    float h = Mathf.Sqrt(Mathf.Max(0f, hSquared));
                    perpendicular.Normalize();
                    TryKeepClosestPlanar(basePoint + perpendicular * h, reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
                    TryKeepClosestPlanar(basePoint - perpendicular * h, reference, centerA, radiusA, centerB, radiusB, ref best, ref hasBest);
                }
            }
        }

        return hasBest;
    }

    private static Vector3 ProjectPointIntoBall(Vector3 point, Vector3 center, float radius)
    {
        Vector3 offset = point - center;
        float magnitude = offset.magnitude;
        if (magnitude <= radius + Epsilon || magnitude <= Epsilon)
        {
            return point;
        }

        return center + offset / magnitude * radius;
    }

    private static Vector3 ProjectPointIntoDisk(Vector3 point, Vector3 center, float radius)
    {
        Vector3 offset = point - center;
        float magnitude = offset.magnitude;
        if (magnitude <= radius + Epsilon || magnitude <= Epsilon)
        {
            return point;
        }

        return center + offset / magnitude * radius;
    }

    private static void TryKeepClosest(
        Vector3 candidate,
        Vector3 reference,
        Vector3 up,
        Vector3 centerA,
        float radiusA,
        Vector3 centerB,
        float radiusB,
        ref Vector3 best,
        ref bool hasBest)
    {
        if (Vector3.Distance(candidate, centerA) > radiusA + Epsilon || Vector3.Distance(candidate, centerB) > radiusB + Epsilon)
        {
            return;
        }

        if (!hasBest || IsCloser(candidate, best, reference, up))
        {
            best = candidate;
            hasBest = true;
        }
    }

    private static void TryKeepClosestPlanar(
        Vector3 candidate,
        Vector3 reference,
        Vector3 centerA,
        float radiusA,
        Vector3 centerB,
        float radiusB,
        ref Vector3 best,
        ref bool hasBest)
    {
        if (Vector3.Distance(candidate, centerA) > radiusA + Epsilon || Vector3.Distance(candidate, centerB) > radiusB + Epsilon)
        {
            return;
        }

        if (!hasBest || (candidate - reference).sqrMagnitude < (best - reference).sqrMagnitude - Epsilon)
        {
            best = candidate;
            hasBest = true;
        }
    }

    private static bool IsCloser(Vector3 candidate, Vector3 best, Vector3 reference, Vector3 up)
    {
        float candidateDistance = (candidate - reference).sqrMagnitude;
        float bestDistance = (best - reference).sqrMagnitude;
        if (candidateDistance < bestDistance - Epsilon)
        {
            return true;
        }

        if (candidateDistance > bestDistance + Epsilon)
        {
            return false;
        }

        return Vector3.Dot(candidate, up) > Vector3.Dot(best, up);
    }

    private static void TryKeepBest(Vector3 candidate, Vector3 center, float radius, Vector3 up, ref Vector3 best, ref bool hasBest)
    {
        TryKeepBest(candidate, center, radius, up, ref best, ref hasBest, center, radius);
    }

    private static void TryKeepBest(Vector3 candidate, Vector3 centerA, float radiusA, Vector3 up, ref Vector3 best, ref bool hasBest, Vector3 centerB, float radiusB)
    {
        if (Vector3.Distance(candidate, centerA) > radiusA + Epsilon || Vector3.Distance(candidate, centerB) > radiusB + Epsilon)
        {
            return;
        }

        if (!hasBest || Vector3.Dot(candidate, up) > Vector3.Dot(best, up))
        {
            best = candidate;
            hasBest = true;
        }
    }

    private static Vector3 NormalizeUp(Vector3 up)
    {
        return up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
    }
}
