public class TitanLeftLegRoleController : TitanBaseLegRoleController
{
    public override Define.TitanRole Role => Define.TitanRole.LeftLeg;

    protected override bool IsLeftLeg => true;
}
