using Mirror;
using Sirenix.OdinInspector;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    public Rigidbody Rb { get; private set; }


    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;

    [SerializeField] private InputActionReference _jumpAction;

    private bool _jumpPressed;

    [Tooltip("Percentage of gravity to negate when gliding")]
    [SerializeField] [Range(0, 100)] private float gravityNegationPercentage = 90;

    [SerializeField] private float rotationSmoothingSpeed = 8;


    [Header("Camera")]
    [SerializeField] private CinemachineCamera _camera;


    [Header("Movement")]
    [Tooltip("Amount of upwards force applied when jumping")]
    [SerializeField] private float _jumpForce = 200f;

    [Tooltip("Amount of forward force applied by movement")]
    [SerializeField] private float _moveForce = 6f;

    [Header("State")]
    [SerializeField] [Sirenix.OdinInspector.ReadOnly] public WheelSeat _seat;

    [field: SerializeField] [field: Sirenix.OdinInspector.ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _camera = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(); //GameObject.Find doesn't work because camera is inactive
        _camera.gameObject.SetActive(true);
        _camera.Follow = transform;
        _camera.LookAt = transform;
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (!isLocalPlayer) { return; }

        _jumpPressed |= _jumpAction.action.WasPressedThisFrame();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) { return; }

        //Movement
        Quaternion cameraOrientation = _camera ? _camera.State.GetFinalOrientation() : Quaternion.identity;
        Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;

        Vector3 cameraRight = cameraOrientation * Vector3.right;
        Vector2 inputDirection = _moveAction.action.ReadValue<Vector2>();

        WorldSpaceMoveDir = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;

        CmdSetWorldSpaceMoveDir(WorldSpaceMoveDir);

        if (WorldSpaceMoveDir.sqrMagnitude > 0)
        {
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
        }

        if (_seat && _jumpPressed)
        {
            _seat.CmdUnsitPlayer();
            _seat = null;
        }

        if (!_seat)
        {
            Vector3 delta = new Vector3(WorldSpaceMoveDir.x, 0.0f, WorldSpaceMoveDir.z) * (Time.fixedDeltaTime * _moveForce);
            Rb.MovePosition(Rb.position + delta);

            //Jump
            if (_jumpPressed && Physics.CheckSphere(Rb.position, 0.1f, ~(1 << gameObject.layer)))
            {
                Rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            }
            else if (_jumpAction.action.IsPressed() && Rb.linearVelocity.y < 0.0f)
            {
                float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
                Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
            }
        }

        _jumpPressed = false;
    }

    [Command]
    private void CmdSetWorldSpaceMoveDir(Vector3 dir)
    {
        WorldSpaceMoveDir = dir;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) { return; }

        WheelSeat newSeat = other.GetComponentInParent<WheelSeat>();
        if (newSeat && !_seat)
        {
            NetworkIdentity identity = GetComponent<NetworkIdentity>();
            newSeat.CmdTrySitPlayer(identity);
        }
    }
}