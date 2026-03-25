using System;
using Mirror;
using System.Collections.Generic;
using TMPro;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private static readonly int RunningState = Animator.StringToHash("Running");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int GroundedState = Animator.StringToHash("Grounded");
    private static readonly int FallState = Animator.StringToHash("Fall");
    private static readonly int GlideState = Animator.StringToHash("Glide");

    public static Material[] SkinMaterials;
    public static Sprite[] SkinIcons;

    [Header("Network")]
    [SyncVar] [ReadOnly] public int PlayerIndex;

    [SyncVar] [ReadOnly] public string PlayerUID;

    [SyncVar] [ReadOnly] public string PlayerName;
    [SyncVar] [ReadOnly] public int PlayerSkinIndex;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }

    private NetworkAnimator _networkAnimator;
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

    public PostWwiseFootstep postWwiseFootstep;

    public AK.Wwise.Event FlaskPickupFX;

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

    [Header("Skin materials")]
    [SerializeField] private Renderer[] _skinnedRenderers;

    [field: SyncVar] [field: SerializeField] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }
    [field: SyncVar] [field: ReadOnly] public float AnalogueMoveScale { get; private set; }

    private List<Vector3> _contactNormals = new();

    [SerializeField] private ActionCurveLine _actionCurveLinePrefab;

    [field: SerializeField] public Transform FlaskPickupTarget { get; private set; }

    //Swap to "WheelSeat" (aka dont make a sound when on wheels) 
    public AK.Wwise.Switch footsteps;

    // Called when a player object is done being initially setup
    // Does NOT imply the player has just joined
    public static UnityEvent<PlayerController> OnPlayerReady = new();

    private void Awake()
    {
        SkinMaterials ??= Resources.LoadAll<Material>("PlayerSkins/Materials");
        SkinIcons ??= Resources.LoadAll<Sprite>("PlayerSkins/Icons");

        Rb = GetComponent<Rigidbody>();
        _networkAnimator = GetComponent<NetworkAnimator>();

        Checkpoint.RespawnEvent.AddListener(OnRespawn);
    }

    public override void OnStartClient()
    {
        foreach (Renderer renderer in _skinnedRenderers)
        {
            renderer.sharedMaterial = SkinMaterials[PlayerSkinIndex];
        }

        _camera = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        _playerNameText.text = PlayerName;

        OnPlayerReady.Invoke(this);
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer) return;
        PlayerPresenceFeed.OnPlayerLeave.Invoke(this);
    }

    private void OnDestroy()
    {
        Checkpoint.RespawnEvent.RemoveListener(OnRespawn);
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

            var orbitalFollow = _camera.GetComponent<CinemachineOrbitalFollow>();
            orbitalFollow.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        // Hide nameplate for local player
        _nameplateCanvas.gameObject.SetActive(false);

        // Set default highlight states for interactables
        Highlight.SetHighlightable("Flask", true);
        Highlight.SetHighlightable("FlaskCarrier", false);

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

        postWwiseFootstep = GetComponent<PostWwiseFootstep>();
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
                if (Seat || !CrosshairDetection.TargetedTransform.CompareTag("Flask")) return;

                Flask newFlask = CrosshairDetection.TargetedTransform.GetComponentInParent<Flask>();
                if (newFlask.State != Flask.FlaskState.Idle) return;

                if (InteractAction.action.IsPressed())
                {
                    newFlask.CmdTryPickup();
                }
            }
            else if (HeldFlask.State == Flask.FlaskState.Held && CrosshairDetection.TargetedTransform.CompareTag("FlaskCarrier"))
            {
                FlaskPutdownTarget carrierTarget = CrosshairDetection.TargetedTransform.GetComponentInChildren<FlaskPutdownTarget>();
                if (InteractAction.action.IsPressed())
                {
                    HeldFlask.CmdTryPutdown(carrierTarget);
                    FlaskPickupFX.Post(gameObject);
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
            Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
        }

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

        if (grounded == true)
        {
            postWwiseFootstep.fallCount = 0;
        }

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

    public void ReturnToCart()
    {
        if (Seat) return;

        var cart = FindAnyObjectByType<Cart>(); // todo use linked cart

        const float radius = 5.5f;
        Vector3 newPosition = default;

        int tries = 0;
        while (newPosition == default)
        {
            tries++;

            var circularPos = Random.insideUnitCircle * radius;
            var attemptedPosition = cart.transform.position + new Vector3(circularPos.x, 1, circularPos.y);

            if (Physics.CheckSphere(attemptedPosition, 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            newPosition = attemptedPosition;
        }

        // Debug.Log($"Found position after {tries} tries");
        Rb.position = newPosition;
        Rb.rotation = Quaternion.LookRotation(cart.transform.position - Rb.position, Vector3.up);
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