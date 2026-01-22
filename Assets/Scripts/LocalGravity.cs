using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LocalGravity : MonoBehaviour
{
    [Tooltip("Direction of local gravity in world space")]
    public Vector3 Direction = Vector3.down;

    [Tooltip("Strength of local gravity in ms^-2")]
    public float Strength = 9.81f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        _rb.AddForce(Direction.normalized * Strength, ForceMode.Acceleration);
    }
}