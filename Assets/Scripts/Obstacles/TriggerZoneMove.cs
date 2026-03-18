using UnityEngine;
using UnityEngine.Events;

public class TriggerZoneMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    private bool _triggerColliding;

    [Tooltip("Invoked when the zone is entered")]
    [SerializeField] private UnityEvent _triggerZoneEntered;


    private void Start()
    {
        _triggerColliding = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        _triggerColliding = true;
    }

    private void FixedUpdate()
    {
        if (_triggerColliding)
        {
            _triggerZoneEntered.Invoke();
        }
    }
}
