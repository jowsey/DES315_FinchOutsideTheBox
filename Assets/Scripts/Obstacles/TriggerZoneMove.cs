using UnityEngine;
using UnityEngine.Events;

public class TriggerZoneMovement : MonoBehaviour
{
    [Tooltip("Triggerzone Start Event")]
    [SerializeField] private UnityEvent _triggerZoneEntered;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") || (other.gameObject.layer == LayerMask.NameToLayer("Cart")))
        {
            _triggerZoneEntered.Invoke();
        }
    }
}
