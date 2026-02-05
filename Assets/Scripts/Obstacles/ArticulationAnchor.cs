using UnityEngine;

public class ArticulationAnchor : MonoBehaviour
{
    private ArticulationBody ab;
    private Vector3 targetPosition;
    public float strength = 5000f;
    public float damping = 100f;

    void Start()
    {
        ab = GetComponent<ArticulationBody>();
        targetPosition = transform.position;
    }

    void FixedUpdate()
    {
        Vector3 displacement = targetPosition - transform.position;
        Vector3 force = displacement * strength - ab.linearVelocity * damping;
        ab.AddForce(force);
    }
}