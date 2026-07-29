using UnityEngine;

public class Interactable : MonoBehaviour
{
    //The transform to be put into CrosshairDetection._hitTransform when this object is being looked at
    [field: SerializeField] public Transform InteractedTransform { get; private set; }

    private void OnValidate()
    {
        if (!InteractedTransform)
        {
            var parentInteractable = transform.parent ? transform.parent.GetComponentInParent<Interactable>() : GetComponent<Interactable>();
            InteractedTransform = parentInteractable ? parentInteractable.InteractedTransform : transform;
        }
    }
}