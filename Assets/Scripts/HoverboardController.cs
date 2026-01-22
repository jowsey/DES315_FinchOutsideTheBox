using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class HoverboardController : MonoBehaviour
{
    [Header("Components")]
    private Rigidbody _rb;

    [SerializeField] private Transform _boardMesh;

    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;

    [SerializeField] private InputActionReference _jumpAction;

    [Header("Camera")]
    [SerializeField] [Required] private CinemachineCamera _camera;

    [SerializeField] [Required] private CinemachineOrbitalFollow _cameraFollow;

    [SerializeField] private float _cameraZoomOutFactor = 1.2f;

    [SerializeField] private float _cameraZoomOutMaxSpeed = 20f;

    private float _cameraDefaultRadius;

    [Header("Floating")]
    [Tooltip("A set of local points that will be sampled for pushing the board upwards")]
    [SerializeField] private List<Vector3> _pushPoints = new();

    [Tooltip("Amount of force applied when pushing upwards by each point")]
    [SerializeField] private float _pushForce = 100f;

    [Tooltip("The height above ground the points will try and reach")]
    [SerializeField] [SuffixLabel("m")] private float _pushDistance = 0.5f;

    [Tooltip("Exponential factor applied to the distance. Increasing this reduces bobbing up and down, but reduces the height at which it stabilises")]
    [SerializeField] private float _pushExponent = 3.0f;

    [Tooltip("Amplitude of push force sine wave")]
    [SerializeField] private float _sinForce = 0.15f;

    [Tooltip("Wavelength of push force sine wave")]
    [SerializeField] private float _sinSpeed = 2f;

    [Header("Movement")]
    [Tooltip("Amount of forward force applied by movement. Max speed and acceleration are both computed as a mix of this value and the linear damping value in the Rigidbody.")]
    [SerializeField] private float _moveForce = 150f;

    [Tooltip("Speed factor at which hoverboard rotates to face movement direction")]
    [SerializeField] private float _rotationSpeed = 3f;

    [Tooltip("Maximum amount of sideways leaning")]
    [SerializeField] [SuffixLabel("degrees")] private float _maxLeanAngle = 15f;

    [Tooltip("Rotation speed at which the board will fully lean")]
    [SerializeField] [SuffixLabel("degrees/sec")] private float _rotSpeedForMaxLean = 90f;

    [Tooltip("Speed of lean angle smoothing")]
    [SerializeField] private float _leanSpeed = 3f;

    [Tooltip("Maximum amount of backwards leaning")]
    [SerializeField] [SuffixLabel("degrees")] private float _maxBackwardLeanAngle = 5f;

    [Tooltip("Speed at which the board will fully lean backwards")]
    [SerializeField] [SuffixLabel("m/s")] private float _speedForMaxBackwardLean = 15f;

    [Tooltip("If true, the board will rotate to face the movement direction when no input is held")]
    [SerializeField] private bool _idlyRotateTowardsForward;

    [Tooltip("If true, uses an alternative movement system more akin to driving a car")]
    [InfoBox("The alternate movement system requires a much higher rotation speed than the main one. Try multiplying by 30x!")]
    [SerializeField] private bool _useNewMovement;

    // [Tooltip("How much upwards force will be applied when jumping")]
    // [SerializeField] private float _jumpForce = 300f;
    //
    // [Tooltip("How big of a dip will be applied before jumping (visual)")]
    // [SerializeField] private float _jumpDipAmount = 0.2f;

    // [Tooltip("Whether to allow moving while in mid-air")]
    // [SerializeField] private bool _canMoveInMidAir = true;
    //
    // [Tooltip("How far up counts as being in the air?")]
    // [SerializeField] [EnableIf("_canMoveInMidAir")] private float _midAirHeightThreshold = 5f;

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
            if (!Physics.Raycast(ray, out var hitInfo, _pushDistance, ~0, QueryTriggerInteraction.Ignore)) continue;

            var factor = 1f - (hitInfo.distance / _pushDistance);
            var expFactor = Mathf.Pow(factor, _pushExponent);
            _rb.AddForceAtPosition(transform.up * (_pushForce * expFactor * sinFactor), worldPoint);
        }

        // Movement force
        var moveInput = _moveAction.action.ReadValue<Vector2>();

        if (_useNewMovement)
        {
            // Forward
            _rb.AddForce(transform.forward * (moveInput.y * _moveForce));

            // Rotation
            _rb.AddTorque(transform.up * (moveInput.x * _rotationSpeed));
        }
        else
        {
            var orientation = _camera.State.GetFinalOrientation();
            var cameraForward = Vector3.Scale(orientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;
            var cameraRight = orientation * Vector3.right;

            // Forward
            var inputDir = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
            _rb.AddForce(inputDir * _moveForce);

            // Rotation
            var flatVel = Vector3.Scale(_rb.linearVelocity, new Vector3(1, 0, 1));
            var prevY = _rb.rotation.eulerAngles.y;

            // Point towards input direction, or velocity direction if no input and moving
            var pointDir = inputDir != Vector3.zero
                ? inputDir
                : _idlyRotateTowardsForward && flatVel.magnitude > 0.5f
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
    }

    private void LateUpdate()
    {
        if (_useNewMovement)
        {
            // alternate movement sets torque so we can just read directly
            _angularVelocityY = _rb.angularVelocity.y * Mathf.Rad2Deg;
        }

        // Z-axis leaning
        var targetSidewaysLean = Mathf.Clamp(-_angularVelocityY / _rotSpeedForMaxLean * _maxLeanAngle, -_maxLeanAngle, _maxLeanAngle);

        var currentSidewaysLean = _boardMesh.localEulerAngles.z;
        if (currentSidewaysLean > 180f) currentSidewaysLean -= 360f; // [-180, 180]

        var newSidewaysLean = Mathf.Lerp(currentSidewaysLean, targetSidewaysLean, Time.deltaTime * _leanSpeed);

        // X-axis leaning
        var flatVel = Vector3.Scale(_rb.linearVelocity, new Vector3(1, 0, 1));
        var targetBackwardLean = Mathf.Clamp(-flatVel.magnitude / _speedForMaxBackwardLean * _maxBackwardLeanAngle, -_maxBackwardLeanAngle, 0f);

        var currentBackwardsLean = _boardMesh.localEulerAngles.x;
        if (currentBackwardsLean > 180f) currentBackwardsLean -= 360f;

        var newBackwardsLean = Mathf.Lerp(currentBackwardsLean, targetBackwardLean, Time.deltaTime * _leanSpeed);

        // apply
        _boardMesh.localEulerAngles = new Vector3(newBackwardsLean, _boardMesh.localEulerAngles.y, newSidewaysLean);

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