using UnityEngine;

public abstract class TitanBaseLegRoleController : TitanBaseController
{
    public enum LegSide
    {
        Left,
        Right,
    }

    [Header("Input")]
    [SerializeField] private TitanLegAnchorResolver legAnchorResolver;

    [Header("Hip Mouse Mapping")]
    [SerializeField] private float hipRadiusPixels = 260f;
    [SerializeField] private bool useScreenCenterAsOrigin = true;
    [SerializeField] private Vector2 mouseOriginPixels = new(960f, 540f);
    [SerializeField] private Vector2 hipYawLimit = new(-40f, 40f);
    [SerializeField] private Vector2 hipRollLimit = new(-90f, 45f);

    [Header("Hip Response")]
    [SerializeField] private float hipSpeed = 2f;

    [Header("Knee Input")]
    [SerializeField] private float kneeSpeed = 110f;
    [SerializeField] private Vector2 kneeRollLimit = new(-5f, 125f);

    [Header("Idle Return")]
    [SerializeField] private float idleReturnSpeed = 12f;

    protected abstract bool IsLeftLeg { get; }

    protected override void Awake()
    {
        base.Awake();
        ResolveDependencies();
    }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            InputDebug.LogWarning($"[TitanLeg] {Role} skipped: rig not ready.");
            return;
        }

        ResolveDependencies();
        TitanLegInputCommand command = EvaluateLegInput(input, Managers.Input.GetTitanMouseSensitivity());
        InputDebug.Log($"[TitanLeg] {Role} command side={command.Side} detach={command.DetachHeld} mouse={command.MousePosition} delta={command.MouseDelta} targetYaw={command.TargetHipYaw:F2} targetRoll={command.TargetHipRoll:F2} knee={command.KneeInput:F2}");
        legAnchorResolver?.UpdateDetachState(command.Side, command.DetachHeld);

        TitanLegControlState state = Managers.TitanRig.GetLegState(left: IsLeftLeg);
        if (legAnchorResolver != null && legAnchorResolver.TryApplyAnchoredMovement(command.Side, command, state, deltaTime))
        {
            InputDebug.Log($"[TitanLeg] {Role} applied anchored movement.");
            Managers.TitanRig.ApplyLegPose(left: IsLeftLeg);
            return;
        }

        float blend = 1f - Mathf.Exp(-hipSpeed * deltaTime);
        state.HipYaw = Mathf.Lerp(state.HipYaw, command.TargetHipYaw, blend);
        state.HipRoll = Mathf.Lerp(state.HipRoll, command.TargetHipRoll, blend);

        state.HipYaw = Mathf.Clamp(state.HipYaw, hipYawLimit.x, hipYawLimit.y);
        state.HipRoll = Mathf.Clamp(state.HipRoll, hipRollLimit.x, hipRollLimit.y);
        state.KneeRoll = Mathf.Clamp(
            state.KneeRoll + (command.KneeInput * kneeSpeed * deltaTime),
            kneeRollLimit.x,
            kneeRollLimit.y);

        Managers.TitanRig.SetLegState(left: IsLeftLeg, state);
        Managers.TitanRig.ApplyLegPose(left: IsLeftLeg);
    }

    public void TickIdle(float deltaTime)
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            return;
        }

        TitanLegControlState state = Managers.TitanRig.GetLegState(left: IsLeftLeg);
        float blend = 1f - Mathf.Exp(-idleReturnSpeed * deltaTime);

        state.HipYaw = Mathf.Lerp(state.HipYaw, 0f, blend);
        state.HipRoll = Mathf.Lerp(state.HipRoll, 0f, blend);
        state.KneeRoll = Mathf.Lerp(state.KneeRoll, 0f, blend);

        ResolveDependencies();
        state.HipYaw = Mathf.Clamp(state.HipYaw, hipYawLimit.x, hipYawLimit.y);
        state.HipRoll = Mathf.Clamp(state.HipRoll, hipRollLimit.x, hipRollLimit.y);
        state.KneeRoll = Mathf.Clamp(state.KneeRoll, kneeRollLimit.x, kneeRollLimit.y);

        Managers.TitanRig.SetLegState(left: IsLeftLeg, state);
        Managers.TitanRig.ApplyLegPose(left: IsLeftLeg);
    }

    private void ResolveDependencies()
    {
        legAnchorResolver ??= GetComponent<TitanLegAnchorResolver>();
    }

    private TitanLegInputCommand EvaluateLegInput(in TitanAggregatedInput input, float sensitivity)
    {
        Vector2 origin = useScreenCenterAsOrigin
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : mouseOriginPixels;

        float resolvedBaseRadius = Mathf.Max(0.01f, hipRadiusPixels);
        float normalizedX = Mathf.Clamp((input.MousePosition.x - origin.x) / resolvedBaseRadius, -1f, 1f);
        float normalizedY = Mathf.Clamp((input.MousePosition.y - origin.y) / resolvedBaseRadius, -1f, 1f);
        float maxYawDegrees = Mathf.Max(Mathf.Abs(hipYawLimit.x), Mathf.Abs(hipYawLimit.y));
        float maxRollDegrees = Mathf.Max(Mathf.Abs(hipRollLimit.x), Mathf.Abs(hipRollLimit.y));

        float targetYaw = Mathf.Clamp(normalizedX * maxYawDegrees * sensitivity, hipYawLimit.x, hipYawLimit.y);
        float targetRoll = Mathf.Clamp(-normalizedY * maxRollDegrees * sensitivity, hipRollLimit.x, hipRollLimit.y);
        float kneeInput = IsLeftLeg ? input.LeftLegKnee : input.RightLegKnee;

        return new TitanLegInputCommand
        {
            Side = IsLeftLeg ? LegSide.Left : LegSide.Right,
            MousePosition = input.MousePosition,
            MouseDelta = input.MouseDelta,
            TargetHipYaw = targetYaw,
            TargetHipRoll = targetRoll,
            KneeInput = kneeInput,
            DetachHeld = input.RightMouseDetachBuffered || input.RightMouseHeld || input.RightMousePressedThisFrame,
        };
    }
}

[System.Serializable]
public struct TitanLegInputCommand
{
    public TitanBaseLegRoleController.LegSide Side;
    public Vector2 MousePosition;
    public Vector2 MouseDelta;
    public float TargetHipYaw;
    public float TargetHipRoll;
    public float KneeInput;
    public bool DetachHeld;
}
