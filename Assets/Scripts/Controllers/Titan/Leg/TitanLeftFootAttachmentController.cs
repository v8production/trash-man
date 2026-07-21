using UnityEngine;

public sealed class TitanLeftFootAttachmentController : FootAttachmentController
{
    protected override void Awake()
    {
        base.Awake();
        side = TitanBaseLegRoleController.LegSide.Left;
    }
}
