using UnityEngine;

public sealed class TitanLegAnchorResolver : MonoBehaviour
{
    private const string LogPrefix = "[TitanLegAnchor]";

    private enum AnchorMode
    {
        Free,
        LeftAnchored,
        RightAnchored,
        Locked,
    }

    [Header("Attachments")]
    [SerializeField] private FootAttachmentController leftFootAttachment;
    [SerializeField] private FootAttachmentController rightFootAttachment;

    [Header("Inverse Movement")]
    [SerializeField] private float inverseYawScale = 1f;
    [SerializeField] private float inverseRollScale = 0.75f;
    [SerializeField] private float inverseMouseDeltaTranslationScale = 0.0025f;

    [Header("Anchor Stabilization")]
    [SerializeField] private float singleFootYawCorrectionScale = 1f;
    [SerializeField] private bool zeroBodyVelocityWhenLocked = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        StabilizeAnchors();
        LogAttachmentTickState();
    }

    public bool HasAnyAttachedFoot()
    {
        return GetAnchorMode() != AnchorMode.Free;
    }

    public void UpdateDetachState(TitanBaseLegRoleController.LegSide side, bool detachHeld)
    {
        FootAttachmentController controller = GetAttachment(side);
        InputDebug.Log($"{LogPrefix} UpdateDetachState side={side} controller={(controller != null)} attached={controller != null && controller.IsAttached} detachHeld={detachHeld}");
        controller?.SetDetachHeld(detachHeld);
    }

    public bool TryApplyAnchoredMovement(TitanBaseLegRoleController.LegSide side, in TitanLegInputCommand command, in TitanLegControlState currentState, float deltaTime)
    {
        ResolveReferences();

        AnchorMode mode = GetAnchorMode();
        InputDebug.Log($"{LogPrefix} TryApplyAnchoredMovement side={side} mode={mode} currentYaw={currentState.HipYaw:F2} targetYaw={command.TargetHipYaw:F2} currentRoll={currentState.HipRoll:F2} targetRoll={command.TargetHipRoll:F2}");
        if (mode == AnchorMode.Locked)
        {
            InputDebug.Log($"{LogPrefix} side={side} blocked: both feet attached.");
            return true;
        }

        bool leftAnchored = mode == AnchorMode.LeftAnchored;
        bool rightAnchored = mode == AnchorMode.RightAnchored;
        if ((side == TitanBaseLegRoleController.LegSide.Left && !leftAnchored) || (side == TitanBaseLegRoleController.LegSide.Right && !rightAnchored))
        {
            InputDebug.Log($"{LogPrefix} side={side} free movement: opposite foot owns anchor.");
            return false;
        }

        Transform movableRoot = Managers.TitanRig.MovementRoot;
        FootAttachmentController anchor = GetAttachment(side);
        if (movableRoot == null || anchor == null || !anchor.IsAttached)
        {
            InputDebug.LogWarning($"{LogPrefix} side={side} cannot anchor. movableRoot={(movableRoot != null)} attachment={(anchor != null)} attached={(anchor != null && anchor.IsAttached)}");
            return false;
        }

        float yawDelta = Mathf.DeltaAngle(currentState.HipYaw, command.TargetHipYaw);
        float rollDelta = command.TargetHipRoll - currentState.HipRoll;
        float inverseYaw = -yawDelta * inverseYawScale;
        float inverseRoll = -rollDelta * inverseRollScale;

        Quaternion yawRotation = Quaternion.AngleAxis(inverseYaw, Vector3.up);
        Quaternion rollRotation = Quaternion.AngleAxis(inverseRoll, movableRoot.forward);
        Quaternion combinedRotation = yawRotation * rollRotation;

        Vector3 pivot = anchor.AttachedWorldPosition;
        Vector3 rotatedOffset = combinedRotation * (movableRoot.position - pivot);
        Vector3 translatedOffset = Vector3.zero;
        if (command.MouseDelta.sqrMagnitude > 0.000001f)
        {
            Vector3 planarRight = Vector3.ProjectOnPlane(movableRoot.right, Vector3.up).normalized;
            Vector3 planarForward = Vector3.ProjectOnPlane(movableRoot.forward, Vector3.up).normalized;
            translatedOffset = (planarRight * (-command.MouseDelta.x) + planarForward * (-command.MouseDelta.y))
                               * inverseMouseDeltaTranslationScale * deltaTime;
        }

        Vector3 nextPosition = pivot + rotatedOffset + translatedOffset;
        Quaternion nextRotation = combinedRotation * movableRoot.rotation;
        InputDebug.Log($"{LogPrefix} side={side} anchored move pivot={pivot} inverseYaw={inverseYaw:F2} inverseRoll={inverseRoll:F2} translatedOffset={translatedOffset}");
        Managers.TitanRig.ApplyMovementRootPose(nextPosition, nextRotation, zeroVelocities: false);
        StabilizeAnchors();
        return true;
    }

    private void StabilizeAnchors()
    {
        if (!Managers.TitanRig.EnsureReady())
        {
            return;
        }

        AnchorMode mode = GetAnchorMode();
        InputDebug.Log($"{LogPrefix} StabilizeAnchors mode={mode} leftAttached={(leftFootAttachment != null && leftFootAttachment.IsAttached)} rightAttached={(rightFootAttachment != null && rightFootAttachment.IsAttached)}");
        switch (mode)
        {
            case AnchorMode.LeftAnchored:
                ApplySingleAnchorLock(leftFootAttachment, zeroBodyVelocityWhenLocked: false);
                break;
            case AnchorMode.RightAnchored:
                ApplySingleAnchorLock(rightFootAttachment, zeroBodyVelocityWhenLocked: false);
                break;
            case AnchorMode.Locked:
                ApplyDualAnchorLock(zeroBodyVelocityWhenLocked);
                break;
        }
    }

    private void ApplySingleAnchorLock(FootAttachmentController attachment, bool zeroBodyVelocityWhenLocked)
    {
        Transform movableRoot = Managers.TitanRig.MovementRoot;
        if (movableRoot == null || attachment == null || !attachment.IsAttached || attachment.FootTransform == null)
        {
            return;
        }

        Vector3 desiredPivot = attachment.AttachedWorldPosition;
        Vector3 currentPivot = attachment.GetCurrentContactPoint();
        Quaternion nextRotation = movableRoot.rotation;

        Vector3 currentForward = Vector3.ProjectOnPlane(attachment.FootTransform.forward, Vector3.up);
        Vector3 desiredForward = Vector3.ProjectOnPlane(attachment.AttachedWorldRotation * Vector3.forward, Vector3.up);
        if (currentForward.sqrMagnitude > 0.0001f && desiredForward.sqrMagnitude > 0.0001f)
        {
            float yawDelta = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up) * singleFootYawCorrectionScale;
            if (Mathf.Abs(yawDelta) > 0.001f)
            {
                Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, Vector3.up);
                Vector3 rotatedPosition = desiredPivot + (yawRotation * (movableRoot.position - desiredPivot));
                nextRotation = yawRotation * nextRotation;
                Managers.TitanRig.ApplyMovementRootPose(rotatedPosition, nextRotation, zeroBodyVelocityWhenLocked);
                currentPivot = attachment.GetCurrentContactPoint();
            }
        }

        Vector3 translationDelta = desiredPivot - currentPivot;
        if (translationDelta.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        InputDebug.Log($"{LogPrefix} SingleAnchor correction pivot={desiredPivot} current={currentPivot} translationDelta={translationDelta}");
        Managers.TitanRig.ApplyMovementRootPose(movableRoot.position + translationDelta, movableRoot.rotation, zeroBodyVelocityWhenLocked);
    }

    private void ApplyDualAnchorLock(bool zeroVelocities)
    {
        Transform movableRoot = Managers.TitanRig.MovementRoot;
        if (movableRoot == null || leftFootAttachment == null || rightFootAttachment == null)
        {
            return;
        }

        if (!leftFootAttachment.IsAttached || !rightFootAttachment.IsAttached)
        {
            return;
        }

        Vector3 leftCurrent = leftFootAttachment.GetCurrentContactPoint();
        Vector3 rightCurrent = rightFootAttachment.GetCurrentContactPoint();
        Vector3 leftDesired = leftFootAttachment.AttachedWorldPosition;
        Vector3 rightDesired = rightFootAttachment.AttachedWorldPosition;

        Vector3 currentMid = (leftCurrent + rightCurrent) * 0.5f;
        Vector3 desiredMid = (leftDesired + rightDesired) * 0.5f;
        Vector3 currentSpan = Vector3.ProjectOnPlane(rightCurrent - leftCurrent, Vector3.up);
        Vector3 desiredSpan = Vector3.ProjectOnPlane(rightDesired - leftDesired, Vector3.up);

        Vector3 nextPosition = movableRoot.position;
        Quaternion nextRotation = movableRoot.rotation;

        if (currentSpan.sqrMagnitude > 0.0001f && desiredSpan.sqrMagnitude > 0.0001f)
        {
            float yawDelta = Vector3.SignedAngle(currentSpan, desiredSpan, Vector3.up);
            if (Mathf.Abs(yawDelta) > 0.001f)
            {
                Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, Vector3.up);
                nextPosition = currentMid + (yawRotation * (nextPosition - currentMid));
                nextRotation = yawRotation * nextRotation;
            }
        }

        nextPosition += desiredMid - currentMid;
        InputDebug.Log($"{LogPrefix} DualAnchor correction currentMid={currentMid} desiredMid={desiredMid} nextPosition={nextPosition}");
        Managers.TitanRig.ApplyMovementRootPose(nextPosition, nextRotation, zeroVelocities);
    }

    private AnchorMode GetAnchorMode()
    {
        bool leftAttached = leftFootAttachment != null && leftFootAttachment.IsAttached;
        bool rightAttached = rightFootAttachment != null && rightFootAttachment.IsAttached;

        if (leftAttached && rightAttached)
        {
            return AnchorMode.Locked;
        }

        if (leftAttached)
        {
            return AnchorMode.LeftAnchored;
        }

        if (rightAttached)
        {
            return AnchorMode.RightAnchored;
        }

        return AnchorMode.Free;
    }

    private FootAttachmentController GetAttachment(TitanBaseLegRoleController.LegSide side)
    {
        return side == TitanBaseLegRoleController.LegSide.Left ? leftFootAttachment : rightFootAttachment;
    }

    private void ResolveReferences()
    {
        if (leftFootAttachment == null || rightFootAttachment == null)
        {
            FootAttachmentController[] attachments = GetComponents<FootAttachmentController>();
            for (int i = 0; i < attachments.Length; i++)
            {
                FootAttachmentController attachment = attachments[i];
                if (attachment == null)
                {
                    continue;
                }

                if (attachment.Side == TitanBaseLegRoleController.LegSide.Left)
                {
                    leftFootAttachment ??= attachment;
                    continue;
                }

                rightFootAttachment ??= attachment;
            }
        }
    }

    private void LogAttachmentTickState()
    {
        if (!InputDebug.Enabled)
        {
            return;
        }

        FootAttachmentController left = leftFootAttachment;
        FootAttachmentController right = rightFootAttachment;
        InputDebug.Log(
            $"{LogPrefix} Tick mode={GetAnchorMode()} " +
            $"leftAttached={(left != null && left.IsAttached)} leftDetached={(left == null || !left.IsAttached)} leftDetachHeld={(left != null && left.DetachHeld)} leftCollider={(left != null && left.AttachedCollider != null ? left.AttachedCollider.name : "<none>")} leftContact={(left != null ? left.GetCurrentContactPoint() : Vector3.zero)} " +
            $"rightAttached={(right != null && right.IsAttached)} rightDetached={(right == null || !right.IsAttached)} rightDetachHeld={(right != null && right.DetachHeld)} rightCollider={(right != null && right.AttachedCollider != null ? right.AttachedCollider.name : "<none>")} rightContact={(right != null ? right.GetCurrentContactPoint() : Vector3.zero)}");
    }
}
