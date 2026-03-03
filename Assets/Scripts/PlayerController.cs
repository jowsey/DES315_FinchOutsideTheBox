using Mirror;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private readonly static int RunningState = Animator.StringToHash("Running");
    private readonly static int JumpTrigger = Animator.StringToHash("Jump");
    private readonly static int IdleBreakerTrigger = Animator.StringToHash("Idle_Break");
    private readonly static int FallState = Animator.StringToHash("Fall");
    private readonly static int GlideState = Animator.StringToHash("Glide");
    private readonly static int BaseColorMapID = Shader.PropertyToID("_BaseColorMap");
    private readonly static int EmissiveColorMapID = Shader.PropertyToID("_EmissiveColorMap");
    
    private int playerNetworkId;
    public static int NextPlayerNetworkId = 0;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }
    private Animator animator;

    [Header("Animation")]
    [Tooltip("The minimum velocity required to initiate the gliding animation (should be negative)")]
    [SerializeField] private float _fallAnimationMinDownardsVelocity;
    [Tooltip("The average number of idle animation loops to play before an idle breaker animation")]
    [SerializeField] private float _idleBreakerFrequency;
    private int _idleBreakerFrequencyTicks; //Impl for _idlBreakerFrequency - same thing but measured in fixed update ticks rather than idle anim loops

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
    [SerializeField] [Range(0, 100)] private float gravityNegationPercentage;

    [SerializeField] private float rotationSmoothingSpeed;

    [Header("Camera")]
    [SerializeField] [ReadOnly] private CinemachineCamera _camera;

    [Header("Movement")]
    [Tooltip("Amount of upwards force applied when jumping")]
    [SerializeField] private float _jumpForce;

    [Tooltip("Amount of forward force applied by movement")]
    [SerializeField] private float _moveForce;

    [Tooltip("Radius of the sphere used for the sphere-raycast grounded check")]
    [SerializeField] private float _groundedSphereRadius;

    [Header("State")]
    [ReadOnly] public WheelSeat Seat;

    [Header("Material Update Data")]
    [SerializeField] private Texture _player2Texture;
    [SerializeField] private Material _player1Material;

    [field: SyncVar]
    [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }
    
    [SerializeField] private float _slopeLimit = 45f;
    private readonly List<Vector3> _steepNormals = new();
    private bool _groundedByContact;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();

        animator = GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Idle")
            {
                int numFixedUpdatesPerIdleAnim = (int)(clip.length / Time.fixedDeltaTime);
                _idleBreakerFrequencyTicks = (int)(numFixedUpdatesPerIdleAnim * _idleBreakerFrequency);
            }
        }

        if (FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length > 1)
        {
            //This is player 2, so change their texture
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMaterial == _player1Material)
                {
                    renderer.material.SetTexture(BaseColorMapID, _player2Texture);
                    renderer.material.SetTexture(EmissiveColorMapID, _player2Texture);
                }
            }
        }
    }

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
        if (isServer)
        {
            RpcSetPlayerNetworkId(NextPlayerNetworkId);
            ++NextPlayerNetworkId;

            carSound.Post(gameObject);
            RTPCSpeed.SetGlobalValue(0);
        }
    }

    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
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
        if (!_camera.Follow || !_camera.LookAt)
        {
            _camera.gameObject.SetActive(true);
            _camera.Follow = transform;
            _camera.LookAt = transform;
        }
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            float angle = Vector3.Angle(contact.normal, Vector3.up);
            if (angle <= _slopeLimit)
            {
                _groundedByContact = true;
            }
            else
            {
                _steepNormals.Add(contact.normal);
            }
        }
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

        animator.SetBool(RunningState, false);
        animator.SetBool(FallState, false);
        animator.SetBool(GlideState, false);

        //Movement
        Quaternion cameraOrientation = _camera ? _camera.State.GetFinalOrientation() : Quaternion.identity;
        Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;

        Vector3 cameraRight = cameraOrientation * Vector3.right;
        Vector2 inputDirection = MoveAction.action.ReadValue<Vector2>();

        WorldSpaceMoveDir = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;

        if (WorldSpaceMoveDir.sqrMagnitude > 0)
        {
            animator.SetBool(RunningState, true);
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

        //Vector3 targetHorizontalVelocity = new Vector3(WorldSpaceMoveDir.x, 0f, WorldSpaceMoveDir.z) * _moveForce;
        //Vector3 currentVelocity = Rb.linearVelocity;
        //Rb.linearVelocity = new Vector3(targetHorizontalVelocity.x, currentVelocity.y, targetHorizontalVelocity.z);

        ////Jump
        //bool groundedOnBumpy = Physics.CheckSphere(Rb.position, _groundedSphereRadius * 4, LayerMask.GetMask("Bumpy"), QueryTriggerInteraction.Ignore);
        //bool grounded = Physics.CheckSphere(Rb.position, _groundedSphereRadius, ~(1 << gameObject.layer),  QueryTriggerInteraction.Ignore);

        Vector3 targetHorizontalVelocity = new Vector3(WorldSpaceMoveDir.x, 0f, WorldSpaceMoveDir.z) * _moveForce;

        // Cancel velocity component pushing INTO any steep surface
        foreach (Vector3 normal in _steepNormals)
        {
            // Project normal onto horizontal plane
            Vector3 wallNormalH = new Vector3(normal.x, 0f, normal.z).normalized;
            float dot = Vector3.Dot(targetHorizontalVelocity, wallNormalH);
            if (dot < 0f) // moving into the wall
            {
                targetHorizontalVelocity -= wallNormalH * dot;
            }
        }

        Vector3 currentVelocity = Rb.linearVelocity;
        Rb.linearVelocity = new Vector3(targetHorizontalVelocity.x, currentVelocity.y, targetHorizontalVelocity.z);

        // Use contact-based grounding instead of (or in addition to) CheckSphere
        bool grounded = _groundedByContact;

        // Reset for next frame
        _groundedByContact = false;
        _steepNormals.Clear();

        if (_jumpPressed && (grounded))
        {
            animator.SetTrigger(JumpTrigger);
            Rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        else if (Rb.linearVelocity.y < _fallAnimationMinDownardsVelocity)
        {
            //Player is falling - are they gliding?
            if (JumpAction.action.IsPressed())
            {
                //Player is gliding
                animator.SetBool(GlideState, true);
                float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
                Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
            }
            else
            {
                //Player is not gliding, they are just falling
                animator.SetBool(FallState, true);
            }
        }

        //Idle-breaker
        AnimatorClipInfo[] animatorInfo = animator.GetCurrentAnimatorClipInfo(0);
        if (animatorInfo.Length > 0 && animatorInfo[0].clip.name == "Idle")
        {
            //Check passes roughly once every _idleBreakerFrequencyTicks ticks
            if (Random.Range(0, _idleBreakerFrequencyTicks) == 0)
            {
                animator.SetTrigger(IdleBreakerTrigger);
            }
        }

        _jumpPressed = false;
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

    private void OnDrawGizmos()
    {
        if (Rb != null)
        {
            Gizmos.DrawSphere(Rb.position, _groundedSphereRadius);
        }
    }
}