using System.Collections;
using UnityEngine;

public class GrolarController : BossController
{
    private Animator _animator;

    protected Define.GrolarAnimState _animState;

    public virtual Define.GrolarAnimState AnimState
    {
        get { return _animState; }
        set
        {
            if (Equals(_animState, value))
                return;
            _animState = value;

            _animator.CrossFade(_animState.ToString(), 0.1f);
        }
    }
    private BossStat _stat;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>(true);
        _stat = gameObject.GetComponent<BossStat>();
    }

}
