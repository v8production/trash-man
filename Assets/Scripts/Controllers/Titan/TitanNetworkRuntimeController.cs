using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public struct TitanRoleRuntimeInput
{
    public Define.TitanRole Role;
    public float AimX;
    public float AimY;
    public float PrimaryAxis;
    public float SecondaryAxis;
}

public struct TitanRuntimePoseState
{
    public Vector3 RootPosition;
    public Quaternion RootRotation;

    public Quaternion LeftShoulder;
    public Quaternion LeftElbow;
    public Quaternion RightShoulder;
    public Quaternion RightElbow;
    public Quaternion LeftHip;
    public Quaternion LeftKnee;
    public Quaternion RightHip;
    public Quaternion RightKnee;
    public Quaternion Spine;

    public bool Valid;
}

public sealed class TitanNetworkRuntimeController : MonoBehaviour
{
    private readonly TitanRigManager _rigManager = Managers.TitanRig;
    private TitanRigRuntime _rigRuntime;
    private TitanBodyRoleController _bodyController;

    public bool EnsureReady()
    {
        if (_rigRuntime != null && _bodyController != null)
        {
            return true;
        }

        if (!_rigManager.TryGetRuntime(out _rigRuntime))
        {
            return false;
        }

        TitanController titanController = _rigRuntime.GetComponent<TitanController>();
        if (titanController == null)
        {
            titanController = _rigRuntime.gameObject.AddComponent<TitanController>();
        }

        titanController.EnsureInitialized();

        _bodyController = _rigRuntime.GetComponent<TitanBodyRoleController>();

        return _bodyController != null && _rigManager.EnsureReady();
    }

    public TitanRoleRuntimeInput CaptureLocalInput(Define.TitanRole role)
    {
        TitanRoleRuntimeInput input = default;
        input.Role = role;

        if (role == Define.TitanRole.Body)
        {
            input.PrimaryAxis = TitanInputUtility.GetAxis(KeyCode.UpArrow, KeyCode.DownArrow, Key.UpArrow, Key.DownArrow);
            input.SecondaryAxis = TitanInputUtility.GetAxis(KeyCode.RightArrow, KeyCode.LeftArrow, Key.RightArrow, Key.LeftArrow);
            input.AimX = TitanInputUtility.GetAxis(KeyCode.Period, KeyCode.Comma, Key.Period, Key.Comma);
            input.AimY = TitanInputUtility.GetAxis(KeyCode.D, KeyCode.A, Key.D, Key.A);
            return input;
        }

        Vector2 mouse = TitanInputUtility.ReadMousePosition();
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 normalized = new Vector2(
            center.x <= 0f ? 0f : Mathf.Clamp((mouse.x - center.x) / center.x, -1f, 1f),
            center.y <= 0f ? 0f : Mathf.Clamp((mouse.y - center.y) / center.y, -1f, 1f));

        normalized = TitanInputUtility.KeepDominantAxis(normalized, 0.02f);

        input.AimX = normalized.x;
        input.AimY = normalized.y;
        input.PrimaryAxis = TitanInputUtility.GetAxis(KeyCode.W, KeyCode.S, Key.W, Key.S);
        input.SecondaryAxis = 0f;
        return input;
    }

    public bool SimulateAuthoritativeStep(
        IReadOnlyDictionary<Define.TitanRole, TitanRoleRuntimeInput> roleInputs,
        float deltaTime,
        out TitanRuntimePoseState pose)
    {
        pose = default;

        if (!EnsureReady())
        {
            return false;
        }

        TitanAggregatedInput aggregated = default;
        if (roleInputs.TryGetValue(Define.TitanRole.Body, out TitanRoleRuntimeInput body))
        {
            aggregated.BodyForward = body.PrimaryAxis;
            aggregated.BodyStrafe = body.SecondaryAxis;
            aggregated.BodyTurn = body.AimX;
            aggregated.BodyWaist = body.AimY;
        }

        TitanBaseController.SetSharedInput(aggregated);
        _bodyController.SetInputEnabled(true);
        _bodyController.TickRoleInput(deltaTime);
        _bodyController.TickPhysics(deltaTime);

        SimulateArmRole(roleInputs, Define.TitanRole.LeftArm, deltaTime, true);
        SimulateArmRole(roleInputs, Define.TitanRole.RightArm, deltaTime, false);
        SimulateLegRole(roleInputs, Define.TitanRole.LeftLeg, deltaTime, true);
        SimulateLegRole(roleInputs, Define.TitanRole.RightLeg, deltaTime, false);

        if (!_rigManager.TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot))
        {
            return false;
        }

