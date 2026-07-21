using System;
using UnityEngine;

public enum TitanSupportFoot
{
    Left,
    Right,
}

public enum TitanLegGroundingState
{
    Airborne,
    Landing,
    Grounded,
}

public enum TitanLegIkSolveMode
{
    Tracking,
    Recovery,
    CanonicalizePosture,
}

[Serializable]
public struct TitanJointConstraint
{
    public Vector3 LocalAxis;
    public float AxisSign;
    public float MinAngle;
    public float MaxAngle;
    public float NeutralAngle;

    public float Clamp(float logicalAngle)
    {
        return Mathf.Clamp(logicalAngle, MinAngle, MaxAngle);
    }

    public float ToPhysicalAngle(float logicalAngle)
    {
        float sign = AxisSign >= 0f ? 1f : -1f;
        return sign * (Clamp(logicalAngle) - NeutralAngle);
    }
}

[Serializable]
public struct TitanLegIkAngles
{
    public float HipYaw;
    public float HipRoll;
    public float KneeRoll;
}

[Serializable]
public struct TitanLegSolverSettings
{
    public TitanJointConstraint HipYaw;
    public TitanJointConstraint HipRoll;
    public TitanJointConstraint KneeRoll;

    public int Iterations;
    public float Damping;
    public float FiniteDifferenceDegrees;
    public float MaxStepDegrees;
    public float PositionTolerance;
    public float ReachMargin;

    public static TitanLegSolverSettings CreateDefault()
    {
        return new TitanLegSolverSettings
        {
            HipYaw = new TitanJointConstraint
            {
                LocalAxis = Vector3.up,
                AxisSign = 1f,
                MinAngle = 0f,
                MaxAngle = 100f,
                NeutralAngle = 50f,
            },
            HipRoll = new TitanJointConstraint
            {
                LocalAxis = Vector3.forward,
                AxisSign = -1f,
                MinAngle = 0f,
                MaxAngle = 100f,
                NeutralAngle = 0f,
            },
            KneeRoll = new TitanJointConstraint
            {
                LocalAxis = Vector3.forward,
                AxisSign = 1f,
                MinAngle = 1f,
                MaxAngle = 179f,
                NeutralAngle = 0f,
            },
            Iterations = 12,
            Damping = 0.08f,
            FiniteDifferenceDegrees = 0.25f,
            MaxStepDegrees = 10f,
            PositionTolerance = 0.002f,
            ReachMargin = 0.0005f,
        };
    }
}

public struct TitanLegIkResult
{
    public TitanLegIkAngles Angles;
    public Vector3 DesiredTarget;
    public Vector3 ReachableTarget;
    public Vector3 ActualPosition;
    public float PositionError;
    public bool TargetWasClamped;
    public float DesiredPositionError;
    public bool Reached;
}

public struct TitanRootPose
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public static TitanRootPose From(Transform root)
    {
        return new TitanRootPose
        {
            Position = root.position,
            Rotation = root.rotation,
            Scale = root.lossyScale,
        };
    }
}

public struct TitanLegKinematicModel
{
    public bool Valid;
    public Matrix4x4 RootToHipParent;
    public Vector3 HipLocalPosition;
    public Vector3 HipLocalScale;
    public Quaternion HipBaseLocalRotation;
    public Matrix4x4 HipToKneeParentStatic;
    public Vector3 KneeLocalPosition;
    public Vector3 KneeLocalScale;
    public Quaternion KneeBaseLocalRotation;
    public Matrix4x4 KneeToFootStatic;
    public float UpperLength;
    public float LowerLength;
    public float MaxReach;
    public Vector3 HipOffsetRootLocal;
    public int Version;
}

public struct TitanLegPoseEvaluation
{
    public Vector3 HipPosition;
    public Vector3 KneePosition;
    public Vector3 FootPosition;
    public Quaternion HipBaseWorldRotation;
    public Quaternion HipWorldRotation;
    public Quaternion KneeBaseWorldRotation;
    public Vector3 HipYawAxisWorld;
    public Vector3 HipRollAxisWorld;
    public Vector3 KneeRollAxisWorld;
}

public struct TitanLegSolveCache
{
    public bool Valid;
    public int KinematicModelVersion;
    public Vector3 Target;
    public Vector3 RootPosition;
    public Quaternion RootRotation;
    public TitanLegIkAngles Angles;
    public TitanLegIkResult Result;
}

public static class TitanLegSupportResolver
{
    public static TitanSupportFoot Resolve(
        TitanSupportFoot currentSupport,
        float leftCandidateHeight,
        float rightCandidateHeight,
        float leftActualLift,
        float rightActualLift,
        bool leftCanSupport,
        bool rightCanSupport,
        float switchHysteresis,
        float contactTolerance)
    {
        if (currentSupport == TitanSupportFoot.Left)
        {
            if (rightCanSupport && rightCandidateHeight < leftCandidateHeight - switchHysteresis && rightActualLift <= contactTolerance)
            {
                return TitanSupportFoot.Right;
            }

            return TitanSupportFoot.Left;
        }

        if (leftCanSupport && leftCandidateHeight < rightCandidateHeight - switchHysteresis && leftActualLift <= contactTolerance)
        {
            return TitanSupportFoot.Left;
        }

        return TitanSupportFoot.Right;
    }
}
