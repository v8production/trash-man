public class TitanLeftArmRoleController : TitanBaseArmRoleController
{
    public override Define.TitanRole Role => Define.TitanRole.LeftArm;

    protected override bool IsLeftArm => true;
}
