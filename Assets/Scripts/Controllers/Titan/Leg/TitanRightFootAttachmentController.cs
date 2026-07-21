using UnityEngine;

public sealed class TitanRightFootAttachmentController : FootAttachmentController
{
    protected override void Awake()
    {
        base.Awake();
        side = TitanBaseLegRoleController.LegSide.Right;
    }
}
