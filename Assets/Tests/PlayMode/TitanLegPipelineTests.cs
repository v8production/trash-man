#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TitanLegPipelineTests
{
    [Test]
    public void Airborne_DoesNotCreateSupportAnchor()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural(withFloor: false);
        fixture.Runtime.TickLegSystem(default, default, 0.02f);

        Assert.That(fixture.Runtime.GroundingState, Is.EqualTo(TitanLegGroundingState.Airborne));
        Assert.That(fixture.Runtime.SupportAnchorValid, Is.False);
        Assert.That(fixture.Runtime.LeftSolveCountThisFixedFrame, Is.EqualTo(0));
        Assert.That(fixture.Runtime.RightSolveCountThisFixedFrame, Is.EqualTo(0));
    }

    [Test]
    public void ElevatedFloor_AnchorUsesPhysicsHitPoint()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural(floorY: 0.37f);
        TickUntilGrounded(fixture.Runtime);

        Assert.That(fixture.Runtime.SupportAnchorValid, Is.True);
        Assert.That(fixture.Runtime.SupportAnchorWorld.y, Is.EqualTo(0.374f).Within(0.005f));
    }

    [Test]
    public void SignedSoleGap_DistinguishesHoverFromPenetration()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        FootAttachmentController attachment = fixture.Runtime.LeftFootAttachment;
        Vector3 up = TitanGroundFrame.Up;
        Vector3 planePoint = Vector3.zero;

        float soleDepth = attachment.GetSoleDepthBelowPivot(up);
        fixture.Runtime.LeftFoot.position = up * (soleDepth + 0.005f);
        Physics.SyncTransforms();
        attachment.RebuildCachedSoleGeometry();
        float hoverGap = attachment.GetMinimumSignedSoleGap(planePoint, up);

        fixture.Runtime.LeftFoot.position = up * (soleDepth - 0.005f);
        Physics.SyncTransforms();
        attachment.RebuildCachedSoleGeometry();
        float penetrationGap = attachment.GetMinimumSignedSoleGap(planePoint, up);

        Assert.That(hoverGap, Is.GreaterThan(0f));
        Assert.That(penetrationGap, Is.LessThan(0f));
        Assert.That(Mathf.Abs(hoverGap), Is.EqualTo(Mathf.Abs(penetrationGap)).Within(0.0005f));
    }

    [Test]
    public void CenterProbeClear_ButToeOrHeelPenetrates_IsRejected()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        FootAttachmentController attachment = fixture.Runtime.LeftFootAttachment;
        fixture.Runtime.LeftFoot.position = new Vector3(0f, attachment.GetSoleDepthBelowPivot(TitanGroundFrame.Up) - 0.002f, 0f);
        Physics.SyncTransforms();
        attachment.RebuildCachedSoleGeometry();

        Assert.That(attachment.BottomProbe.position.y, Is.GreaterThan(0f));
        Assert.That(attachment.GetMinimumSignedSoleGap(Vector3.zero, TitanGroundFrame.Up), Is.LessThan(0f));
    }

    [Test]
    public void SphereCastStartsOverlapping_RaycastFallbackFindsGround()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        FootAttachmentController attachment = fixture.Runtime.LeftFootAttachment;
        fixture.Runtime.LeftFoot.position = new Vector3(0f, 0.02f, 0f);
        Physics.SyncTransforms();

        bool found = attachment.TryGetGroundContactRobust(
            TitanGroundFrame.Up,
            true,
            Vector3.zero,
            fixture.FloorCollider,
            out FootGroundContact contact);

        Assert.That(found, Is.True);
        Assert.That(contact.Collider, Is.EqualTo(fixture.FloorCollider));
        Assert.That(contact.Point.y, Is.EqualTo(0f).Within(0.002f));
    }

    [Test]
    public void ActualTransformPosition_DrivesReachedState()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        runtime.LeftFoot.position += Vector3.up * 0.05f;
        Physics.SyncTransforms();
        runtime.TickLegSystem(default, default, 0.02f);

        Assert.That(runtime.LeftLegState.ActualFootPosition, Is.EqualTo(runtime.LeftFoot.position));
        Assert.That(runtime.LeftLegState.LastSolveReached, Is.False);
    }

    [Test]
    public void ActualTitanPrefab_RepeatedAlternatingSteps_NeverPenetrateGround()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Titan");
        Assert.That(prefab, Is.Not.Null);
        GameObject titan = Object.Instantiate(prefab);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
        try
        {
            TitanRigRuntime runtime = titan.GetComponent<TitanRigRuntime>();
            Assert.That(runtime, Is.Not.Null);
            Physics.SyncTransforms();
            TickUntilGrounded(runtime);
            Vector2[] deltas =
            {
                new Vector2(4f, 6f),
                new Vector2(-4f, -5f),
                new Vector2(6f, 0f),
                new Vector2(-5f, 4f),
            };

            for (int i = 0; i < 12; i++)
            {
                RunStep(runtime, i % 2 == 0, deltas[i % deltas.Length]);
                AssertPlantedFootInvariant(runtime, true, floor.GetComponent<Collider>());
                AssertPlantedFootInvariant(runtime, false, floor.GetComponent<Collider>());
            }
        }
        finally
        {
            Object.DestroyImmediate(titan);
            Object.DestroyImmediate(floor);
            Physics.SyncTransforms();
        }
    }

    [Test]
    public void SupportFootTarget_RemainsAnchored()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(5f, 0f) }, 0.02f);
        Vector3 supportTarget = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftLegState.DesiredGroundTarget : runtime.RightLegState.DesiredGroundTarget;

        Assert.That(Vector3.Distance(supportTarget, runtime.SupportAnchorWorld), Is.EqualTo(0f).Within(0.00001f));
    }

    [Test]
    public void LiftOnly_DoesNotDropRoot()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 up = TitanGroundFrame.Up;
        Vector3 rootBefore = runtime.MovementRoot.position;
        Vector3 supportAnchor = runtime.SupportAnchorWorld;
        float leftKnee = runtime.LeftLegState.SolvedAngles.KneeRoll;
        float rightKnee = runtime.RightLegState.SolvedAngles.KneeRoll;

        for (int i = 0; i < 45; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        }

        Assert.That(runtime.IsStepActive, Is.True);
        Assert.That(Mathf.Abs(Vector3.Dot(runtime.MovementRoot.position - rootBefore, up)), Is.LessThanOrEqualTo(0.01f));
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(rootBefore, up), Vector3.ProjectOnPlane(runtime.MovementRoot.position, up)), Is.LessThanOrEqualTo(0.01f));
        Assert.That(Vector3.Distance(supportAnchor, runtime.SupportAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
        TitanLegControlState support = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftLegState : runtime.RightLegState;
        Transform supportFoot = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftFoot : runtime.RightFoot;
        Assert.That(Vector3.Distance(supportFoot.position, support.PlantAnchorWorld), Is.LessThanOrEqualTo(0.04f));
        Assert.That(runtime.LeftLegState.SolvedAngles.KneeRoll - leftKnee, Is.LessThan(25f));
        Assert.That(runtime.RightLegState.SolvedAngles.KneeRoll - rightKnee, Is.LessThan(25f));
    }

    [Test]
    public void LiftStart_ZeroMouse_UsesConstantTimeFastPath()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 startTarget = runtime.RightLegState.DesiredGroundTarget;

        for (int i = 0; i < 60; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
            if (runtime.RightLegState.FootLift > 0.011f)
            {
                Assert.That(Vector3.Distance(startTarget, runtime.RightLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.00001f));
                Assert.That(runtime.RootGeometrySolveCountThisFixedFrame, Is.LessThanOrEqualTo(1));
                Assert.That(runtime.ExhaustiveRootFallbackCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.FullPreviewLegSolveCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.RecoverySeedAttemptCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.PreviewTransformWriteCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.BoneTransformWriteCountThisFixedFrame, Is.EqualTo(4));
                Assert.That(runtime.TrackingIterationCountThisFixedFrame, Is.LessThanOrEqualTo(6));
                Assert.That(runtime.LeftSolveCountThisFixedFrame, Is.EqualTo(1));
                Assert.That(runtime.RightSolveCountThisFixedFrame, Is.EqualTo(1));
                return;
            }
        }

        Assert.Fail("Foot lift never crossed horizontal unlock height.");
    }

    [Test]
    public void HeldLift_ZeroMouse_DoesNotRevalidateGroundTarget()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 60; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
            if (runtime.RightLegState.FootLift > 0.011f)
            {
                Assert.That(runtime.ExhaustiveRootFallbackCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.FullPreviewLegSolveCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.RecoverySeedAttemptCountThisFixedFrame, Is.EqualTo(0));
                Assert.That(runtime.PreviewTransformWriteCountThisFixedFrame, Is.EqualTo(0));
            }
        }
    }

    [Test]
    public void ReachableSwingMotion_PreservesReferenceRoot()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 up = TitanGroundFrame.Up;
        Vector3 rootBefore = runtime.MovementRoot.position;

        for (int i = 0; i < 30; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0.5f, 0.25f) }, 0.02f);
        }

        Assert.That(runtime.IsStepActive, Is.True);
        Assert.That(Vector3.Distance(runtime.MovementRoot.position, rootBefore), Is.LessThanOrEqualTo(0.01f));
        Assert.That(Mathf.Abs(Vector3.Dot(runtime.MovementRoot.position - rootBefore, up)), Is.LessThanOrEqualTo(0.01f));
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False);
    }

    [Test]
    public void BackwardSwingInput_MovesFootBackward()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 supportAnchor = runtime.SupportAnchorWorld;
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        Vector3 startTarget = runtime.RightLegState.DesiredGroundTarget;

        for (int i = 0; i < 16; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, -5f) }, 0.02f);
        }

        float backwardDistance = Vector3.Dot(runtime.RightLegState.DesiredGroundTarget - startTarget, TitanGroundFrame.WorldForward);
        Assert.That(backwardDistance, Is.LessThan(-0.1f));
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False, DescribeLegState(runtime, false));
        Assert.That(Vector3.Distance(supportAnchor, runtime.SupportAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(runtime.ExhaustiveRootFallbackCountThisFixedFrame, Is.LessThanOrEqualTo(1));
        AssertJointLimits(runtime);
    }

    [Test]
    public void HorizontalSwingInput_UsesPelvisFacingDirection()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        runtime.MovementRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
        Physics.SyncTransforms();
        TickUntilGrounded(runtime);
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        Vector3 startTarget = runtime.RightLegState.DesiredGroundTarget;

        for (int i = 0; i < 16; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, 5f) }, 0.02f);
        }

        Vector3 moved = runtime.RightLegState.DesiredGroundTarget - startTarget;
        Assert.That(Vector3.Dot(moved, Vector3.right), Is.GreaterThan(0.1f));
        Assert.That(Mathf.Abs(Vector3.Dot(moved, TitanGroundFrame.WorldForward)), Is.LessThan(0.05f));
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False, DescribeLegState(runtime, false));
    }

    [Test]
    public void InteriorHorizontalMotion_DoesNotUseExhaustiveFallback()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 12; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, -1f) }, 0.02f);
        }

        Assert.That(Vector3.Dot(runtime.RightLegState.DesiredGroundTarget - runtime.RightLegState.PlantAnchorWorld, TitanGroundFrame.WorldForward), Is.LessThan(-0.02f));
        Assert.That(runtime.ExhaustiveRootFallbackCountThisFixedFrame, Is.EqualTo(0));
        Assert.That(runtime.FullPreviewLegSolveCountThisFixedFrame, Is.EqualTo(0));
    }

    [Test]
    public void ExtremeHorizontalMotion_HasBoundedFallback()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 10; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        }

        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(200f, 200f) }, 0.02f);

        Assert.That(runtime.GeometryBinarySearchIterationCountThisFixedFrame, Is.GreaterThan(0));
        Assert.That(runtime.ExhaustiveRootFallbackCountThisFixedFrame, Is.LessThanOrEqualTo(1));
        Assert.That(runtime.FullPreviewLegSolveCountThisFixedFrame, Is.LessThanOrEqualTo(80));
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False);
    }

    [Test]
    public void BackwardStep_TouchesDown()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 28; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, -3f) }, 0.02f);
        }

        TickUntilDoubleSupport(runtime, 180);

        Assert.That(runtime.IsDoubleSupport, Is.True, DescribeLegState(runtime, false));
        Assert.That(runtime.IsStepActive, Is.False);
        Assert.That(runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(runtime.RightLegState.FootLiftTarget, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Distance(runtime.RightLegState.ActualFootPosition, runtime.RightLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.04f));
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False);
    }

    [Test]
    public void RootReturnsToReferenceWhenSwingReturns()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 rootReference = runtime.MovementRoot.position;

        for (int i = 0; i < 45; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, 8f) }, 0.02f);
        }

        float compensatedDistance = Vector3.Distance(runtime.MovementRoot.position, rootReference);
        for (int i = 0; i < 60; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, -8f) }, 0.02f);
        }

        Assert.That(Vector3.Distance(runtime.MovementRoot.position, rootReference), Is.LessThan(compensatedDistance));
        Assert.That(Vector3.Distance(runtime.SupportAnchorWorld, runtime.LeftLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
    }

    [Test]
    public void Touchdown_AlignsPelvisToTorso()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 leftAnchor = runtime.LeftLegState.PlantAnchorWorld;
        Vector3 rightAnchor = runtime.RightLegState.PlantAnchorWorld;
        runtime.SetWaistYaw(30f);
        runtime.ApplySpine(30f, 0f, 0f);
        Vector3 torsoForwardBefore = Vector3.ProjectOnPlane(runtime.Spine.forward, TitanGroundFrame.Up).normalized;
        Quaternion rootBefore = runtime.MovementRoot.rotation;

        RunStep(runtime, false, new Vector2(0f, 2f));
        runtime.ApplyMovementRootBaseRotation();
        runtime.TickLegSystem(default, default, 0.02f);

        float rootYawDelta = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(rootBefore * Vector3.forward, TitanGroundFrame.Up),
            Vector3.ProjectOnPlane(runtime.MovementRoot.forward, TitanGroundFrame.Up),
            TitanGroundFrame.Up);
        Vector3 torsoForwardAfter = Vector3.ProjectOnPlane(runtime.Spine.forward, TitanGroundFrame.Up).normalized;

        Assert.That(rootYawDelta, Is.EqualTo(30f).Within(2f));
        Assert.That(runtime.WaistYaw, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Angle(torsoForwardBefore, torsoForwardAfter), Is.LessThanOrEqualTo(1f));
        Assert.That(Vector3.Distance(leftAnchor, runtime.LeftLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Vector3.Distance(rightAnchor, runtime.RightLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Vector3.Distance(runtime.LeftLegState.ActualFootPosition, runtime.LeftLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.04f));
        Assert.That(Vector3.Distance(runtime.RightLegState.ActualFootPosition, runtime.RightLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.04f));
    }

    [Test]
    public void SupportFoot_RemainsAtWorldAnchor_WhileRootMoves()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 supportAnchor = runtime.SupportAnchorWorld;

        for (int i = 0; i < 24; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, 5f) }, 0.02f);
        }

        for (int i = 0; i < 30; i++)
        {
            runtime.TickLegSystem(default, default, 0.02f);
        }

        TitanLegControlState supportState = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftLegState : runtime.RightLegState;
        Transform supportFoot = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftFoot : runtime.RightFoot;
        Assert.That(Vector3.Distance(supportAnchor, runtime.SupportAnchorWorld), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Vector3.Distance(supportState.DesiredGroundTarget, supportAnchor), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Vector3.Distance(supportFoot.position, supportAnchor), Is.LessThanOrEqualTo(0.05f));
    }

    [Test]
    public void SwingFoot_ReachesGround_AfterForwardStep()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 24; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(0f, 5f) }, 0.02f);
        }

        for (int i = 0; i < 80; i++)
        {
            Physics.SyncTransforms();
            runtime.TickLegSystem(default, default, 0.02f);
        }

        Assert.That(runtime.IsDoubleSupport, Is.True, DescribeLegState(runtime, false));
        Assert.That(runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.001f));
        Assert.That(Vector3.Distance(runtime.RightFoot.position, runtime.RightLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.05f));
    }

    [Test]
    public void LargeSwingMove_TouchesDownAfterScrollStops()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 48; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(6f, 6f) }, 0.02f);
        }

        TickUntilDoubleSupport(runtime, 200);

        Assert.That(runtime.IsDoubleSupport, Is.True, DescribeLegState(runtime, false));
        Assert.That(runtime.IsStepActive, Is.False);
        Assert.That(runtime.RightLegState.FootLiftTarget, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(Vector3.Distance(runtime.RightLegState.ActualFootPosition, runtime.RightLegState.PlantAnchorWorld), Is.LessThanOrEqualTo(0.035f));
    }

    [Test]
    public void RepeatedAlternatingSteps_DoNotLeaveFootHovering()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int step = 0; step < 8; step++)
        {
            bool moveLeft = step % 2 == 1;
            RunStep(runtime, moveLeft, new Vector2(moveLeft ? -4f : 4f, 3f));
            Assert.That(runtime.IsDoubleSupport, Is.True, DescribeLegState(runtime, moveLeft));
            Assert.That(runtime.IsStepActive, Is.False);
            Assert.That(runtime.LeftLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
            AssertPlantedFootInvariant(runtime, true, fixture.FloorCollider);
            AssertPlantedFootInvariant(runtime, false, fixture.FloorCollider);
        }
    }

    [Test]
    public void AlternatingSteps_DoNotRatchetRootHeightDown()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int step = 0; step < 8; step++)
        {
            bool moveLeft = step % 2 == 1;
            RunStep(runtime, moveLeft, new Vector2(moveLeft ? -3f : 3f, 2f));
            for (int i = 0; i < 80; i++)
            {
                Physics.SyncTransforms();
                runtime.TickLegSystem(default, default, 0.02f);
            }

            Assert.That(runtime.MovementRoot.position.y, Is.EqualTo(ExpectedHighestRootHeight(runtime)).Within(0.02f));
        }
    }

    [Test]
    public void LoweredRoot_RecoversToHighestFeasibleStance()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        float settledHeight = runtime.MovementRoot.position.y;
        runtime.MovementRoot.position += Vector3.down * 0.25f;

        for (int i = 0; i < 60; i++)
        {
            Physics.SyncTransforms();
            runtime.TickLegSystem(default, default, 0.02f);
        }

        Assert.That(runtime.MovementRoot.position.y, Is.EqualTo(settledHeight).Within(0.02f));
        Assert.That(runtime.LeftLegState.SolvedAngles.KneeRoll, Is.EqualTo(runtime.LeftLegSolverSettings.KneeRoll.MinAngle).Within(3f));
        Assert.That(runtime.RightLegState.SolvedAngles.KneeRoll, Is.EqualTo(runtime.RightLegSolverSettings.KneeRoll.MinAngle).Within(3f));
    }

    [Test]
    public void SwingTarget_IsClampedBeforeBecomingUnreachable()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int i = 0; i < 10; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        }

        runtime.TickLegSystem(default, new TitanLegInputCommand { HorizontalDelta = new Vector2(1000f, 1000f) }, 0.02f);
        Assert.That(runtime.RightLegState.TargetWasClamped, Is.False);

        TickUntilDoubleSupport(runtime, 200);
        Assert.That(runtime.IsDoubleSupport, Is.True, DescribeLegState(runtime, false));
    }

    [Test]
    public void SupportAnchor_RemainsBitwiseStableDuringStep()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        Vector3 supportAnchor = runtime.SupportAnchorWorld;

        for (int i = 0; i < 80; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(5f, -3f) }, 0.02f);
            TitanLegControlState support = runtime.SupportFoot == TitanSupportFoot.Left ? runtime.LeftLegState : runtime.RightLegState;
            Assert.That(Vector3.Distance(support.PlantAnchorWorld, supportAnchor), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(support.DesiredGroundTarget, supportAnchor), Is.LessThanOrEqualTo(0.0001f));
        }
    }

    [Test]
    public void JointLimits_RemainValidDuringRepeatedSteps()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        for (int step = 0; step < 6; step++)
        {
            bool moveLeft = step % 2 == 1;
            for (int i = 0; i < 24; i++)
            {
                runtime.TickLegSystem(
                    moveLeft ? new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(-4f, 2f) } : default,
                    moveLeft ? default : new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(4f, 2f) },
                    0.02f);
                AssertJointLimits(runtime);
            }

            for (int i = 0; i < 160 && !runtime.IsDoubleSupport; i++)
            {
                runtime.TickLegSystem(default, default, 0.02f);
                AssertJointLimits(runtime);
            }
        }
    }

    [Test]
    public void BothFeetPlanted_CollisionCannotMoveRoot()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);

        Rigidbody body = runtime.MovementRigidbody;
        Assert.That(runtime.IsDoubleSupport, Is.True);
        Assert.That(body, Is.Not.Null);
        Assert.That(body.isKinematic, Is.True);
        Assert.That(body.useGravity, Is.False);
    }

    [Test]
    public void LegPipeline_RunsOncePerFixedUpdate()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        fixture.Runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);

        Assert.That(fixture.Runtime.LegPipelineTickCountThisFixedFrame, Is.EqualTo(1));
        Assert.That(fixture.Runtime.LeftSolveCountThisFixedFrame, Is.EqualTo(1));
        Assert.That(fixture.Runtime.RightSolveCountThisFixedFrame, Is.EqualTo(1));
    }

    [Test]
    public void InteriorMouseSequence_MapsExactlyToFootTarget()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);

        Vector2[] sequence =
        {
            new Vector2(0.1f, 0f),
            new Vector2(-0.05f, 0f),
            new Vector2(0f, 0.12f),
            new Vector2(0f, -0.04f),
            new Vector2(0.08f, 0.06f),
            new Vector2(-0.03f, -0.02f),
            new Vector2(0.15f, -0.05f),
            new Vector2(-0.07f, 0.11f),
        };
        float sensitivity = GetFootMoveSensitivity(runtime);

        for (int i = 0; i < 56; i++)
        {
            Vector2 mouse = sequence[i % sequence.Length];
            Vector3 previous = runtime.RightLegState.DesiredGroundTarget;
            Vector3 expectedDelta = ExpectedWorldDelta(mouse, sensitivity);

            TickRightSwing(runtime, mouse);

            Vector3 actualDelta = runtime.RightLegState.DesiredGroundTarget - previous;
            Assert.That(Vector3.Distance(actualDelta, expectedDelta), Is.LessThanOrEqualTo(0.0001f), $"tick={i}");
            Assert.That(runtime.FootInputAcceptanceRatioThisTick, Is.EqualTo(1f).Within(0.0001f), $"tick={i}");
            Assert.That(runtime.FootTargetWorkspaceClampedThisTick, Is.False, $"tick={i}");
        }
    }

    [Test]
    public void TinyMouseDeltas_NeverProduceAnInteriorDeadTick()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);
        float sensitivity = GetFootMoveSensitivity(runtime);
        Vector2 mouse = new Vector2(0.01f, -0.006f);

        for (int i = 0; i < 60; i++)
        {
            Vector3 previous = runtime.RightLegState.DesiredGroundTarget;
            Vector3 expectedDelta = ExpectedWorldDelta(mouse, sensitivity);

            TickRightSwing(runtime, mouse);

            Vector3 actualDelta = runtime.RightLegState.DesiredGroundTarget - previous;
            Assert.That(mouse.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(actualDelta.sqrMagnitude, Is.GreaterThan(0f), $"tick={i}");
            Assert.That(Vector3.Distance(actualDelta, expectedDelta), Is.LessThanOrEqualTo(0.0001f), $"tick={i}");
            Assert.That(runtime.FootTargetWorkspaceClampedThisTick, Is.False, $"tick={i}");
        }
    }

    [Test]
    public void GroundedRootMoveSpeed_DoesNotChangeFootTargetPath()
    {
        using RuntimeFixture slowFixture = RuntimeFixture.CreateProcedural();
        using RuntimeFixture fastFixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime slow = slowFixture.Runtime;
        TitanRigRuntime fast = fastFixture.Runtime;
        TestReflection.SetPrivateField(slow, "groundedRootMoveSpeed", 0.001f);
        TestReflection.SetPrivateField(fast, "groundedRootMoveSpeed", 1000f);
        TickUntilGrounded(slow);
        TickUntilGrounded(fast);
        BeginRightSwing(slow);
        BeginRightSwing(fast);
        Vector2[] sequence =
        {
            new Vector2(0.12f, 0f),
            new Vector2(0.05f, 0.07f),
            new Vector2(-0.04f, 0.09f),
            new Vector2(0.1f, -0.03f),
            new Vector2(-0.08f, -0.02f),
        };

        for (int i = 0; i < 50; i++)
        {
            Vector2 mouse = sequence[i % sequence.Length];
            TickRightSwing(slow, mouse);
            TickRightSwing(fast, mouse);

            Assert.That(Vector3.Distance(slow.RightLegState.DesiredGroundTarget, fast.RightLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.0001f), $"tick={i}");
        }
    }

    [Test]
    public void ActualFoot_ReachesUpdatedTargetInSameFixedTick()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);

        for (int i = 0; i < 20; i++)
        {
            TickRightSwing(runtime, new Vector2(0.2f, 0.05f));
            Vector3 expectedTarget = runtime.RightLegState.DesiredGroundTarget + TitanGroundFrame.Up * runtime.RightLegState.FootLift;
            Assert.That(Vector3.Distance(runtime.RightLegState.ActualFootPosition, expectedTarget), Is.LessThanOrEqualTo(runtime.RightLegSolverSettings.PositionTolerance + 0.0001f), $"tick={i}");
        }
    }

    [Test]
    public void EarlyLiftMouseInput_IsNotDiscarded()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Vector3 previous = runtime.RightLegState.DesiredGroundTarget;
        float sensitivity = GetFootMoveSensitivity(runtime);
        Vector2 mouse = new Vector2(0.2f, 0f);

        TickRightSwing(runtime, mouse);

        Assert.That(runtime.RightLegState.FootLiftTarget, Is.GreaterThan(0f));
        Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget - previous, ExpectedWorldDelta(mouse, sensitivity)), Is.LessThanOrEqualTo(0.0001f));
    }

    [Test]
    public void AggressiveInteriorDelta_DoesNotSwitchRootFallbackBranch()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);
        float sensitivity = GetFootMoveSensitivity(runtime);
        Vector2 mouse = new Vector2(2f, 1f);
        Vector3 previous = runtime.RightLegState.DesiredGroundTarget;

        TickRightSwing(runtime, mouse);

        Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget - previous, ExpectedWorldDelta(mouse, sensitivity)), Is.LessThanOrEqualTo(0.0001f));
