using Mirror;
using UnityEngine;
using UnityEngine.Splines;

public class MovingPlatform : MonoBehaviour
{
    private Rigidbody _rb;
    private SplineContainer _container;

    [SerializeField] private float _duration;

    private void Awake()
    {
        _rb = GetComponentInChildren<Rigidbody>();
        _container = GetComponentInChildren<SplineContainer>();
    }

    private void FixedUpdate()
    {
        var t = (float)(NetworkTime.time % _duration / _duration);
        Vector3 localPos = _container.Splines[0].EvaluatePosition(t);
        Vector3 worldPos = _container.transform.TransformPoint(localPos);
        _rb.MovePosition(worldPos);
    }
}
