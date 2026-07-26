using UnityEngine;

public abstract class TitanBaseLegRoleController : TitanBaseController
{
    private const float FootMoveInputEpsilonSqr = 0.0001f;
    private const float FootLiftMotorEpsilon = 0.001f;

    private TitanLegInputCommand _pendingCommand;

    public enum LegSide
    {
        Left,
        Right,
    }

    protected abstract bool IsLeftLeg { get; }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        _pendingCommand = TitanLegInputCommand.From(input);

        bool verticalInputActive = !Mathf.Approximately(input.LegScrollInput, 0f);
        bool horizontalInputActive = input.MouseDelta.sqrMagnitude > FootMoveInputEpsilonSqr && CanMoveFootHorizontally();
        if (verticalInputActive || horizontalInputActive)
            MovementFeedback.RequestMotorActivity();
    }

    private bool CanMoveFootHorizontally()
    {
        TitanLegControlState state = Managers.TitanRig.GetLegState(IsLeftLeg);
        return !state.HasGroundContact
            || !state.IsPlanted
            || state.FootLift > FootLiftMotorEpsilon
            || state.FootLiftTarget > FootLiftMotorEpsilon;
    }

    public TitanLegInputCommand ConsumePendingCommand()
    {
        TitanLegInputCommand result = _pendingCommand;
        _pendingCommand = default;
        return result;
    }

}