Assert.That(runtime.RootFallbackUsedThisTick, Is.False);
        Vector3 accepted = runtime.RightLegState.DesiredGroundTarget;
        for (int i = 0; i < 20; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
            Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget, accepted), Is.LessThanOrEqualTo(0.0001f), $"tick={i}");
        }
    }

    [Test]
    public void AlternatingInput_HasNoResidualOrDelayedMovement()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);
        float sensitivity = GetFootMoveSensitivity(runtime);
        Vector3 start = runtime.RightLegState.DesiredGroundTarget;
        Vector3 cumulative = Vector3.zero;
        Vector2[] sequence =
        {
            new Vector2(0.15f, 0f),
            new Vector2(0.15f, 0f),
            new Vector2(-0.15f, 0f),
            new Vector2(-0.15f, 0f),
            new Vector2(0f, 0.12f),
            new Vector2(0f, -0.12f),
            new Vector2(0.08f, -0.05f),
            new Vector2(-0.08f, 0.05f),
        };

        for (int i = 0; i < sequence.Length; i++)
        {
            cumulative += ExpectedWorldDelta(sequence[i], sensitivity);
            TickRightSwing(runtime, sequence[i]);
            Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget, start + cumulative), Is.LessThanOrEqualTo(0.0001f), $"tick={i}");
        }

        Vector3 final = runtime.RightLegState.DesiredGroundTarget;
        for (int i = 0; i < 20; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
            Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget, final), Is.LessThanOrEqualTo(0.0001f), $"zero tick={i}");
        }
    }

    [Test]
    public void RequiredRoot_IsMinimumCorrectionFromCurrentRootWhenTargetRemainsReachable()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);
        Vector3 previousRoot = runtime.MovementRoot.position;

        TickRightSwing(runtime, new Vector2(0.1f, 0f));

        Assert.That(Vector3.Distance(runtime.MovementRoot.position, previousRoot), Is.LessThanOrEqualTo(0.0001f));
