using UnityEngine;

public class JuiceRotator : MonoBehaviour
{
    public float stiffness = 50f; 
    public float damping = 5f; 

    private Vector3 angularVelocity;

    void Update()
    {
        Vector3 currentUp = transform.up;
        Vector3 targetUp = Vector3.up;

        Vector3 torque = Vector3.Cross(currentUp, targetUp) * stiffness;

        angularVelocity = angularVelocity * Mathf.Exp(-damping * Time.deltaTime);
        angularVelocity += torque * Time.deltaTime;

        transform.rotation = Quaternion.Euler(angularVelocity * Mathf.Rad2Deg) * transform.rotation;
    }
}
