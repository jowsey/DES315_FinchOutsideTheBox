using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
    using Sirenix.Utilities.Editor;
#endif

public class CenterMassHelper : Mirror.NetworkBehaviour
{
    [SerializeField] private Vector3 centerOfMassOffset = Vector3.zero;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Set CoM to current CoM + offset
        _rb.centerOfMass = _rb.centerOfMass + centerOfMassOffset;
    }
}
