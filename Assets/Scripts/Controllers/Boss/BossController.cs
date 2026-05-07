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
        if (_hitColliders == null || _hitColliders.Length == 0)
        {
            CapsuleCollider hitCollider = gameObject.AddComponent<CapsuleCollider>();
            hitCollider.center = new Vector3(0f, 0.9f, 0f);
            hitCollider.radius = 0.6f;
            hitCollider.height = 1.8f;
            _hitColliders = new Collider[] { hitCollider };
        }
    }

    public void ReceiveAttack(Stat attacker)
    {
        _stat.OnAttacked(attacker);
    }

    public void ReceiveClawAttach(Stat attacker)
    {
        _stat.OnAttach(attacker);
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
