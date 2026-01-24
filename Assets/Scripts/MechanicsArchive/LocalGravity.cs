using System;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LocalGravity : MonoBehaviour
{
    [Tooltip("Default direction of local gravity in world space when not affected by a surface")]
    public Vector3 DefaultDirection = Vector3.down;
    
    [Tooltip("Strength of local gravity")]
    [SuffixLabel("m/s^2")] public float Strength = 9.81f;

    private Rigidbody _rb;
    private Vector3 _direction;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _rb.useGravity = false;
        _direction = DefaultDirection;
    }

    private void FixedUpdate()
    {
        _rb.AddForce(_direction.normalized * Strength, ForceMode.Acceleration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out GravitySurface surface))
        {
            switch (surface.Type)
            {
                case GravitySurface.SurfaceType.ConstantLocal:
                    _direction = surface.transform.TransformDirection(surface.ConstantDirection);
                    break;
                case GravitySurface.SurfaceType.MatchSurfaceNormal:
                    // todo
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out GravitySurface surface))
        {
            _direction = DefaultDirection;
        }
    }
}