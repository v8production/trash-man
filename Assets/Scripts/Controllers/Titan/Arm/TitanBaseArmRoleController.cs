using UnityEngine;

public abstract class TitanBaseArmRoleController : TitanBaseController
{
    [Header("Shoulder Mouse Mapping")]
    [SerializeField] private float maxThetaDegrees = 55f;
    [SerializeField] private float thetaRadiusPixels = 260f;
    [SerializeField] private float shoulderYawRadiusPixels = 2600f;
    [SerializeField] private bool useScreenCenterAsOrigin = true;
    [SerializeField] private Vector2 mouseOriginPixels = new(960f, 540f);
    [SerializeField] private float shoulderSpeed = 1f;

    [Header("Elbow Input")]
    [SerializeField] private float elbowSpeed = 120f;
    [SerializeField] private Vector2 shoulderYawLimit = new(-15f, 45f);
    [SerializeField] private Vector2 shoulderPitchLimit = new(-360f, 360f);
    [SerializeField] private Vector2 elbowPitchLimit = new(-130f, 15f);
    [SerializeField] private Vector2 rightElbowPitchLimit = new(0f, 180f);

    [Header("Idle Return")]
    [SerializeField] private float idleReturnSpeed = 10f;

    protected abstract bool IsLeftArm { get; }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            return;
        }

        Vector2 mousePosition = input.MousePosition;
        Vector2 origin = useScreenCenterAsOrigin
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : mouseOriginPixels;

        Vector2 targetAngles = TitanInputUtility.ComputeSphericalAngles(
            mousePosition,
            origin,
            thetaRadiusPixels,
            maxThetaDegrees,
            Managers.Input.GetTitanMouseSensitivity(),
            secondaryMaxDegrees: 360f,
            applySensitivityToSecondary: false);

        float normalizedX = shoulderYawRadiusPixels > 0f
            ? Mathf.Clamp((mousePosition.x - origin.x) / shoulderYawRadiusPixels, -1f, 1f)
            : 0f;
        float yawT = Mathf.InverseLerp(-1f, 1f, normalizedX);
        if (IsLeftArm)
        {
            yawT = 1f - yawT;
        }

        float targetYaw = Mathf.Lerp(shoulderYawLimit.x, shoulderYawLimit.y, yawT);
        float targetPitch = targetAngles.y;

        TitanArmControlState state = Managers.TitanRig.GetArmState(left: IsLeftArm);
        float blend = 1f - Mathf.Exp(-shoulderSpeed * deltaTime);
        state.ShoulderYaw = Mathf.Lerp(state.ShoulderYaw, targetYaw, blend);
        state.ShoulderPitch = Mathf.Lerp(state.ShoulderPitch, targetPitch, blend);

        float elbowInput = IsLeftArm ? input.LeftArmElbow : -input.RightArmElbow;

        state.ShoulderPitch = Mathf.Clamp(state.ShoulderPitch, shoulderPitchLimit.x, shoulderPitchLimit.y);
        state.ShoulderYaw = Mathf.Clamp(state.ShoulderYaw, shoulderYawLimit.x, shoulderYawLimit.y);
        Vector2 resolvedElbowLimit = IsLeftArm ? elbowPitchLimit : rightElbowPitchLimit;
        state.ElbowPitch = Mathf.Clamp(
            state.ElbowPitch - (elbowInput * elbowSpeed * deltaTime),
            resolvedElbowLimit.x,
            resolvedElbowLimit.y);

        Managers.TitanRig.SetArmState(left: IsLeftArm, state);
        Managers.TitanRig.ApplyArmPose(left: IsLeftArm);
    }

    public void TickIdle(float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            return;
        }

        TitanArmControlState state = Managers.TitanRig.GetArmState(left: IsLeftArm);
        float blend = 1f - Mathf.Exp(-idleReturnSpeed * deltaTime);

        state.ShoulderYaw = Mathf.Lerp(state.ShoulderYaw, 0f, blend);
        state.ShoulderPitch = Mathf.Lerp(state.ShoulderPitch, 0f, blend);
        state.ElbowPitch = Mathf.Lerp(state.ElbowPitch, 0f, blend);

        state.ShoulderPitch = Mathf.Clamp(state.ShoulderPitch, shoulderPitchLimit.x, shoulderPitchLimit.y);
        state.ShoulderYaw = Mathf.Clamp(state.ShoulderYaw, shoulderYawLimit.x, shoulderYawLimit.y);
        Vector2 resolvedElbowLimit = IsLeftArm ? elbowPitchLimit : rightElbowPitchLimit;
        state.ElbowPitch = Mathf.Clamp(state.ElbowPitch, resolvedElbowLimit.x, resolvedElbowLimit.y);

        Managers.TitanRig.SetArmState(left: IsLeftArm, state);
        Managers.TitanRig.ApplyArmPose(left: IsLeftArm);
    }
}
