using UnityEngine;

public class TankController : MonoBehaviour
{
    private const float LifetimeSeconds = 20f;
    private const int HpRecoverAmount = 20;
    private const int GaugeRecoverAmount = 50;

    private static readonly Vector3 HpTimerLocalOffset = new(0f, 0.1f, 0f);
    private static readonly Vector3 GaugeTimerLocalOffset = new(0f, 0.2f, 0f);

    private UI_Timer _timerUi;
    private float _remainingLifetime;
    private bool _isCollected;
    private TankType _tankType;

    private enum TankType
    {
        Hp,
        Gauge,
    }

    private void Awake()
    {
        _tankType = gameObject.name.Contains("Gauge") ? TankType.Gauge : TankType.Hp;
        AttachAuraCollisionRelays();
    }

    private void OnEnable()
    {
        _remainingLifetime = LifetimeSeconds;
        _isCollected = false;
    }

    private void OnDisable()
    {
        DestroyTimerUi();
    }

    private void Update()
    {
        _remainingLifetime -= Time.deltaTime;

        if (_remainingLifetime <= 0f)
            Despawn();
    }

    private void LateUpdate()
    {
        if (!_isCollected)
            UpdateTimerUi();
    }

    public void HandleAuraCollision(Collider other)
    {
        if (_isCollected)
            return;

        TitanStat titanStat = other.GetComponentInParent<TitanStat>();
        if (titanStat == null)
            return;

        if (_tankType == TankType.Hp)
            titanStat.Hp = Mathf.Min(titanStat.MaxHp, titanStat.Hp + HpRecoverAmount);
        else
            titanStat.RecoverGauge(GaugeRecoverAmount);

        _isCollected = true;
        Despawn();
    }

    private void AttachAuraCollisionRelays()
    {
        Collider[] auraColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < auraColliders.Length; i++)
        {
            Collider auraCollider = auraColliders[i];
            auraCollider.isTrigger = true;

            TankAuraCollisionRelay relay = auraCollider.GetComponent<TankAuraCollisionRelay>();
            if (relay == null)
                relay = auraCollider.gameObject.AddComponent<TankAuraCollisionRelay>();

            relay.SetOwner(this);
        }
    }

    private void UpdateTimerUi()
    {
        if (_timerUi == null)
            _timerUi = Managers.UI.CreateSceneUI<UI_Timer>(nameof(UI_Timer), 10);

        if (!_timerUi.SetWorldPosition(transform.TransformPoint(GetTimerLocalOffset())))
            return;

        _timerUi.SetFillAmount(1f - (_remainingLifetime / LifetimeSeconds));
    }

    private Vector3 GetTimerLocalOffset()
    {
        return _tankType == TankType.Hp ? HpTimerLocalOffset : GaugeTimerLocalOffset;
    }

    private void Despawn()
    {
        DestroyTimerUi();
        Managers.Resource.Destory(gameObject);
    }

    private void DestroyTimerUi()
    {
        if (_timerUi == null)
            return;

        _timerUi.Hide();
        Managers.Resource.Destory(_timerUi.gameObject);
        _timerUi = null;
    }
}

public class TankAuraCollisionRelay : MonoBehaviour
{
    private TankController _owner;

    public void SetOwner(TankController owner)
    {
        _owner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_owner != null)
            _owner.HandleAuraCollision(other);
    }

}
