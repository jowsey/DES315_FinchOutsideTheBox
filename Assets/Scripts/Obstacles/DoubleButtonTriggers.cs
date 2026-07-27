using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class DoubleButtonTriggers : NetworkBehaviour
{
    private static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    
    private Animator _animator;
    public bool Forward; //True when going forward, false when going backwards

    public bool _triggerColliding;
    private bool _triggerCollidingLastTick;

    public AK.Wwise.Event LeverDown;

    private void Start()
    {
        _animator = GetComponentInParent<Animator>();
        _animator.SetFloat(AnimSpeed, 0.0f);
        Forward = true;
        _triggerColliding = false;
        _triggerCollidingLastTick = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (authority)
        {
            if (other.gameObject.layer == 7)
            _triggerColliding = true;
        }
    }

    private void FixedUpdate()
    {
        if (authority)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            // Debug.Log(Forward + " " + stateInfo.normalizedTime);
            if ((stateInfo.normalizedTime >= 0.99f) && Forward)
            {
                //Clamp to 1 and stop animation so it doesn't go past 1
                _animator.Play(stateInfo.fullPathHash, 0, 1.0f);
                _animator.SetFloat(AnimSpeed, 0.0f);
            }
            else if ((stateInfo.normalizedTime <= 0.01f) && !Forward)
            {
                //Clamp to 0 and stop animation so it doesn't go past 0 into negatives
                _animator.Play(stateInfo.fullPathHash, 0, 0.0f);
                _animator.SetFloat(AnimSpeed, 0.0f);
            }

            //Check for changes in _triggerColliding state
            if (!_triggerCollidingLastTick && _triggerColliding)
            {
                _animator.SetFloat(AnimSpeed, 1.0f);
                Forward = true;
            }
            else if (_triggerCollidingLastTick && !_triggerColliding)
            {
                _animator.SetFloat(AnimSpeed, -1.0f);
                Forward = false;
            }

            //Reset
            _triggerCollidingLastTick = _triggerColliding;
            _triggerColliding = false;
        }
    }
}