Assert.That(runtime.RequiredRootCorrectionThisTick.sqrMagnitude, Is.LessThanOrEqualTo(0.00000001f));
    }

    [Test]
    public void PhysicalBoundary_IsTheOnlyPartialAcceptanceCase()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        BeginRightSwing(runtime);
        Vector3 beforeOutward = runtime.RightLegState.DesiredGroundTarget;
        TickRightSwing(runtime, new Vector2(1000f, 1000f));

        Assert.That(runtime.FootTargetWorkspaceClampedThisTick, Is.True);
        Assert.That(runtime.FootInputAcceptanceRatioThisTick, Is.LessThan(1f));
        Vector3 boundary = runtime.RightLegState.DesiredGroundTarget;
        Assert.That(Vector3.Distance(boundary, beforeOutward), Is.GreaterThan(0f));

        TickRightSwing(runtime, new Vector2(-0.1f, -0.1f));

        Assert.That(runtime.FootInputAcceptanceRatioThisTick, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(runtime.FootTargetWorkspaceClampedThisTick, Is.False);
        Vector3 inward = runtime.RightLegState.DesiredGroundTarget;
        Assert.That(Vector3.Distance(inward, boundary), Is.GreaterThan(0f));

        for (int i = 0; i < 20; i++)
        {
            runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
            Assert.That(Vector3.Distance(runtime.RightLegState.DesiredGroundTarget, inward), Is.LessThanOrEqualTo(0.0001f), $"zero tick={i}");
        }
    }

    [Test]
    public void SoleAlwaysFacesOppositeGravity()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        runtime.TickLegSystem(new TitanLegInputCommand { LiftInput = 1f }, default, 0.02f);

        Assert.That(runtime.GetFootSoleAngleFromGround(true), Is.LessThanOrEqualTo(0.5f));
        Assert.That(runtime.GetFootSoleAngleFromGround(false), Is.LessThanOrEqualTo(0.5f));
    }

    [Test]
    public void LeftRightInputOrder_DoesNotChangeResult()
    {
        using RuntimeFixture fixtureA = RuntimeFixture.CreateProcedural();
        using RuntimeFixture fixtureB = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixtureA.Runtime);
        TickUntilGrounded(fixtureB.Runtime);
        TitanLegInputCommand left = new TitanLegInputCommand { LiftInput = 0.5f, HorizontalDelta = new Vector2(2f, 1f) };
        TitanLegInputCommand right = new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = new Vector2(3f, 0f) };

        fixtureA.Runtime.TickLegSystem(left, right, 0.02f);
        fixtureB.Runtime.TickLegSystem(left, right, 0.02f);

        Assert.That(fixtureA.Runtime.SupportFoot, Is.EqualTo(fixtureB.Runtime.SupportFoot));
        Assert.That(Vector3.Distance(fixtureA.Runtime.LeftLegState.DesiredGroundTarget, fixtureB.Runtime.LeftLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.00001f));
        Assert.That(Vector3.Distance(fixtureA.Runtime.RightLegState.DesiredGroundTarget, fixtureB.Runtime.RightLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.00001f));
    }

    [Test]
    public void OneFootContact_InitializesOtherFootToSupportPlane()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural(rightFootHeightOffset: 0.25f);
        TickUntilGrounded(fixture.Runtime);

        Assert.That(fixture.Runtime.GroundTargetsInitialized, Is.True);
        AssertProbeTargetHeight(fixture.Runtime, true, 0f, 0.03f);
        AssertProbeTargetHeight(fixture.Runtime, false, 0f, 0.03f);
        Assert.That(fixture.Runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void NoInput_GroundedStance_KeepsBothFeetOnGround()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        Vector3 leftTarget = fixture.Runtime.LeftLegState.DesiredGroundTarget;
        Vector3 rightTarget = fixture.Runtime.RightLegState.DesiredGroundTarget;
        float leftFootHeight = fixture.Runtime.LeftFoot.position.y;
        float rightFootHeight = fixture.Runtime.RightFoot.position.y;

        for (int i = 0; i < 30; i++)
        {
            fixture.Runtime.TickLegSystem(default, default, 0.02f);
        }

        Assert.That(Vector3.Distance(leftTarget, fixture.Runtime.LeftLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Vector3.Distance(rightTarget, fixture.Runtime.RightLegState.DesiredGroundTarget), Is.LessThanOrEqualTo(0.0001f));
        Assert.That(Mathf.Abs(fixture.Runtime.LeftFoot.position.y - leftFootHeight), Is.LessThanOrEqualTo(0.02f));
        Assert.That(Mathf.Abs(fixture.Runtime.RightFoot.position.y - rightFootHeight), Is.LessThanOrEqualTo(0.02f));
    }

    [Test]
    public void Landing_DoesNotReuseAirborneWorldTarget()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural(floorY: -1f);
        fixture.Runtime.MovementRoot.position = Vector3.up * 1.5f;
        fixture.Runtime.TickLegSystem(default, default, 0.02f);
        Vector3 airborneRightTarget = fixture.Runtime.RightLegState.DesiredGroundTarget;
        fixture.Runtime.MovementRoot.position = Vector3.zero;
        fixture.MoveFloor(0f);
        TickUntilGrounded(fixture.Runtime);

        Vector3 groundedRightTarget = fixture.Runtime.RightLegState.DesiredGroundTarget;
        Assert.That(Vector3.Distance(airborneRightTarget, groundedRightTarget), Is.GreaterThan(0.5f));
        Assert.That(airborneRightTarget.y - groundedRightTarget.y, Is.GreaterThan(0.5f));
    }

    [Test]
    public void Relanding_RebasesBothTargets()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        Vector3 firstLeftTarget = fixture.Runtime.LeftLegState.DesiredGroundTarget;
        fixture.Runtime.MovementRoot.position += Vector3.up * 1.2f;
        for (int i = 0; i < 8; i++)
        {
            fixture.Runtime.TickLegSystem(default, default, 0.02f);
        }

        fixture.MoveFloor(0.5f);
        fixture.Runtime.MovementRoot.position = Vector3.up * 0.5f;
        TickUntilGrounded(fixture.Runtime);
        Assert.That(Vector3.Distance(firstLeftTarget, fixture.Runtime.LeftLegState.DesiredGroundTarget), Is.GreaterThan(0.25f));
        AssertProbeTargetHeight(fixture.Runtime, true, 0.5f, 0.04f);
        AssertProbeTargetHeight(fixture.Runtime, false, 0.5f, 0.04f);
    }

    [Test]
    public void ActualTitanPrefab_SpawnAtY3_FallsBeforeGrounding()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Titan");
        Assert.That(prefab, Is.Not.Null);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            DisablePoseWriters(instance);

            TitanRigRuntime runtime = instance.GetComponent<TitanRigRuntime>();
            Assert.That(runtime, Is.Not.Null);
            Rigidbody body = runtime.MovementRigidbody;
            Assert.That(body, Is.Not.Null);
            PrepareDynamicSpawn(runtime, body, new Vector3(0f, 3f, 0f));
            float startY = runtime.MovementRoot.position.y;

            runtime.TickLegSystem(default, default, 0.02f);
            Assert.That(runtime.GroundingState, Is.EqualTo(TitanLegGroundingState.Airborne));
            Assert.That(runtime.SupportAnchorValid, Is.False);
            Assert.That(runtime.GroundTargetsInitialized, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);

            int landingTick = -1;
            int groundedTick = -1;
            for (int i = 0; i < 10; i++)
            {
                SimulateRuntimeFixedTick(runtime);
                if (landingTick < 0 && runtime.GroundingState == TitanLegGroundingState.Landing) landingTick = i + 1;
                if (groundedTick < 0 && runtime.GroundingState == TitanLegGroundingState.Grounded) groundedTick = i + 1;
            }

            Assert.That(startY - runtime.MovementRoot.position.y, Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));
            Assert.That(runtime.SupportAnchorValid, Is.False);
            Assert.That(runtime.GroundTargetsInitialized, Is.False);
            Assert.That(runtime.GroundedRootWriteCountWhileAirborne, Is.EqualTo(0));
            Assert.That(runtime.EmergencyGroundPoseRestoreCount, Is.EqualTo(0));

            for (int i = 10; i < 240 && !runtime.GroundTargetsInitialized; i++)
            {
                SimulateRuntimeFixedTick(runtime);
                if (landingTick < 0 && runtime.GroundingState == TitanLegGroundingState.Landing) landingTick = i + 1;
                if (groundedTick < 0 && runtime.GroundingState == TitanLegGroundingState.Grounded) groundedTick = i + 1;
            }

            Assert.That(landingTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(groundedTick, Is.GreaterThan(landingTick));
            Assert.That(runtime.SupportAnchorValid, Is.True);
            Assert.That(runtime.GroundTargetsInitialized, Is.True);
            Assert.That(runtime.GroundingState, Is.EqualTo(TitanLegGroundingState.Grounded));
            Assert.That(runtime.LeftLegState.IsPlanted, Is.True);
            Assert.That(runtime.RightLegState.IsPlanted, Is.True);
            Assert.That(runtime.LeftLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(runtime.RightLegState.FootLift, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(runtime.LeftLegState.FootLiftTarget, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(runtime.RightLegState.FootLiftTarget, Is.EqualTo(0f).Within(0.0001f));
            AssertPlantedFootInvariant(runtime, true, floor.GetComponent<Collider>());
            AssertPlantedFootInvariant(runtime, false, floor.GetComponent<Collider>());

            float minimumGap = float.PositiveInfinity;
            Vector2[] deltas =
            {
                new Vector2(4f, 6f),
                new Vector2(-4f, -5f),
                new Vector2(6f, 0f),
                new Vector2(-5f, 4f),
            };
            for (int i = 0; i < 12; i++)
            {
                RunStep(runtime, i % 2 == 0, deltas[i % deltas.Length]);
                minimumGap = Mathf.Min(minimumGap, runtime.LeftFootAttachment.GetMinimumSignedSoleGap(Vector3.zero, TitanGroundFrame.Up));
                minimumGap = Mathf.Min(minimumGap, runtime.RightFootAttachment.GetMinimumSignedSoleGap(Vector3.zero, TitanGroundFrame.Up));
                AssertPlantedFootInvariant(runtime, true, floor.GetComponent<Collider>());
                AssertPlantedFootInvariant(runtime, false, floor.GetComponent<Collider>());
            }

            Assert.That(minimumGap, Is.GreaterThanOrEqualTo(-0.0025f));
            Assert.That(runtime.EmergencyGroundPoseRestoreCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void ActualTitanPrefab_SoleDiagnosticsDoNotAddCollisionResponse()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Titan");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            TitanRigRuntime runtime = instance.GetComponent<TitanRigRuntime>();
            Assert.That(runtime, Is.Not.Null);
            Rigidbody body = runtime.MovementRigidbody;
            Assert.That(body, Is.Not.Null);

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                bool forbiddenAuthoritativeSole = collider.enabled
                    && !collider.isTrigger
                    && (collider.name == "LeftAuthoritativeSoleCollider" || collider.name == "RightAuthoritativeSoleCollider");
                Assert.That(forbiddenAuthoritativeSole, Is.False, collider.name);
            }

            Assert.That(runtime.LeftFootAttachment.SoleContactPointCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(runtime.RightFootAttachment.SoleContactPointCount, Is.GreaterThanOrEqualTo(4));
            AssertDiagnosticCollidersDoNotAffectMovementBody(runtime.LeftFootAttachment, body);
            AssertDiagnosticCollidersDoNotAffectMovementBody(runtime.RightFootAttachment, body);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void Airborne_DoesNotRestoreLastValidGroundedPose()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Rigidbody body = runtime.MovementRigidbody;
        Vector3 groundedRoot = runtime.MovementRoot.position;

        TestReflection.SetPrivateField(runtime, "groundingState", TitanLegGroundingState.Airborne);
        TestReflection.SetPrivateField(runtime, "supportAnchorValid", false);
        TestReflection.SetPrivateField(runtime, "groundTargetsInitialized", false);
        runtime.ApplyMovementRootPose(groundedRoot + Vector3.up * 2f, runtime.MovementRoot.rotation, zeroVelocities: true);
        body.isKinematic = false;
        body.useGravity = true;
        body.WakeUp();
        float startY = runtime.MovementRoot.position.y;

        for (int i = 0; i < 6; i++)
        {
            SimulateRuntimeFixedTick(runtime);
        }

        Assert.That(runtime.MovementRoot.position.y, Is.LessThan(startY));
        Assert.That(Vector3.Distance(runtime.MovementRoot.position, groundedRoot), Is.GreaterThan(0.5f));
        Assert.That(runtime.EmergencyGroundPoseRestoreCount, Is.EqualTo(0));
        Assert.That(runtime.GroundedRootWriteCountWhileAirborne, Is.EqualTo(0));
    }

    [Test]
    public void Landing_RemainsDynamicUntilAnchorsInitialize()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        Rigidbody body = runtime.MovementRigidbody;
        body.isKinematic = false;
        body.useGravity = true;
        TestReflection.SetPrivateField(runtime, "groundingStableTime", 0.08f);
        TestReflection.SetPrivateField(runtime, "groundTargetsInitialized", false);
        TestReflection.SetPrivateField(runtime, "supportAnchorValid", false);
        TestReflection.SetPrivateField(runtime, "groundingState", TitanLegGroundingState.Airborne);

        runtime.TickLegSystem(default, default, 0.02f);
        Assert.That(runtime.GroundingState, Is.EqualTo(TitanLegGroundingState.Landing));
        Assert.That(body.isKinematic, Is.False);
        Assert.That(body.useGravity, Is.True);
        Assert.That(runtime.GroundTargetsInitialized, Is.False);

        for (int i = 0; i < 8 && !runtime.GroundTargetsInitialized; i++)
        {
            runtime.TickLegSystem(default, default, 0.02f);
        }

        Assert.That(runtime.GroundingState, Is.EqualTo(TitanLegGroundingState.Grounded));
        Assert.That(body.isKinematic, Is.True);
        Assert.That(body.useGravity, Is.False);
    }

    [Test]
    public void Airborne_PlantSurfaceFallbackIsDisabled()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TitanRigRuntime runtime = fixture.Runtime;
        TickUntilGrounded(runtime);
        Collider floor = fixture.FloorCollider;
        runtime.SetPlantSurfaceForTests(true, floor, Vector3.zero, TitanGroundFrame.Up);
        TestReflection.SetPrivateField(runtime, "groundingState", TitanLegGroundingState.Airborne);
        TestReflection.SetPrivateField(runtime, "supportAnchorValid", false);
        TestReflection.SetPrivateField(runtime, "groundTargetsInitialized", false);
        runtime.ApplyMovementRootPose(new Vector3(0f, 3f, 0f), runtime.MovementRoot.rotation, zeroVelocities: true);
        Physics.SyncTransforms();

        bool found = runtime.TryGetFootGroundContactRobustForTests(true, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void LegPipeline_CountsMultipleCallsInSameFixedTime()
    {
        using RuntimeFixture fixture = RuntimeFixture.CreateProcedural();
        TickUntilGrounded(fixture.Runtime);
        fixture.Runtime.TickLegSystem(default, default, 0.02f);
        fixture.Runtime.TickLegSystem(default, default, 0.02f);

        Assert.That(fixture.Runtime.LegPipelineTickCountThisFixedFrame, Is.GreaterThanOrEqualTo(2));
    }

    private static void TickUntilGrounded(TitanRigRuntime runtime)
    {
        for (int i = 0; i < 4; i++)
        {
            Physics.SyncTransforms();
            runtime.TickLegSystem(default, default, 0.02f);
        }

        TestReflection.SetPrivateField(runtime, "lastLegPipelineFixedTime", double.NegativeInfinity);
    }

    private static void DisablePoseWriters(GameObject instance)
    {
        TitanRoleNetworkDriver driver = instance.GetComponent<TitanRoleNetworkDriver>();
        if (driver != null)
        {
            driver.enabled = false;
        }
    }

    private static void PrepareDynamicSpawn(TitanRigRuntime runtime, Rigidbody body, Vector3 position)
    {
        body.isKinematic = false;
        body.useGravity = true;
        runtime.ApplyMovementRootPose(position, Quaternion.identity, zeroVelocities: true);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        Physics.SyncTransforms();
    }

    private static void SimulateRuntimeFixedTick(TitanRigRuntime runtime)
    {
        Physics.Simulate(0.02f);
        Physics.SyncTransforms();
        runtime.TickLegSystem(default, default, 0.02f);
    }

    private static void AssertDiagnosticCollidersDoNotAffectMovementBody(FootAttachmentController attachment, Rigidbody movementBody)
    {
        Assert.That(attachment, Is.Not.Null);
        Collider[] diagnostics = attachment.PenetrationDiagnosticColliders;
        if (diagnostics == null)
        {
            return;
        }

        foreach (Collider diagnostic in diagnostics)
        {
            if (diagnostic == null || !diagnostic.enabled)
            {
                continue;
            }

            Assert.That(diagnostic.isTrigger || diagnostic.attachedRigidbody != movementBody, Is.True, diagnostic.name);
        }
    }

    private static void TickUntilDoubleSupport(TitanRigRuntime runtime, int maxTicks)
    {
        for (int i = 0; i < maxTicks && !runtime.IsDoubleSupport; i++)
        {
            Physics.SyncTransforms();
            runtime.TickLegSystem(default, default, 0.02f);
        }
    }

    private static void RunStep(TitanRigRuntime runtime, bool left, Vector2 horizontalDelta)
    {
        for (int i = 0; i < 24; i++)
        {
            runtime.TickLegSystem(
                left ? new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = horizontalDelta } : default,
                left ? default : new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = horizontalDelta },
                0.02f);
        }

        TickUntilDoubleSupport(runtime, 180);
    }

    private static void BeginRightSwing(TitanRigRuntime runtime)
    {
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f }, 0.02f);
        Assert.That(runtime.IsStepActive, Is.True);
        Assert.That(runtime.SupportFoot, Is.EqualTo(TitanSupportFoot.Left));
        Assert.That(runtime.RightLegState.IsPlanted, Is.False);
    }

    private static void TickRightSwing(TitanRigRuntime runtime, Vector2 horizontalDelta)
    {
        runtime.TickLegSystem(default, new TitanLegInputCommand { LiftInput = 1f, HorizontalDelta = horizontalDelta }, 0.02f);
    }

    private static Vector3 ExpectedWorldDelta(Vector2 mouseDelta, float sensitivity)
    {
        return (TitanGroundFrame.WorldRight * mouseDelta.x + TitanGroundFrame.WorldForward * mouseDelta.y) * sensitivity;
    }

    private static float GetFootMoveSensitivity(TitanRigRuntime runtime)
    {
        return TestReflection.GetPrivateField<float>(runtime, "footMoveSensitivity");
    }

    private static void AssertJointLimits(TitanRigRuntime runtime)
    {
        AssertJointLimits(runtime.LeftLegState.SolvedAngles);
        AssertJointLimits(runtime.RightLegState.SolvedAngles);
    }

    private static void AssertPlantedFootInvariant(TitanRigRuntime runtime, bool left, Collider floor)
    {
        TitanLegControlState state = left ? runtime.LeftLegState : runtime.RightLegState;
        FootAttachmentController attachment = left ? runtime.LeftFootAttachment : runtime.RightFootAttachment;
        Transform foot = left ? runtime.LeftFoot : runtime.RightFoot;
        if (!state.IsPlanted)
        {
            return;
        }

        float minGap = attachment.GetMinimumSignedSoleGap(Vector3.zero, TitanGroundFrame.Up);
        Assert.That(minGap, Is.GreaterThanOrEqualTo(-0.0025f), DescribeLegState(runtime, left));
        Assert.That(Vector3.Distance(foot.position, state.PlantAnchorWorld), Is.LessThanOrEqualTo(0.005f), DescribeLegState(runtime, left));
        Assert.That(state.LastSolveReached, Is.True, DescribeLegState(runtime, left));
        Assert.That(state.TargetWasClamped, Is.False, DescribeLegState(runtime, left));
        if (attachment.TryGetMaximumSolePenetration(floor, out float penetration, out _))
        {
            Assert.That(penetration, Is.LessThanOrEqualTo(0.0025f), DescribeLegState(runtime, left));
        }
    }

    private static void AssertJointLimits(TitanLegIkAngles angles)
    {
        Assert.That(float.IsFinite(angles.HipYaw), Is.True);
        Assert.That(float.IsFinite(angles.HipRoll), Is.True);
        Assert.That(float.IsFinite(angles.KneeRoll), Is.True);
        Assert.That(angles.HipYaw, Is.InRange(0f, 100f));
        Assert.That(angles.HipRoll, Is.InRange(0f, 100f));
        Assert.That(angles.KneeRoll, Is.InRange(1f, 179f));
    }

    private static void AssertProbeTargetHeight(TitanRigRuntime runtime, bool left, float floorY, float tolerance)
    {
        TitanLegControlState state = left ? runtime.LeftLegState : runtime.RightLegState;
        float probeToFoot = Vector3.Dot(runtime.GetFootProbeWorldPosition(left) - (left ? runtime.LeftFoot.position : runtime.RightFoot.position), TitanGroundFrame.Up);
        Assert.That(Vector3.Dot(state.DesiredGroundTarget, TitanGroundFrame.Up) + probeToFoot, Is.EqualTo(floorY).Within(tolerance));
    }

    private static string DescribeLegState(TitanRigRuntime runtime, bool left)
    {
        TitanLegControlState state = left ? runtime.LeftLegState : runtime.RightLegState;
        return $"step={runtime.IsStepActive} double={runtime.IsDoubleSupport} support={runtime.SupportFoot} lift={state.FootLift} liftTarget={state.FootLiftTarget} contact={state.HasGroundContact} clamped={state.TargetWasClamped} solveError={state.SolveError} desiredError={state.DesiredPositionError} actual={state.ActualFootPosition} desired={state.DesiredGroundTarget} reachable={state.ReachableFootTarget} plant={state.PlantAnchorWorld} root={runtime.MovementRoot.position}";
    }

    private static float ExpectedHighestRootHeight(TitanRigRuntime runtime)
    {
        TitanLegRootWorkspace left = new TitanLegRootWorkspace(
            runtime.LeftHip.position - runtime.MovementRoot.position,
            runtime.LeftLegState.PlantAnchorWorld,
            TestMaxReach(runtime, true));
        TitanLegRootWorkspace right = new TitanLegRootWorkspace(
            runtime.RightHip.position - runtime.MovementRoot.position,
            runtime.RightLegState.PlantAnchorWorld,
            TestMaxReach(runtime, false));
        TitanStanceRootResult result = TitanStanceRootSolver.SolveHighestDoubleSupport(runtime.MovementRoot.position, TitanGroundFrame.Up, left, right);
        Assert.That(result.Feasible, Is.True);
        return Vector3.Dot(result.RootPosition, TitanGroundFrame.Up);
    }

    private static float TestMaxReach(TitanRigRuntime runtime, bool left)
    {
        Transform hip = left ? runtime.LeftHip : runtime.RightHip;
        Transform knee = left ? runtime.LeftKnee : runtime.RightKnee;
        Transform foot = left ? runtime.LeftFoot : runtime.RightFoot;
        TitanLegSolverSettings settings = left ? runtime.LeftLegSolverSettings : runtime.RightLegSolverSettings;
        float upperLength = Vector3.Distance(hip.position, knee.position);
        float lowerLength = Vector3.Distance(knee.position, foot.position);
        float physicalKneeMinRadians = Mathf.Abs(settings.KneeRoll.ToPhysicalAngle(settings.KneeRoll.MinAngle)) * Mathf.Deg2Rad;
        float maxReachSquared = upperLength * upperLength
            + lowerLength * lowerLength
            + 2f * upperLength * lowerLength * Mathf.Cos(physicalKneeMinRadians);
        return Mathf.Sqrt(Mathf.Max(0f, maxReachSquared)) - settings.ReachMargin - 0.0005f;
    }
}

