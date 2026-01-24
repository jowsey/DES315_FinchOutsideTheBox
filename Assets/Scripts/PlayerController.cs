using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;

    [Header("Components")]
    private Rigidbody _rb;


    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;

    [SerializeField] private InputActionReference _jumpAction;

    [Tooltip("Percentage of gravity to negate when gliding")]
    [SerializeField] [Range(0,100)] private float gravityNegationPercentage;

    [SerializeField] private float rotationSmoothingSpeed;


    [Header("Camera")]
    [SerializeField] [Required] private CinemachineCamera _camera;

    [SerializeField] [Required] private CinemachineOrbitalFollow _cameraFollow;


    [Header("Movement")]
    [Tooltip("Amount of upwards force applied when jumping")]
    [SerializeField] private float _jumpForce = 100f;

    [Tooltip("Amount of forward force applied by movement")]
    [SerializeField] private float _moveForce = 150f;

    private Collider collider;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        collider = GetComponent<Collider>();
    }

    private void FixedUpdate()
    {
        //Movement
        Quaternion cameraOrientation = _camera.State.GetFinalOrientation();
        Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 cameraRight = cameraOrientation * Vector3.right;
        Vector2 inputDirection = _moveAction.action.ReadValue<Vector2>();
        Vector3 moveDirection = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;
        Vector3 movePositionDelta = new Vector3(moveDirection.x, 0.0f, moveDirection.z) * _moveForce * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + movePositionDelta);

        //Camera
        if (moveDirection.sqrMagnitude != 0.0f)
        {
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(moveDirection, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
        }

        //Jump
        if (_jumpAction.action.WasPressedThisFrame() && Physics.CheckSphere(_rb.position, 0.1f, ~playerLayer))
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        else if (_jumpAction.action.IsPressed() && _rb.linearVelocity.y < 0.0f)
        {
            float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
            _rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
        }
    }
}