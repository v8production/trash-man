using UnityEngine;

public abstract class TitanBaseController : MonoBehaviour
{
    protected virtual void Awake()
    {
        Managers.TitanRig.EnsureBoundTo(gameObject);
    }

    public abstract Define.TitanRole Role { get; }
    public abstract void TickRoleInput(in TitanAggregatedInput input, float deltaTime);
}
