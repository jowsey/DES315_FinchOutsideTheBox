using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HoverboardController : MonoBehaviour
{
    [Header("Components")] private Rigidbody _rb;

    [SerializeField] private Transform _boardMesh;

    [Header("Input")] [SerializeField] private InputActionReference _moveAction;

    [Header("Camera")] [SerializeField] [Required]
    private CinemachineCamera _camera;

    [SerializeField] [Required] private CinemachineOrbitalFollow _cameraFollow;

    [SerializeField] private float _cameraZoomOutFactor = 1.2f;

    [SerializeField] private float _cameraZoomOutMaxSpeed = 20f;

    private float _cameraDefaultRadius;

    [Header("Floating")] [SerializeField] [Tooltip("A set of local points that will be sampled for pushing the board upwards")]
    private List<Vector3> _pushPoints = new();

    [SerializeField] [Tooltip("Amount of force applied when pushing upwards by each point")]
    private float _pushForce = 100f;

    [SerializeField] [Tooltip("The height above ground the points will try and reach")]
    private float _pushDistance = 0.5f;

    [SerializeField] [Tooltip("Exponential factor applied to the distance. Increasing this reduces bobbing up and down, but reduces the height at which it stabilises")]
    private float _pushExponent = 3.0f;

    [SerializeField] [Tooltip("Amplitude of push force sine wave")]
    private float _sinForce = 0.15f;

    [SerializeField] [Tooltip("Wavelength of push force sine wave")]
    private float _sinSpeed = 2f;

    [Header("Movement")]
    [SerializeField]
    [Tooltip("Amount of forward force applied by movement. Max speed and acceleration are both computed as a mix of this value and the linear damping value in the Rigidbody.")]
    private float _moveForce = 150f;

    [SerializeField] [Tooltip("Speed factor at which hoverboard rotates to face movement direction")]
    private float _rotationSpeed = 3f;

    [SerializeField] [Tooltip("Maximum amount of sideways leaning in degrees")]
    private float _maxLeanAngle = 15f;

    [SerializeField] [Tooltip("Rotation speed in degrees/sec at which the board will fully lean")]
    private float _rotSpeedForMaxLean = 90f;

    [SerializeField] [Tooltip("Speed of lean angle smoothing")]
    private float _leanSpeed = 3f;

    [SerializeField] [Tooltip("Maximum amount of backwards leaning in degrees")]
    private float _maxBackwardLeanAngle = 5f;

    [SerializeField] [Tooltip("Speed in meters/sec at which the board will fully lean backwards")]
    private float _speedForMaxBackwardLean = 15f;

    [SerializeField] [Tooltip("If true, the board will rotate to face the movement direction when no input is held")]
    private bool _rotateTowardsForward = true;

    private float _angularVelocityY;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _cameraDefaultRadius = _cameraFollow.Radius;
    }

    private void FixedUpdate()
    {
        var sinFactor = 1 + Mathf.Sin(Time.time * _sinSpeed) * _sinForce;

        foreach (var point in _pushPoints)
        {
            var worldPoint = transform.TransformPoint(point);
            var ray = new Ray(worldPoint, -transform.up);
            if (!Physics.Raycast(ray, out var hitInfo, _pushDistance)) continue;

            var factor = 1f - (hitInfo.distance / _pushDistance);
            var expFactor = Mathf.Pow(factor, _pushExponent);
            _rb.AddForceAtPosition(transform.up * (_pushForce * expFactor * sinFactor), worldPoint);
        }

        // Movement force
        var moveInput = _moveAction.action.ReadValue<Vector2>();

        var orientation = _camera.State.GetFinalOrientation();
        var cameraForward = Vector3.Scale(orientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;
        var cameraRight = orientation * Vector3.right;

        var inputDir = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
        _rb.AddForce(inputDir * _moveForce);

        // Rotation
        var flatVel = Vector3.Scale(_rb.linearVelocity, new Vector3(1, 0, 1));
        var prevY = _rb.rotation.eulerAngles.y;

        // Point towards input direction, or velocity direction if no input and moving
        var pointDir = inputDir != Vector3.zero
            ? inputDir
            : _rotateTowardsForward && flatVel.magnitude > 0.5f
                ? flatVel.normalized
                : Vector3.zero;

        if (pointDir != Vector3.zero)
        {
            var targetRotation = Quaternion.LookRotation(pointDir, Vector3.up);
            var newRot = Quaternion.Slerp(_rb.rotation, targetRotation, Time.fixedDeltaTime * _rotationSpeed);
            _rb.MoveRotation(newRot);
        }

        var newY = _rb.rotation.eulerAngles.y;
        _angularVelocityY = Mathf.DeltaAngle(prevY, newY) / Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        // Z-axis leaning
        var targetLean = Mathf.Clamp(-_angularVelocityY / _rotSpeedForMaxLean * _maxLeanAngle, -_maxLeanAngle, _maxLeanAngle);

        var currentLean = _boardMesh.localEulerAngles.z;
        if (currentLean > 180f) currentLean -= 360f; // [-180, 180]

        var newSidewaysLean = Mathf.Lerp(currentLean, targetLean, Time.deltaTime * _leanSpeed);

        // X-axis leaning
        var flatVel = Vector3.Scale(_rb.linearVelocity, new Vector3(1, 0, 1));
        var targetBackwardLean = Mathf.Clamp(-flatVel.magnitude / _speedForMaxBackwardLean * _maxBackwardLeanAngle, -_maxBackwardLeanAngle, 0f);

        var currentBackwardLean = _boardMesh.localEulerAngles.x;
        if (currentBackwardLean > 180f) currentBackwardLean -= 360f;

        var newBackwardLean = Mathf.Lerp(currentBackwardLean, targetBackwardLean, Time.deltaTime * _leanSpeed);

        // apply
        _boardMesh.localEulerAngles = new Vector3(newBackwardLean, _boardMesh.localEulerAngles.y, newSidewaysLean);

        // Camera zoom out based on speed
        var speedFactor = Mathf.Clamp01(flatVel.magnitude / _cameraZoomOutMaxSpeed);
        _cameraFollow.Radius = Mathf.Lerp(_cameraDefaultRadius, _cameraDefaultRadius * _cameraZoomOutFactor, speedFactor);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw push points on board
        Gizmos.color = Color.cyan;
        foreach (var point in _pushPoints)
        {
            var worldPoint = transform.TransformPoint(point);
            Gizmos.DrawLine(worldPoint, worldPoint - transform.up * _pushDistance);
            Gizmos.DrawSphere(worldPoint, 0.05f);
        }
    }
}