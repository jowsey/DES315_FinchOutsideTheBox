using Mirror;
using UnityEngine;
using Sirenix.OdinInspector;

public class SpinningCylinder : NetworkBehaviour
{
    [SuffixLabel("deg/s")]
    [SerializeField] private float _spinSpeed;

    void Update()
    {
        if (!isServer) { return; }

        transform.Rotate(0, _spinSpeed * Time.deltaTime, 0);
    }
}
