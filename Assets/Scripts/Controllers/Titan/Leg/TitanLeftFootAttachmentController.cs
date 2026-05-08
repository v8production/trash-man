using UnityEngine;

public sealed class TitanLeftFootAttachmentController : FootAttachmentController
{
    private void Awake()
    {
        side = TitanBaseLegRoleController.LegSide.Left;
    }
}
