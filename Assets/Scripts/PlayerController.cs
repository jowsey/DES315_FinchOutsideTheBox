using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.RenderGraphModule;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private int playerNetworkId;
    private static int nextPlayerNetworkId = 0;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }
    private Animator animator;

    [Header("Animation")]
    [Tooltip("The minimum velocity required to initiate the gliding animation (should be negative)")]
    [SerializeField] private float _glideAnimationMinDownardsVelocity;

    [Header("Input")]
    public InputActionReference MoveAction;
    public InputActionReference JumpAction;

    private bool _jumpPressed;

    //Wwise Event to trigger footstep sound
    public AK.Wwise.Event footstepSound = new AK.Wwise.Event();
    
    //Car Stuff
    public AK.Wwise.Event carSound = new AK.Wwise.Event();
    public AK.Wwise.RTPC RTPCSpeed;
    public float RTPCSpeedValue;

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

    [Header("Material Update Data")]
    [SerializeField] private Texture _player2Texture;
    [SerializeField] private Material _player1Material;

    [field: SyncVar]
    [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
        if (isServer)
        {
            RpcSetPlayerNetworkId(nextPlayerNetworkId);
            ++nextPlayerNetworkId;

            carSound.Post(gameObject);
            RTPCSpeed.SetGlobalValue(0);
        }

        Debug.Log(playerNetworkId);

        if (playerNetworkId != 0)
        {
            //This is player 2, so change their texture
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMaterial == _player1Material)
                {
                    renderer.material.SetTexture("_BaseColorMap", _player2Texture);
                    renderer.material.SetTexture("_EmissiveColorMap", _player2Texture);
                }
            }
        }
    }

    [ClientRpc]
    void RpcSetPlayerNetworkId(int id)
    {
        playerNetworkId = id;
    }

    void OnRespawn(Checkpoint checkpoint)
    {
        if (authority)
        {
            Transform newTransform = checkpoint.playerRespawnLocalTransforms[playerNetworkId];

            Rb.position = newTransform.position;
            Rb.rotation = newTransform.rotation;
            if (!Rb.isKinematic)
            {
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }

            _camera.PreviousStateIsValid = false;
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
        if (!authority) { return; }

        _jumpPressed |= JumpAction.action.WasPressedThisFrame();
    }

    private void LateUpdate()
    {
        if (Seat)
        {
            transform.position = Seat.SeatedPosition;
            Physics.SyncTransforms();
        }
    }

    private void FixedUpdate()
    {
        if (!authority) { return; }

        //Movement
        Quaternion cameraOrientation = _camera ? _camera.State.GetFinalOrientation() : Quaternion.identity;
        Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;

        Vector3 cameraRight = cameraOrientation * Vector3.right;
        Vector2 inputDirection = MoveAction.action.ReadValue<Vector2>();

        WorldSpaceMoveDir = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;

        if (WorldSpaceMoveDir.sqrMagnitude > 0)
        {
            animator.SetBool("Running", true);
            // todo this should definitely run outside of localplayer (for other players' footsteps) and rtpc speed should be based on actual wheel speed, not input time
            if (Seat)
            {
                RTPCSpeedValue += 4f;
            }
            else
            {
                footstepSound.Post(gameObject);
            }
            
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));

        }
        else
        {
            animator.SetBool("Running", false);
            // todo again should be based on cart speed
            RTPCSpeedValue -= 3f;
        }

        RTPCSpeedValue = Mathf.Clamp(RTPCSpeedValue, 0, 100);
        RTPCSpeed.SetGlobalValue(RTPCSpeedValue);

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
            var grounded = Physics.CheckSphere(Rb.position, 0.1f, ~(1 << gameObject.layer),  QueryTriggerInteraction.Ignore);
            if (!grounded)
            {
                Debug.Log("Not grounded");
                animator.SetBool("Running", false);
            }
            if (_jumpPressed && grounded)
            {
                animator.SetTrigger("Jump Up");
                Rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            }
            else if (JumpAction.action.IsPressed() && Rb.linearVelocity.y < _glideAnimationMinDownardsVelocity)
            {
                animator.SetBool("Jump Down", true);
                float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
                Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
            }
            else if (Rb.linearVelocity.y < _glideAnimationMinDownardsVelocity)
            {
                animator.SetBool("Jump Down", true);
            }
            else if (Rb.linearVelocity.y < 1e-2 && animator.GetCurrentAnimatorClipInfo(0)[0].clip.name == "Jump_Up")
            {
                animator.SetBool("Jump Down", true);
            }
            else
            {
                animator.SetBool("Jump Down", false);
            }
        }

        _jumpPressed = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawSphere(Rb.position, 0.1f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!authority) { return; }

        WheelSeat newSeat = other.GetComponentInParent<WheelSeat>();
        if (newSeat && !Seat)
        {
            NetworkIdentity identity = GetComponent<NetworkIdentity>();
            newSeat.CmdTrySitPlayer(identity);
        }
    }
}