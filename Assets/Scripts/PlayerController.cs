using Mirror;
using System.Collections.Generic;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private readonly static int RunningState = Animator.StringToHash("Running");
    private readonly static int JumpTrigger = Animator.StringToHash("Jump");
    private readonly static int IdleBreakerTrigger = Animator.StringToHash("Idle_Break");
    private readonly static int GroundedState = Animator.StringToHash("Grounded");
    private readonly static int FallState = Animator.StringToHash("Fall");
    private readonly static int GlideState = Animator.StringToHash("Glide");
    private readonly static int BaseColorMapID = Shader.PropertyToID("_BaseColorMap");
    private readonly static int EmissiveColorMapID = Shader.PropertyToID("_EmissiveColorMap");
    
    [SyncVar] private int _playerIndex;
    public static int NextPlayerIndex = 0;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }
    private NetworkAnimator _networkAnimator;
    [SerializeField] private CrosshairDetection _crosshairDetector;

    [Header("Animation")]
    [Tooltip("The minimum velocity required to initiate the gliding animation (should be negative)")]
    [SerializeField] private float _fallAnimationMinDownardsVelocity;
    [Tooltip("The average number of idle animation loops to play before an idle breaker animation")]
    [SerializeField] private float _idleBreakerFrequency;
    private int _idleBreakerFrequencyTicks; //Impl for _idlBreakerFrequency - same thing but measured in fixed update ticks rather than idle anim loops

    [Header("Input")]
    public InputActionReference MoveAction;
    public InputActionReference JumpAction;
    public InputActionReference PickupAction;

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
    [SerializeField] private Material _bodyMaterial;

    [field: SyncVar]
    [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }

    private List<Vector3> _contactNormals = new List<Vector3>();
    
    [SerializeField] private ActionCurveLine _actionCurveLinePrefab;
    
    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();

        _networkAnimator = GetComponent<NetworkAnimator>();
        foreach (AnimationClip clip in _networkAnimator.animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Idle")
            {
                int numFixedUpdatesPerIdleAnim = (int)(clip.length / Time.fixedDeltaTime);
                _idleBreakerFrequencyTicks = (int)(numFixedUpdatesPerIdleAnim * _idleBreakerFrequency);
            }
        }
        
        Checkpoint.respawnEvent.AddListener(OnRespawn);
    }

    public override void OnStartServer()
    {
        _playerIndex = NextPlayerIndex++;
    }

    public override void OnStartClient()
    {
        // Set every 2nd player's texture to the alternate colour
        if (_playerIndex % 2 == 1)
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetTexture(BaseColorMapID, _player2Texture);
            propertyBlock.SetTexture(EmissiveColorMapID, _player2Texture);
            
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMaterial != _bodyMaterial) continue;
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private void Start()
    {
        carSound.Post(gameObject);
        RTPCSpeed.SetGlobalValue(0);
    }

    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        if (authority)
        {
            Transform newTransform = checkpoint.playerRespawnLocalTransforms[_playerIndex % checkpoint.playerRespawnLocalTransforms.Length];

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
        
        // todo this sucks
        // eventually we should just link carts to 2 players so we can have an arbitrary number of carts/players
        var wheels = FindObjectsByType<WheelSeat>(FindObjectsSortMode.None);
        var closestWheel = wheels[0];
        var closestDist = float.MaxValue;
        foreach (var wheel in wheels)
        {
            var dist = Vector3.Distance(transform.position, wheel.transform.position);
            if (dist >= closestDist) continue;
            closestWheel = wheel;
            closestDist = dist;
        }

        var onboardingJumpLine = Instantiate(_actionCurveLinePrefab, null);
        onboardingJumpLine.StartFollowTarget = transform;
        onboardingJumpLine.StartTrackingOffset = Vector3.up * 0.5f;
        onboardingJumpLine.EndFollowTarget = closestWheel.transform;
        onboardingJumpLine.EndTrackingOffset = closestWheel.transform.InverseTransformPoint(closestWheel.SeatedPosition);
        onboardingJumpLine.PromptLabel = "Hop on with <b>[Space]</b>!";
        onboardingJumpLine.ShouldDestroy = () => Seat; // if we're sat, job's done
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        if (!authority) { return; }

        _contactNormals.Clear();

        _jumpPressed |= JumpAction.action.WasPressedThisFrame();
        if (CrosshairDetection._hitTransform != null && CrosshairDetection._hitTransform.CompareTag("Flask"))
        {
            Flask flask = CrosshairDetection._hitTransform.GetComponent<Flask>();
            if (PickupAction.action.IsPressed())
            {
                flask.CmdPickup();
            }
        }
    }

    private void LateUpdate()
    {
        if (Seat)
        {
            transform.position = Seat.SeatedPosition;
            Physics.SyncTransforms();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            _contactNormals.Add(contact.normal);
        }
    }

    private void FixedUpdate()
    {
        if (!authority) { return; }

        _networkAnimator.animator.SetBool(RunningState, false);
        _networkAnimator.animator.SetBool(FallState, false);
        _networkAnimator.animator.SetBool(GlideState, false);
        _networkAnimator.animator.SetBool(GroundedState, false);

        //Movement
        Quaternion cameraOrientation = _camera ? _camera.State.GetFinalOrientation() : Quaternion.identity;
        Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;

        Vector3 cameraRight = cameraOrientation * Vector3.right;
        Vector2 inputDirection = MoveAction.action.ReadValue<Vector2>();

        WorldSpaceMoveDir = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;

        if (WorldSpaceMoveDir.sqrMagnitude > 0)
        {
            _networkAnimator.animator.SetBool(RunningState, true);
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

        bool grounded = Physics.CheckSphere(Rb.position, _groundedSphereRadius, ~(1 << gameObject.layer),  QueryTriggerInteraction.Ignore);
        bool groundedOnBumpy = Physics.CheckSphere(Rb.position, _groundedSphereRadius, LayerMask.GetMask("Bumpy"), QueryTriggerInteraction.Ignore);
        Rb.useGravity = !groundedOnBumpy;
        _networkAnimator.animator.SetBool(GroundedState, (grounded || groundedOnBumpy));

        Vector3 delta = new Vector3(WorldSpaceMoveDir.x, 0.0f, WorldSpaceMoveDir.z) * (Time.fixedDeltaTime * _moveForce);
        foreach (Vector3 normal in _contactNormals)
        {
            if (Vector3.Dot(delta, normal) < 0) //moving into the surface
            {
                delta = Vector3.ProjectOnPlane(delta, normal);
            }
        }
        Rb.MovePosition(Rb.position + delta);

        if (_jumpPressed && (grounded || groundedOnBumpy))
        {
            _networkAnimator.SetTrigger(JumpTrigger);
            Rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        else if (Rb.linearVelocity.y < _fallAnimationMinDownardsVelocity)
        {
            //Player is falling - are they gliding?
            if (JumpAction.action.IsPressed())
            {
                //Player is gliding
                _networkAnimator.animator.SetBool(GlideState, true);
                float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
                Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
            }
            else
            {
                //Player is not gliding, they are just falling
                _networkAnimator.animator.SetBool(FallState, true);
            }
        }

        //Idle-breaker
        AnimatorClipInfo[] animatorInfo = _networkAnimator.animator.GetCurrentAnimatorClipInfo(0);
        if (animatorInfo.Length > 0 && animatorInfo[0].clip.name == "Idle")
        {
            //Check passes roughly once every _idleBreakerFrequencyTicks ticks
            if (Random.Range(0, _idleBreakerFrequencyTicks) == 0)
            {
                _networkAnimator.SetTrigger(IdleBreakerTrigger);
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