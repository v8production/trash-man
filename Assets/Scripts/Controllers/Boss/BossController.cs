using UnityEngine;

[RequireComponent(typeof(BossStat))]
public class BossController : MonoBehaviour
{
    protected BossStat _stat;
    private Collider[] _hitColliders;

    public BossStat Stat => _stat;

    protected virtual void Awake()
    {
        _stat = gameObject.GetorAddComponent<BossStat>();
        _hitColliders = GetComponentsInChildren<Collider>();
    }

    public void ReceiveAttack(Stat attacker)
    {
        _stat.OnAttacked(attacker);
    }

    public bool IsWithinHitRadius(Vector3 origin, float radius)
    {
        float sqrRadius = radius * radius;
        if (_hitColliders != null)
        {
            for (int i = 0; i < _hitColliders.Length; i++)
            {
                Collider hitCollider = _hitColliders[i];
                if (hitCollider == null || !hitCollider.enabled)
                    continue;

                Vector3 closest = hitCollider.ClosestPoint(origin);
                if ((closest - origin).sqrMagnitude <= sqrRadius)
                    return true;
            }
        }

        return (transform.position - origin).sqrMagnitude <= sqrRadius;
    }
}
