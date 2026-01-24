using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Cart : MonoBehaviour
{
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
}