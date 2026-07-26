using Unity.VisualScripting;
using UnityEngine;

public abstract class TitanBaseController : MonoBehaviour
{
    protected TitanMovementFeedbackController MovementFeedback { get; private set; }

    protected virtual void Awake()
    {
        Managers.TitanRig.EnsureBoundTo(gameObject);
        MovementFeedback = gameObject.GetOrAddComponent<TitanMovementFeedbackController>();
    }

    public abstract Define.TitanRole Role { get; }
    public abstract void TickRoleInput(in TitanAggregatedInput input, float deltaTime);
}
