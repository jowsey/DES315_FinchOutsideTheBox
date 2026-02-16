using Mirror;
using Sirenix.Utilities;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private int playerNetworkId;
    private static int nextPlayerNetworkId = 0;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }

    [Header("Input")]
    [SerializeField] private InputActionReference _moveAction;

    [SerializeField] private InputActionReference _jumpAction;

    private bool _jumpPressed;

    //Wwise Event to trigger footstep sound
    public AK.Wwise.Event footstepSound = new AK.Wwise.Event();
    public AK.Wwise.Event carSound = new AK.Wwise.Event();

    [Tooltip("Percentage of gravity to negate when gliding")]
    [SerializeField] [Range(0, 100)] private float gravityNegationPercentage = 90;

    [SerializeField] private float rotationSmoothingSpeed = 8;


    [Header("Camera")]
    [SerializeField] [ReadOnly] private CinemachineCamera _camera;


    [Header("Movement")]
    [Tooltip("Amount of upwards force applied when jumping")]
    [SerializeField] private float _jumpForce = 200f;

    [Tooltip("Amount of forward force applied by movement")]
    [SerializeField] private float _moveForce = 6f;

    [Header("State")]
    [ReadOnly] public WheelSeat Seat;

    [field: SyncVar]
    [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
        if (isServer)
        {
            RpcSetPlayerNetworkId(nextPlayerNetworkId);
            ++nextPlayerNetworkId;

            carSound.Post(gameObject);
        }
    }

    [ClientRpc]
    void RpcSetPlayerNetworkId(int id)
    {
        playerNetworkId = id;
    }

    void OnRespawn(Checkpoint checkpoint)
    {
        if (isLocalPlayer)
        {
            Transform newTransform = checkpoint.playerRespawnLocalTransforms[playerNetworkId];

            Rb.position = newTransform.position;
            Rb.rotation = newTransform.rotation;
            if (!Rb.isKinematic)
            {
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }
        }
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        
        _camera = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include); //GameObject.Find doesn't work because camera is inactive
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

        if (WorldSpaceMoveDir.sqrMagnitude > 0 && Seat == null)
        {
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
            footstepSound.Post(gameObject);
            
        }

        if (Seat && _jumpPressed)
        {
            Seat.CmdUnsitPlayer();
            Seat = null;
        }

        if (!Seat)
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

    

    private void OnTriggerEnter(Collider other)
    {
        if (!isLocalPlayer) { return; }

        WheelSeat newSeat = other.GetComponentInParent<WheelSeat>();
        if (newSeat && !Seat)
        {
            NetworkIdentity identity = GetComponent<NetworkIdentity>();
            newSeat.CmdTrySitPlayer(identity);
        }
    }
}