using UnityEngine;
using Sirenix.OdinInspector;

public class SpinningCylinder : MonoBehaviour
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
        Quaternion turnOffset = Quaternion.Euler(0, 0, _spinSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * turnOffset);
    }
}
