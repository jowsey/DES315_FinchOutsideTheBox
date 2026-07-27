using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class VehicleLever : NetworkBehaviour
{
    private static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    
    private Animator _animator;
    public bool Forward; //True when going forward, false when going backwards

    private bool _bothTriggers = false;
    private bool _triggerCollidingLastTick;

    [Tooltip("Invoked when the lever starts moving down")]
    [SerializeField] private UnityEvent _onLeverActivate;
    [Tooltip("Invoked when the lever starts moving up")]
    [SerializeField] private UnityEvent _onLeverDeactivate;
    [Tooltip("Invoked when the lever has reached fully down")]
    [SerializeField] private UnityEvent _onLeverTargetPos;
    [Tooltip("Invoked when the lever has reached fully up (not triggered on first tick)")]
    [SerializeField] private UnityEvent _onLeverDefaultPos;

    [SerializeField] private GameObject triggerA;
    [SerializeField] private GameObject triggerB;

    public AK.Wwise.Event LeverDown;

    private void Start()
    {
        _animator = GetComponentInParent<Animator>();
        _animator.SetFloat(AnimSpeed, 0.0f);
        Forward = true;
    }

    private void Update()
    {
        _bothTriggers = (triggerA.GetComponent<DoubleButtonTriggers>()._triggerColliding) && (triggerB.GetComponent<DoubleButtonTriggers>()._triggerColliding);
    }

    private void FixedUpdate()
    {
        if (authority)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            // Debug.Log(Forward + " " + stateInfo.normalizedTime);
            if ((stateInfo.normalizedTime >= 0.99f) && Forward)
            {
                _onLeverTargetPos.Invoke();
            }
            else if ((stateInfo.normalizedTime <= 0.01f) && !Forward)
            {
                _onLeverDefaultPos.Invoke();
            }

            //Check for changes in _triggerColliding state
            if (!_triggerCollidingLastTick && _bothTriggers)
            {
                //Trigger is now active
                _onLeverActivate.Invoke();
                Forward = true;
            }
            else if (_triggerCollidingLastTick && !_bothTriggers)
            {
                //Trigger is no longer active
                _onLeverDeactivate.Invoke();
                Forward = false;
            }

            //Reset
            _triggerCollidingLastTick = _bothTriggers;
            _bothTriggers = false;
        }
    }
}
