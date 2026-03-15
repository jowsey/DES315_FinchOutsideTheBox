using Mirror;
using System.Collections.Generic;
using TMPro;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private static readonly int RunningState = Animator.StringToHash("Running");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int GroundedState = Animator.StringToHash("Grounded");
    private static readonly int FallState = Animator.StringToHash("Fall");
    private static readonly int GlideState = Animator.StringToHash("Glide");
    
    [Header("Network")]
    [SyncVar] [ReadOnly] public int PlayerIndex;

    [SyncVar] [ReadOnly] public string PlayerName;
    [SyncVar] [ReadOnly] public PlayerPresenceFeed.CatSkin PlayerSkin;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }

    private NetworkAnimator _networkAnimator;
    [SerializeField] private CrosshairDetection _crosshairDetector;
    [SerializeField] private Canvas _nameplateCanvas;
    [SerializeField] private TextMeshProUGUI _playerNameText;
    
    [Header("Animation")]
    [Tooltip("The minimum velocity required to initiate the gliding animation (should be negative)")]
    [SerializeField] private float _fallAnimationMinDownardsVelocity;

    [Header("Input")]
    public InputActionReference MoveAction;

    public InputActionReference JumpAction;
    public InputActionReference InteractAction;

    private bool _jumpPressed;

    //Wwise values
    public AK.Wwise.Event CarSound = new();
    public AK.Wwise.RTPC RTPCSpeed;
    public float RTPCSpeedValue;

    public AK.Wwise.Event FlaskPickup;

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

    [ReadOnly] public Flask HeldFlask;

    [Header("Material Update Data")]
    [SerializeField] private Material _bodyMaterial;

    [SerializeField] private Material _player2Material;

    [field: SyncVar] [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }
    [field: SyncVar] [field: ReadOnly] public float AnalogueMoveScale { get; private set; }

    private List<Vector3> _contactNormals = new();
    
    [SerializeField] private ActionCurveLine _actionCurveLinePrefab;

    [field: SerializeField] public Transform FlaskPickupTarget { get; private set; }

    //Swap to "WheelSeat" (aka dont make a sound when on wheels) 
    public AK.Wwise.Switch footsteps;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _networkAnimator = GetComponent<NetworkAnimator>();
        
        Checkpoint.respawnEvent.AddListener(OnRespawn);
    }

    public override void OnStartClient()
    {
        if (PlayerSkin == PlayerPresenceFeed.CatSkin.Blue)
        {
            foreach (SkinnedMeshRenderer renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMaterial != _bodyMaterial) continue;
                renderer.sharedMaterial = _player2Material;
            }
        }
        
        _camera = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        _playerNameText.text = PlayerName;
    }

    private void Start()
    {
        CarSound.Post(gameObject);
        RTPCSpeed.SetGlobalValue(0);
    }

    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        if (!authority) return;

        Transform newTransform = checkpoint.playerRespawnLocalTransforms[PlayerIndex % checkpoint.playerRespawnLocalTransforms.Length];

        Rb.position = newTransform.position;
        Rb.rotation = newTransform.rotation;
        if (!Rb.isKinematic)
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
        }

        _camera.PreviousStateIsValid = false;
    }

    public override void OnStartLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        
        // Set camera follow target
        if (!_camera.Follow || !_camera.LookAt)
        {
            _camera.gameObject.SetActive(true);
            _camera.Follow = transform;
            _camera.LookAt = transform;
        }
        
        // Hide nameplate for local player
        _nameplateCanvas.gameObject.SetActive(false);
        
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
        if (!authority) return;

        _contactNormals.Clear();

        _jumpPressed |= JumpAction.action.WasPressedThisFrame();

        if (CrosshairDetection.TargetedTransform)
        {
            if (!HeldFlask)
            {
                if (!CrosshairDetection.TargetedTransform.CompareTag("Flask")) return;

                Flask newFlask = CrosshairDetection.TargetedTransform.GetComponentInParent<Flask>();
                if (newFlask.State != Flask.HeldState.None) return;

                if (InteractAction.action.IsPressed())
                {
                    newFlask.CmdTryPickup();
                }
            }
            else if (HeldFlask.State == Flask.HeldState.Held && CrosshairDetection.TargetedTransform.CompareTag("FlaskCarrier"))
            {
                FlaskPutdownTarget carrierTarget = CrosshairDetection.TargetedTransform.GetComponentInChildren<FlaskPutdownTarget>();
                if (InteractAction.action.IsPressed())
                {
                    HeldFlask.CmdTryPutdown(carrierTarget);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (Seat)
        {
            transform.position = Seat.SeatedPosition;
            Physics.SyncTransforms();
            //Swap to no sound when sat on wheels
            AkUnitySoundEngine.SetSwitch("Footsteps", "WheelSeat", gameObject);
        }

        if (_camera && !isLocalPlayer)
        {
            _nameplateCanvas.transform.rotation = Quaternion.LookRotation(_nameplateCanvas.transform.position - _camera.transform.position);
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
        if (!authority) return;

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
        AnalogueMoveScale = inputDirection.magnitude; //input system has a normalise processor on the move input action

        if (WorldSpaceMoveDir.sqrMagnitude > 0)
        {
            _networkAnimator.animator.SetBool(RunningState, true);
            // todo this should definitely run outside of localplayer (for other players' footsteps) ---> Paolo: I think this is fixed now by adding sounds to the animation
            // and rtpc speed should be based on actual wheel speed, not input time, ---> Paolo: Also, I think this should somehow be attached to the cart instead, as right now it only plays the sound when a specific player is riding, but as a temp solution this works
            if (Seat)
            {
                RTPCSpeedValue += 4f;
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

        //Unsitting
        if (Seat && _jumpPressed)
        {
            Seat.CmdUnsitPlayer();
            
            CleanupFixedUpdate();
            return;
        }

        bool grounded = Physics.CheckSphere(Rb.position, _groundedSphereRadius, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore);
        bool groundedOnBumpy = Physics.CheckSphere(Rb.position, _groundedSphereRadius, LayerMask.GetMask("Bumpy"), QueryTriggerInteraction.Ignore);
        Rb.useGravity = !groundedOnBumpy;
        _networkAnimator.animator.SetBool(GroundedState, grounded || groundedOnBumpy);

        if (!Seat)
        {
            Vector3 delta = new Vector3(WorldSpaceMoveDir.x, 0.0f, WorldSpaceMoveDir.z) * (Time.fixedDeltaTime * _moveForce * AnalogueMoveScale);
            foreach (Vector3 normal in _contactNormals)
            {
                if (Vector3.Dot(delta, normal) < 0) //moving into the surface
                {
                    delta = Vector3.ProjectOnPlane(delta, normal);
                }
            }

            Rb.MovePosition(Rb.position + delta);
        }
        
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

        CleanupFixedUpdate();
    }

    private void CleanupFixedUpdate()
    {
        _jumpPressed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!authority) return;

        WheelSeat newSeat = other.GetComponentInParent<WheelSeat>();
        if (newSeat && !Seat)
        {
            NetworkIdentity identity = GetComponent<NetworkIdentity>();
            newSeat.CmdTrySitPlayer(identity);
        }
    }

    private void OnDrawGizmos()
    {
        if (Rb)
        {
            Gizmos.DrawSphere(Rb.position, _groundedSphereRadius);
        }
    }
}