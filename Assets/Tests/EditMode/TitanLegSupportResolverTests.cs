#if UNITY_EDITOR
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public sealed class TitanLegSupportResolverTests
{
    [Test]
    public void SupportSelection_UsesHysteresis()
    {
        TitanSupportFoot withinThreshold = TitanLegSupportResolver.Resolve(TitanSupportFoot.Left, 1f, 0.995f, 0f, 0f, true, true, 0.01f, 0.015f);
        TitanSupportFoot beyondThreshold = TitanLegSupportResolver.Resolve(TitanSupportFoot.Left, 1f, 0.98f, 0f, 0f, true, true, 0.01f, 0.015f);
        TitanSupportFoot noiseBack = TitanLegSupportResolver.Resolve(beyondThreshold, 0.985f, 0.98f, 0f, 0f, true, true, 0.01f, 0.015f);

        Assert.That(withinThreshold, Is.EqualTo(TitanSupportFoot.Left));
        Assert.That(beyondThreshold, Is.EqualTo(TitanSupportFoot.Right));
        Assert.That(noiseBack, Is.EqualTo(TitanSupportFoot.Right));
    }

    [Test]
    public void AirborneFoot_CannotBecomeSupport()
    {
        TitanSupportFoot resolved = TitanLegSupportResolver.Resolve(TitanSupportFoot.Left, 1f, 0.8f, 0f, 0.05f, true, true, 0.01f, 0.015f);

        Assert.That(resolved, Is.EqualTo(TitanSupportFoot.Left));
    }

    [Test]
    public void CandidateWithoutContact_CannotBecomeSupport()
    {
        TitanSupportFoot resolved = TitanLegSupportResolver.Resolve(TitanSupportFoot.Left, 1f, 0.8f, 0f, 0f, true, false, 0.01f, 0.015f);

        Assert.That(resolved, Is.EqualTo(TitanSupportFoot.Left));
    }

    [Test]
    public void LiftTarget_RisesWhileScrolling_AndFallsWithoutScroll()
    {
        GameObject root = new GameObject("LiftRuntimeTest");
        TitanRigRuntime runtime = root.AddComponent<TitanRigRuntime>();
        Transform hip = new GameObject("LeftHip").transform;
        Transform knee = new GameObject("LeftKnee").transform;
        Transform foot = new GameObject("LeftFoot").transform;
        hip.SetParent(root.transform);
        knee.SetParent(hip);
        foot.SetParent(knee);
        hip.localPosition = new Vector3(0f, 1.8f, 0f);
        knee.localPosition = new Vector3(0f, -0.9f, 0f);
        foot.localPosition = new Vector3(0f, -0.9f, 0f);
        SetPrivateField(runtime, "leftHip", hip);
        SetPrivateField(runtime, "leftKnee", knee);
        SetPrivateField(runtime, "leftFoot", foot);

        try
        {
            for (int i = 0; i < 10; i++)
            {
                float previous = runtime.LeftLegState.FootLiftTarget;
                runtime.IntegrateLiftTargetForTests(true, 1f, 0.02f);
                Assert.That(runtime.LeftLegState.FootLiftTarget, Is.GreaterThanOrEqualTo(previous));
                Assert.That(runtime.LeftLegState.FootLiftTarget, Is.LessThanOrEqualTo(1.2f));
            }

            float raised = runtime.LeftLegState.FootLiftTarget;
            runtime.IntegrateLiftTargetForTests(true, 0f, 0.02f);
            Assert.That(runtime.LeftLegState.FootLiftTarget, Is.LessThan(raised));
            Assert.That(runtime.LeftLegState.FootLiftTarget, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
#endif
