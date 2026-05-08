using UnityEngine;

public sealed class TitanRightFootAttachmentController : FootAttachmentController
{
    private void Awake()
    {
        side = TitanBaseLegRoleController.LegSide.Right;
    }
}
