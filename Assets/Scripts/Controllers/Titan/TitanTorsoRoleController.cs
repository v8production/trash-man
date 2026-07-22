using Unity.VisualScripting;
using UnityEngine;

public class TitanTorsoRoleController : TitanBaseController
{
    private const float DrillGaugeCost = 10f;
    private const float ShieldGaugeCost = 100f;
    private const float DrillActiveDurationSeconds = 1f;
    private const float ShieldActiveDurationSeconds = 1f;
    private const float ClawLaunchGaugeCost = 10f;
    private const float DrillHitRadius = 0.3f;
    private const float DrillHitIntervalSeconds = 0.25f;
    private const float TorsoYawSpeed = 90f;

    private TitanController titanController;
    private TitanStat titanStat;
    private float nextDrillHitTime;
    private float drillActiveTimeRemaining;
    private float shieldActiveTimeRemaining;
    private uint lastHandledDrillPressCounter;
    private uint lastHandledShieldPressCounter;
    private uint lastHandledClawPressCounter;

    public override Define.TitanRole Role => Define.TitanRole.Torso;

    protected override void Awake()
    {
        base.Awake();
        titanController = gameObject.GetOrAddComponent<TitanController>();
        titanStat = gameObject.GetOrAddComponent<TitanStat>();
    }

    public override void TickRoleInput(in TitanAggregatedInput input, float deltaTime)
    {
        UpdateTorsoFacing(input, deltaTime);
        UpdateSpecialAbilities(input, deltaTime);
    }

    private void UpdateTorsoFacing(in TitanAggregatedInput input, float deltaTime)
    {
        if (Mathf.Approximately(input.TorsoYawInput, 0f))
            return;

        Managers.TitanRig.SetWaistYaw(Managers.TitanRig.WaistYaw + input.TorsoYawInput * TorsoYawSpeed * deltaTime);
        Managers.TitanRig.ApplyTorsoPose();
    }

    private void UpdateSpecialAbilities(in TitanAggregatedInput input, float deltaTime)
    {
        if (TryConsumePress(input.TorsoDrillPressCounter, ref lastHandledDrillPressCounter) && titanStat.TrySpendGauge(DrillGaugeCost))
            drillActiveTimeRemaining = DrillActiveDurationSeconds;

        if (TryConsumePress(input.TorsoShieldPressCounter, ref lastHandledShieldPressCounter) && titanStat.TrySpendGauge(ShieldGaugeCost))
            shieldActiveTimeRemaining = ShieldActiveDurationSeconds;

        if (drillActiveTimeRemaining > 0f)
            drillActiveTimeRemaining = Mathf.Max(0f, drillActiveTimeRemaining - deltaTime);

        if (shieldActiveTimeRemaining > 0f)
            shieldActiveTimeRemaining = Mathf.Max(0f, shieldActiveTimeRemaining - deltaTime);

        bool drillActive = drillActiveTimeRemaining > 0f;
        bool shieldActive = input.TorsoShieldHeld || shieldActiveTimeRemaining > 0f;

        titanController.LeftDrillActive = drillActive;
        titanController.Guard = shieldActive;

        if (drillActive)
            TryApplyDrillAttack();

        if (TryConsumePress(input.TorsoClawPressCounter, ref lastHandledClawPressCounter) && titanController.CanLaunchRightClaw && titanStat.TrySpendGauge(ClawLaunchGaugeCost))
            titanController.NotifyRightClawLaunched();
    }

    private static bool TryConsumePress(uint pressCounter, ref uint lastHandledPressCounter)
    {
        if (pressCounter == 0 || pressCounter == lastHandledPressCounter)
            return false;

        lastHandledPressCounter = pressCounter;
        return true;
    }

    private void TryApplyDrillAttack()
    {
        if (Time.time < nextDrillHitTime)
            return;

        Vector3 drillPosition = ResolveDrillPosition();
        BossController[] bosses = Object.FindObjectsByType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController boss = bosses[i];
            if (boss == null || !boss.IsWithinHitRadius(drillPosition, DrillHitRadius))
                continue;

            boss.ReceiveAttack(titanStat);
            nextDrillHitTime = Time.time + DrillHitIntervalSeconds;
            return;
        }
    }

    private static Vector3 ResolveDrillPosition()
    {
        Transform anchor = Managers.TitanRig.LeftElbow;
        if (anchor != null)
            return anchor.position;

        Transform movementRoot = Managers.TitanRig.MovementRoot;
        return movementRoot.position + movementRoot.forward * 0.5f;
    }
}
