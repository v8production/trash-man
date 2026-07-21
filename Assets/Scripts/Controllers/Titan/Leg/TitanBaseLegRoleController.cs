public abstract class TitanBaseLegRoleController : TitanBaseController
{
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
    }

    public TitanLegInputCommand ConsumePendingCommand()
    {
        TitanLegInputCommand result = _pendingCommand;
        _pendingCommand = default;
        return result;
    }

}