        pose = new TitanRuntimePoseState
        {
            RootPosition = snapshot.RootPosition,
            RootRotation = snapshot.RootRotation,
            LeftShoulder = snapshot.LeftShoulderRotation,
            LeftElbow = snapshot.LeftElbowRotation,
            RightShoulder = snapshot.RightShoulderRotation,
            RightElbow = snapshot.RightElbowRotation,
            LeftHip = snapshot.LeftHipRotation,
            LeftKnee = snapshot.LeftKneeRotation,
            RightHip = snapshot.RightHipRotation,
            RightKnee = snapshot.RightKneeRotation,
            Spine = snapshot.SpineRotation,
            Valid = true,
        };

        return true;
    }

    public void ApplyRuntimePose(in TitanRuntimePoseState pose)
    {
        if (!pose.Valid || !EnsureReady())
        {
            return;
        }

        TitanRigPoseSnapshot snapshot = new TitanRigPoseSnapshot
        {
            RootPosition = pose.RootPosition,
            RootRotation = pose.RootRotation,
            HasLeftShoulder = true,
            LeftShoulderRotation = pose.LeftShoulder,
            HasLeftElbow = true,
            LeftElbowRotation = pose.LeftElbow,
            HasRightShoulder = true,
            RightShoulderRotation = pose.RightShoulder,
            HasRightElbow = true,
            RightElbowRotation = pose.RightElbow,
            HasLeftHip = true,
            LeftHipRotation = pose.LeftHip,
            HasLeftKnee = true,
            LeftKneeRotation = pose.LeftKnee,
            HasRightHip = true,
            RightHipRotation = pose.RightHip,
            HasRightKnee = true,
            RightKneeRotation = pose.RightKnee,
            HasSpine = true,
            SpineRotation = pose.Spine,
        };

        _rigManager.ApplyPoseSnapshot(snapshot);
    }

    private void SimulateArmRole(
        IReadOnlyDictionary<Define.TitanRole, TitanRoleRuntimeInput> roleInputs,
        Define.TitanRole role,
        float deltaTime,
            bool left)
    {
        if (!roleInputs.TryGetValue(role, out TitanRoleRuntimeInput input))
        {
            return;
        }

        float yawT = Mathf.InverseLerp(-1f, 1f, input.AimX);
        if (left)
        {
            yawT = 1f - yawT;
        }

        float targetYaw = Mathf.Lerp(90f, 180f, yawT);
        float targetPitch = input.AimY * 360f;
        float blend = 1f - Mathf.Exp(-5f * deltaTime);

        UpdateArmState(left, targetYaw, targetPitch, input.PrimaryAxis, blend, deltaTime);
    }

    private void SimulateLegRole(
        IReadOnlyDictionary<Define.TitanRole, TitanRoleRuntimeInput> roleInputs,
        Define.TitanRole role,
        float deltaTime,
        bool left)
    {
        if (!roleInputs.TryGetValue(role, out TitanRoleRuntimeInput input))
        {
            return;
        }

        float targetYaw = input.AimX * 40f;
        float targetRoll = input.AimY * 90f;
        float blend = 1f - Mathf.Exp(-12f * deltaTime);

        UpdateLegState(left, targetYaw, targetRoll, input.PrimaryAxis, blend, deltaTime);
    }

    private void UpdateArmState(bool left, float targetYaw, float targetPitch, float primaryAxis, float blend, float deltaTime)
    {
        TitanArmControlState state = _rigManager.GetArmState(left);
        state.ShoulderYaw = Mathf.Clamp(Mathf.Lerp(state.ShoulderYaw, targetYaw, blend), 90f, 180f);
        state.ShoulderPitch = Mathf.Lerp(state.ShoulderPitch, targetPitch, blend);
        state.ElbowPitch = Mathf.Clamp(state.ElbowPitch - (primaryAxis * 120f * deltaTime), -15f, 130f);

        _rigManager.SetArmState(left, state);
        _rigManager.ApplyArmPose(left);
    }

    private void UpdateLegState(bool left, float targetYaw, float targetRoll, float primaryAxis, float blend, float deltaTime)
    {
        TitanLegControlState state = _rigManager.GetLegState(left);
        state.HipYaw = Mathf.Lerp(state.HipYaw, targetYaw, blend);
        state.HipRoll = Mathf.Lerp(state.HipRoll, targetRoll, blend);
        state.KneeRoll = Mathf.Clamp(state.KneeRoll + (primaryAxis * 110f * deltaTime), -5f, 125f);

        _rigManager.SetLegState(left, state);
        _rigManager.ApplyLegPose(left);
    }
}
