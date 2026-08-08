using Mirror;
using UnityEngine;
using UnityEngine.Events;

public class VehicleLever : NetworkBehaviour
{
    private static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    
    public bool Forward; //True when going forward, false when going backwards

    private bool _bothTriggers = false;
    private bool _triggerCollidingLastTick;

    [Tooltip("Invoked when the lever starts moving down")]
    [SerializeField] private UnityEvent _onLeverActivate;
    [Tooltip("Invoked when the lever starts moving up")]
    [SerializeField] private UnityEvent _onLeverDeactivate;

    [SerializeField] private GameObject triggerA;
    [SerializeField] private GameObject triggerB;

    public AK.Wwise.Event LeverDown;

    private void Start()
    {
        Forward = true;
    }

    private void Update()
    {
        _bothTriggers = (triggerA.GetComponent<DoubleButtonTriggers>()._triggered) && (triggerB.GetComponent<DoubleButtonTriggers>()._triggered);
    }

    private void FixedUpdate()
    {
        if (authority)
        {
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

    public void ButtonDown()
    {
        LeverDown.Post(gameObject);
    }
}
