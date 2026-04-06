public class TitanRightArmRoleController : TitanBaseArmRoleController
{
    public override Define.TitanRole Role => Define.TitanRole.RightArm;

    protected override bool IsLeftArm => false;
}
