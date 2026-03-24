using UnityEngine;
using UnityEngine.Events;

public class TriggerZoneMovement : MonoBehaviour
{
    [Tooltip("Triggerzone Start Event")]
    [SerializeField] private UnityEvent _triggerZoneEntered;
    [SerializeField] private UnityEvent _triggerZoneExited;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") || (other.gameObject.layer == LayerMask.NameToLayer("Cart")) || (other.gameObject.layer == LayerMask.NameToLayer("Interactable")))
        {
            _triggerZoneEntered.Invoke();
        }
    }

    private void OnTriggerExited(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") || (other.gameObject.layer == LayerMask.NameToLayer("Cart")) || (other.gameObject.layer == LayerMask.NameToLayer("Interactable")))
        {
            _triggerZoneExited.Invoke();
        }
    }
}
