#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class TitanStanceRootSolverTests
{
    [Test]
    public void ClosestSharedWorkspace_ReferenceInsideBoth_ReturnsReference()
    {
        Vector3 reference = new Vector3(0.5f, 0.25f, 0f);
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 2f);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, Vector3.right, 2f);

        TitanStanceRootResult result = TitanStanceRootSolver.SolveClosestSharedWorkspace(reference, Vector3.up, support, swing);

        Assert.That(result.Feasible, Is.True);
        Assert.That(Vector3.Distance(result.RootPosition, reference), Is.LessThanOrEqualTo(0.00001f));
    }

    [Test]
    public void PreserveHeight_UsesPlanarMovementInsteadOfLowering()
    {
        Vector3 reference = Vector3.up;
        float reach = Mathf.Sqrt(3.25f);
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, reach);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, new Vector3(2.5f, 0f, 0f), reach);

        TitanStanceRootResult result = TitanStanceRootSolver.SolveClosestSharedWorkspacePreserveHeight(reference, Vector3.up, support, swing);

        Assert.That(result.Feasible, Is.True);
        Assert.That(result.RootPosition.y, Is.EqualTo(reference.y).Within(0.0001f));
        Assert.That(result.RootPosition.x, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void PreserveHeight_ReturnsClosestDiskIntersectionPoint()
    {
        Vector3 reference = new Vector3(0.5f, 1f, 2f);
        float reach = Mathf.Sqrt(2f);
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, reach);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, Vector3.right, reach);

        TitanStanceRootResult result = TitanStanceRootSolver.SolveClosestSharedWorkspacePreserveHeight(reference, Vector3.up, support, swing);

        Assert.That(result.Feasible, Is.True);
        Assert.That(result.RootPosition.x, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(result.RootPosition.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.RootPosition.z, Is.EqualTo(Mathf.Sqrt(0.75f)).Within(0.0001f));
    }

    [Test]
    public void ClosestSharedWorkspace_FallsBackTo3DOnlyWhenRequired()
    {
        Vector3 reference = Vector3.up * 2f;
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 1f);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, Vector3.right, 1f);

        TitanStanceRootResult preserveHeight = TitanStanceRootSolver.SolveClosestSharedWorkspacePreserveHeight(reference, Vector3.up, support, swing);
        TitanStanceRootResult result = TitanStanceRootSolver.SolveClosestSharedWorkspace(reference, Vector3.up, support, swing);

        Assert.That(preserveHeight.Feasible, Is.False);
        Assert.That(result.Feasible, Is.True);
        Assert.That(result.RootPosition.y, Is.EqualTo(Mathf.Sqrt(0.75f)).Within(0.0001f));
    }

    [Test]
    public void ClosestSharedWorkspace_DisjointBalls_ReturnsInfeasible()
    {
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 1f);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, Vector3.right * 3f, 1f);

        TitanStanceRootResult result = TitanStanceRootSolver.SolveClosestSharedWorkspace(Vector3.zero, Vector3.up, support, swing);

        Assert.That(result.Feasible, Is.False);
    }

    [Test]
    public void ClosestSharedWorkspace_PositiveAndNegativeForwardAreMirrored()
    {
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 2f);
        TitanLegRootWorkspace forward = new TitanLegRootWorkspace(Vector3.zero, Vector3.forward * 3f, 2f);
        TitanLegRootWorkspace backward = new TitanLegRootWorkspace(Vector3.zero, Vector3.back * 3f, 2f);

        TitanStanceRootResult positive = TitanStanceRootSolver.SolveClosestSharedWorkspace(Vector3.zero, Vector3.up, support, forward);
        TitanStanceRootResult negative = TitanStanceRootSolver.SolveClosestSharedWorkspace(Vector3.zero, Vector3.up, support, backward);

        Assert.That(positive.Feasible, Is.True);
        Assert.That(negative.Feasible, Is.True);
        Assert.That(positive.RootPosition.z, Is.EqualTo(-negative.RootPosition.z).Within(0.0001f));
        Assert.That(positive.RootPosition.x, Is.EqualTo(negative.RootPosition.x).Within(0.0001f));
    }

    [Test]
    public void ClosestSharedWorkspace_DoesNotPreferHighestPointOverCloserPoint()
    {
        Vector3 reference = new Vector3(0.5f, 0f, -2f);
        TitanLegRootWorkspace support = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 1f);
        TitanLegRootWorkspace swing = new TitanLegRootWorkspace(Vector3.zero, Vector3.right, 1f);

        TitanStanceRootResult closest = TitanStanceRootSolver.SolveClosestSharedWorkspace(reference, Vector3.up, support, swing);
        TitanStanceRootResult highest = TitanStanceRootSolver.SolveHighestDoubleSupport(reference, Vector3.up, support, swing);

        Assert.That(closest.Feasible, Is.True);
        Assert.That(closest.RootPosition.z, Is.LessThan(-0.8f));
        Assert.That(closest.RootPosition.y, Is.LessThan(0.1f));
        Assert.That(highest.RootPosition.y, Is.GreaterThan(0.8f));
    }

    [Test]
    public void HighestDoubleSupport_StillReturnsHighestFeasiblePoint()
    {
        Vector3 up = Vector3.up;
        TitanLegRootWorkspace left = new TitanLegRootWorkspace(Vector3.zero, Vector3.zero, 1f);
        TitanLegRootWorkspace right = new TitanLegRootWorkspace(Vector3.zero, new Vector3(1f, 0f, 0f), 1f);

        TitanStanceRootResult result = TitanStanceRootSolver.SolveHighestDoubleSupport(Vector3.zero, up, left, right);

        Assert.That(result.Feasible, Is.True);
        Assert.That(result.RootPosition.y, Is.EqualTo(Mathf.Sqrt(0.75f)).Within(0.0001f));
        Assert.That(Vector3.Distance(result.RootPosition, left.Center), Is.LessThanOrEqualTo(left.MaxReach + 0.0001f));
        Assert.That(Vector3.Distance(result.RootPosition, right.Center), Is.LessThanOrEqualTo(right.MaxReach + 0.0001f));
    }
}
#endif