public static class TestReflection
{
    public static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindPrivateField(target, fieldName);
        field.SetValue(target, value);
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)FindPrivateField(target, fieldName).GetValue(target);
    }

    private static FieldInfo FindPrivateField(object target, string fieldName)
    {
        FieldInfo field = null;
        System.Type type = target.GetType();
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            type = type.BaseType;
        }

        Assert.That(field, Is.Not.Null, fieldName);
        return field;
    }
}

public sealed class RuntimeFixture : System.IDisposable
{
    private readonly GameObject root;
    private readonly GameObject floor;

    private RuntimeFixture(GameObject root, TitanRigRuntime runtime, GameObject floor)
    {
        this.root = root;
        this.floor = floor;
        Runtime = runtime;
    }

    public TitanRigRuntime Runtime { get; }
    public Collider FloorCollider => floor != null ? floor.GetComponent<Collider>() : null;

    public static RuntimeFixture CreateProcedural(bool withFloor = true, float floorY = 0f, float rightFootHeightOffset = 0f)
    {
        GameObject root = new GameObject("TitanLegPipelineRuntime");
        root.transform.position = Vector3.up * floorY;
        root.AddComponent<Rigidbody>().isKinematic = true;
        TitanRigRuntime runtime = root.AddComponent<TitanRigRuntime>();
        Transform spine = new GameObject("Spine").transform;
        spine.SetParent(root.transform);
        spine.localPosition = new Vector3(0f, 1.2f, 0f);
        CreateLeg(root.transform, true, out Transform leftHip, out Transform leftKnee, out Transform leftFoot);
        CreateLeg(root.transform, false, out Transform rightHip, out Transform rightKnee, out Transform rightFoot);
        rightHip.localPosition += Vector3.up * rightFootHeightOffset;
        GameObject floor = null;
        if (withFloor)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, floorY - 0.05f, 0f);
            floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
        }

        AddFootAttachment<TitanLeftFootAttachmentController>(root, leftFoot);
        AddFootAttachment<TitanRightFootAttachmentController>(root, rightFoot);
        TestReflection.SetPrivateField(runtime, "mechaRoot", root.transform);
        TestReflection.SetPrivateField(runtime, "leftHip", leftHip);
        TestReflection.SetPrivateField(runtime, "leftKnee", leftKnee);
        TestReflection.SetPrivateField(runtime, "leftFoot", leftFoot);
        TestReflection.SetPrivateField(runtime, "rightHip", rightHip);
        TestReflection.SetPrivateField(runtime, "rightKnee", rightKnee);
        TestReflection.SetPrivateField(runtime, "rightFoot", rightFoot);
        TestReflection.SetPrivateField(runtime, "spine", spine);
        Physics.SyncTransforms();
        return new RuntimeFixture(root, runtime, floor);
    }

    public void MoveFloor(float floorY)
    {
        if (floor == null)
        {
            return;
        }

        floor.transform.position = new Vector3(0f, floorY - 0.05f, 0f);
        Physics.SyncTransforms();
    }

    private static void AddFootAttachment<T>(GameObject root, Transform foot) where T : FootAttachmentController
    {
        T attachment = root.AddComponent<T>();
        Transform bottomProbe = new GameObject("BottomProbe").transform;
        bottomProbe.SetParent(foot, false);
        bottomProbe.localPosition = new Vector3(0f, -0.001f, 0f);

        Transform[] solePoints = new Transform[4];
        solePoints[0] = CreateSolePoint(foot, "HeelLeft", -0.08f, -0.004f, -0.15f);
        solePoints[1] = CreateSolePoint(foot, "HeelRight", 0.08f, -0.004f, -0.15f);
        solePoints[2] = CreateSolePoint(foot, "ToeLeft", -0.08f, -0.004f, 0.15f);
        solePoints[3] = CreateSolePoint(foot, "ToeRight", 0.08f, -0.004f, 0.15f);

        TestReflection.SetPrivateField(attachment, "footTransform", foot);
        TestReflection.SetPrivateField(attachment, "bottomProbe", bottomProbe);
        TestReflection.SetPrivateField(attachment, "soleContactPoints", solePoints);
        TestReflection.SetPrivateField(attachment, "penetrationDiagnosticColliders", System.Array.Empty<Collider>());
        attachment.RebuildCachedSoleGeometry();
    }

    private static Transform CreateSolePoint(Transform foot, string name, float x, float y, float z)
    {
        Transform point = new GameObject(name).transform;
        point.SetParent(foot, false);
        point.localPosition = new Vector3(x, y, z);
        return point;
    }

    private static void CreateLeg(Transform root, bool left, out Transform hip, out Transform knee, out Transform foot)
    {
        float x = left ? -0.35f : 0.35f;
        hip = new GameObject(left ? "LeftHip" : "RightHip").transform;
        knee = new GameObject(left ? "LeftKnee" : "RightKnee").transform;
        foot = new GameObject(left ? "LeftFoot" : "RightFoot").transform;
        hip.SetParent(root);
        knee.SetParent(hip);
        foot.SetParent(knee);
        hip.localPosition = new Vector3(x, 1.8f, 0f);
        knee.localPosition = new Vector3(0f, -0.9f, 0f);
        foot.localPosition = new Vector3(0f, -0.9f, 0f);
    }

    public void Dispose()
    {
        Object.DestroyImmediate(root);
        if (floor != null)
        {
            Object.DestroyImmediate(floor);
        }

        Physics.SyncTransforms();
    }
}
#endif
