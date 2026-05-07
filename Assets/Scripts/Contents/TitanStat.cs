using UnityEngine;

public class TitanStat : Stat
{
    private const float GaugeRecoverPerSecond = 50f;

    [SerializeField]
    protected int _gauge;
    [SerializeField]
    protected int _maxGauge;

    private float _gaugeValue;

    public int Gauge { get { return _gauge; } set { SetGauge(value); } }
    public int MaxGauge { get { return _maxGauge; } set { _maxGauge = Mathf.Max(0, value); SetGauge(_gaugeValue); } }

    void Start()
    {
        _hp = 100;
        _maxHp = 100;
        _attack = 10;
        _maxGauge = 100;
        SetGauge(100f);
    }

    public void RecoverGauge(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        SetGauge(_gaugeValue + (GaugeRecoverPerSecond * deltaTime));
    }

    public bool TrySpendGauge(float amount)
    {
        if (amount <= 0f)
            return true;

        if (_gaugeValue < amount)
            return false;

        SetGauge(_gaugeValue - amount);
        return true;
    }

    private void SetGauge(float value)
    {
        _gaugeValue = Mathf.Clamp(value, 0f, _maxGauge);
        _gauge = Mathf.RoundToInt(_gaugeValue);
    }

    protected override void OnDead(Stat attacker)
    {
        Debug.Log("Player Dead");
        base.OnDead(attacker);
    }
}
