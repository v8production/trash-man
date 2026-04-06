public class TitanRightLegRoleController : TitanBaseLegRoleController
{
    public override Define.TitanRole Role => Define.TitanRole.RightLeg;

    protected override bool IsLeftLeg => false;
}
