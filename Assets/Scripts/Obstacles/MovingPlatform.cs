using Mirror;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class MovingPlatform : NetworkBehaviour
{
    private Rigidbody _rb;
    private SplineContainer _container;

    [SyncVar]
    private float _t = 0;

    [SerializeField] private float _duration;


    void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>();
        _container = GetComponentInChildren<SplineContainer>();
    }


    void FixedUpdate()
    {
        Vector3 localPos = _container.Splines[0].EvaluatePosition(_t);
        Vector3 worldPos = _container.transform.TransformPoint(localPos);
        _rb.MovePosition(worldPos);
        _t = (_t + Time.fixedDeltaTime / _duration) % 1.0f;
    }
}
