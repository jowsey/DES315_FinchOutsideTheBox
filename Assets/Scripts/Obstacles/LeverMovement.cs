using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class LeverMovement : NetworkBehaviour
{
    private Animator _animator;
    bool _forward; //True when going forward, false when going backwards

    private bool _triggerColliding;
    private bool _triggerCollidingLastTick;

    [Tooltip("Invoked when the lever starts moving down")]
    [SerializeField] private UnityEvent _onLeverActivate;
    [Tooltip("Invoked when the lever starts moving up")]
    [SerializeField] private UnityEvent _onLeverDeactivate;
    [Tooltip("Invoked when the lever has reached fully down")]
    [SerializeField] private UnityEvent _onLeverTargetPos;
    [Tooltip("Invoked when the lever has reached fully up (not triggered on first tick)")]
    [SerializeField] private UnityEvent _onLeverDefaultPos;


    private void Start()
    {
        _animator = GetComponentInParent<Animator>();
        _animator.SetFloat("AnimSpeed", 0.0f);
        _forward = true;
        _triggerColliding = false;
        _triggerCollidingLastTick = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (authority)
        {
            _triggerColliding = true;
        }
    }

    private void FixedUpdate()
    {
        if (authority)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log(_forward + " " + stateInfo.normalizedTime);
            if ((stateInfo.normalizedTime >= 0.99f) && _forward)
            {
                _onLeverTargetPos.Invoke();

                //Clamp to 1 and stop animation so it doesn't go past 1
                _animator.Play(stateInfo.fullPathHash, 0, 1.0f);
                _animator.SetFloat("AnimSpeed", 0.0f);
            }
            else if ((stateInfo.normalizedTime <= 0.01f) && !_forward)
            {
                _onLeverDefaultPos.Invoke();

                //Clamp to 0 and stop animation so it doesn't go past 0 into negatives
                _animator.Play(stateInfo.fullPathHash, 0, 0.0f);
                _animator.SetFloat("AnimSpeed", 0.0f);
            }

            //Check for changes in _triggerColliding state
            if (!_triggerCollidingLastTick && _triggerColliding)
            {
                //Trigger is now active
                _onLeverActivate.Invoke();
                _animator.SetFloat("AnimSpeed", 1.0f);
                _forward = true;
            }
            else if (_triggerCollidingLastTick && !_triggerColliding)
            {
                //Trigger is no longer active
                _onLeverDeactivate.Invoke();
                _animator.SetFloat("AnimSpeed", -1.0f);
                _forward = false;
            }

            //Reset
            _triggerCollidingLastTick = _triggerColliding;
            _triggerColliding = false;
        }
    }
}
