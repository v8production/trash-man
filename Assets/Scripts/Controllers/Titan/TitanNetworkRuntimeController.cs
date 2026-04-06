using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Titan
{
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
        private TitanRig _rig;
        private TitanInputAggregationManager _inputAggregation;
        private TitanBodyRoleController _bodyController;

        private float _leftArmShoulderYaw;
        private float _leftArmShoulderPitch;
        private float _leftArmElbowPitch;

        private float _rightArmShoulderYaw;
        private float _rightArmShoulderPitch;
        private float _rightArmElbowPitch;

        private float _leftLegHipYaw;
        private float _leftLegHipRoll;
        private float _leftLegKneeRoll;

        private float _rightLegHipYaw;
        private float _rightLegHipRoll;
        private float _rightLegKneeRoll;

        public bool EnsureReady()
        {
            if (_rig != null && _inputAggregation != null && _bodyController != null)
            {
                return true;
            }

            _rig = FindAnyObjectByType<TitanRig>();
            if (_rig == null)
            {
                return false;
            }

            _inputAggregation = _rig.GetComponent<TitanInputAggregationManager>();
            if (_inputAggregation == null)
            {
                _inputAggregation = _rig.gameObject.AddComponent<TitanInputAggregationManager>();
            }

            _bodyController = _rig.GetComponent<TitanBodyRoleController>();
            if (_bodyController == null)
            {
                _bodyController = _rig.gameObject.AddComponent<TitanBodyRoleController>();
            }

            return true;
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

            _inputAggregation.SetCurrent(aggregated);
            _bodyController.SetInputEnabled(true);
            _bodyController.TickRoleInput(deltaTime);
            _bodyController.TickPhysics(deltaTime);

            SimulateArmRole(roleInputs, Define.TitanRole.LeftArm, deltaTime, true);
            SimulateArmRole(roleInputs, Define.TitanRole.RightArm, deltaTime, false);
            SimulateLegRole(roleInputs, Define.TitanRole.LeftLeg, deltaTime, true);
            SimulateLegRole(roleInputs, Define.TitanRole.RightLeg, deltaTime, false);

            if (!_rig.TryGetPoseSnapshot(out TitanRigPoseSnapshot snapshot))
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

            _rig.ApplyPoseSnapshot(snapshot);
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

            float targetYaw = input.AimX * 55f;
            float targetPitch = input.AimY * 360f;
            float blend = 1f - Mathf.Exp(-5f * deltaTime);

            if (left)
            {
                _leftArmShoulderYaw = Mathf.Lerp(_leftArmShoulderYaw, targetYaw, blend);
                _leftArmShoulderPitch = Mathf.Lerp(_leftArmShoulderPitch, targetPitch, blend);
                _leftArmElbowPitch = Mathf.Clamp(_leftArmElbowPitch + (input.PrimaryAxis * 120f * deltaTime), -15f, 130f);
                _rig.ApplyLeftArm(_leftArmShoulderPitch, _leftArmShoulderYaw, _leftArmElbowPitch);
                return;
            }

            _rightArmShoulderYaw = Mathf.Lerp(_rightArmShoulderYaw, targetYaw, blend);
            _rightArmShoulderPitch = Mathf.Lerp(_rightArmShoulderPitch, targetPitch, blend);
            _rightArmElbowPitch = Mathf.Clamp(_rightArmElbowPitch + (input.PrimaryAxis * 120f * deltaTime), -15f, 130f);
            _rig.ApplyRightArm(_rightArmShoulderPitch, _rightArmShoulderYaw, _rightArmElbowPitch);
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

            if (left)
            {
                _leftLegHipYaw = Mathf.Lerp(_leftLegHipYaw, targetYaw, blend);
                _leftLegHipRoll = Mathf.Lerp(_leftLegHipRoll, targetRoll, blend);
                _leftLegKneeRoll = Mathf.Clamp(_leftLegKneeRoll + (input.PrimaryAxis * 110f * deltaTime), -5f, 125f);
                _rig.ApplyLeftLeg(_leftLegHipYaw, _leftLegHipRoll, _leftLegKneeRoll);
                return;
            }

            _rightLegHipYaw = Mathf.Lerp(_rightLegHipYaw, targetYaw, blend);
            _rightLegHipRoll = Mathf.Lerp(_rightLegHipRoll, targetRoll, blend);
            _rightLegKneeRoll = Mathf.Clamp(_rightLegKneeRoll + (input.PrimaryAxis * 110f * deltaTime), -5f, 125f);
            _rig.ApplyRightLeg(_rightLegHipYaw, _rightLegHipRoll, _rightLegKneeRoll);
        }
    }
}
