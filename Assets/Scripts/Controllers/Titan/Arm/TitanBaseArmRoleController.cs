using UnityEngine;

public abstract class TitanBaseArmRoleController : TitanBaseController
{
    private const float ShoulderRollSensitivity = 0.12f;
    private const float ShoulderPitchSensitivity = 0.12f;
    private const float ElbowSpeed = 90f;
    private const float MinShoulderRoll = -45f;
    private const float MaxShoulderRoll = 45f;
    private const float MinShoulderPitch = -60f;
    private const float MaxShoulderPitch = 60f;
    private const float MinElbowPitch = 0f;
    private const float MaxElbowPitch = 120f;

    protected abstract bool IsLeftArm { get; }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        TitanArmControlState state = Managers.TitanRig.GetArmState(IsLeftArm);
        float shoulderPitchDirection = IsLeftArm ? 1f : -1f;

        state.ShoulderRoll = Mathf.Clamp(state.ShoulderRoll + input.MouseDelta.x * ShoulderRollSensitivity, MinShoulderRoll, MaxShoulderRoll);
        state.ShoulderPitch = Mathf.Clamp(state.ShoulderPitch + input.MouseDelta.y * ShoulderPitchSensitivity * shoulderPitchDirection, MinShoulderPitch, MaxShoulderPitch);
        state.ElbowPitch = Mathf.Clamp(state.ElbowPitch + input.ArmElbowInput * ElbowSpeed * deltaTime, MinElbowPitch, MaxElbowPitch);
        Managers.TitanRig.SetArmState(IsLeftArm, state);
        Managers.TitanRig.ApplyArmPose(IsLeftArm);
    }
}
