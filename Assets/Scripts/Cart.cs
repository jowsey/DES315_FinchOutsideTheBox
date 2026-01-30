using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Cart : MonoBehaviour
{
    public Rigidbody Rb { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
    }
}