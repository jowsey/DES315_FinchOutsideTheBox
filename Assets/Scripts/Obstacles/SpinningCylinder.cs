using Mirror;
using UnityEngine;
using Sirenix.OdinInspector;

public class SpinningCylinder : NetworkBehaviour
{
    [SuffixLabel("deg/s")]
    [SerializeField] private float _spinSpeed;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Quaternion turnOffset = Quaternion.Euler(0, _spinSpeed * Time.fixedDeltaTime, 0);
        rb.MoveRotation(rb.rotation * turnOffset);
    }
}
