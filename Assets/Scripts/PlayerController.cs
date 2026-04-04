using Mirror;
using System.Collections.Generic;
using TMPro;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Util;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private static readonly int RunningState = Animator.StringToHash("Running");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int GroundedState = Animator.StringToHash("Grounded");
    private static readonly int FallState = Animator.StringToHash("Fall");
    private static readonly int GlideState = Animator.StringToHash("Glide");

    public static SkinData[] LoadedSkins;

    private static readonly HashSet<int> _playerRbIds = new();
    public static bool IsPlayerRb(int id) => _playerRbIds.Contains(id);

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

    public WwiseAnimationEvents WwiseAnimationEvents { get; private set; }

    public AK.Wwise.Event FlaskPickupFX;

    [Tooltip("Percentage of gravity to negate when gliding")]
    [SerializeField] [Range(0, 100)] private float gravityNegationPercentage;

    [SerializeField] private float rotationSmoothingSpeed;

    [Header("Camera")]
    [SerializeField] [ReadOnly] private CinemachineCamera _cinemachineCamera;

    [SerializeField] [ReadOnly] private Camera _camera;
    [SerializeField] private Transform _firstPersonCameraViewPosition;
    [SerializeField] private float _firstPersonSensitivity;
    private InputActionReference _firstPersonLookAction;
    private float _pitch;


    [Header("Movement")]
    [Tooltip("Amount of upwards force applied when jumping")]
    [SerializeField] private float _jumpForce;

    [Tooltip("Amount of forward force applied by movement")]
    [SerializeField] private float _moveForce;

    [Tooltip("Radius of the sphere used for the sphere-raycast grounded check")]
    [SerializeField] private float _groundedSphereRadius;

    private Collider[] _groundedCheckColliderBuffer = new Collider[32];

    [Header("State")]
    [ReadOnly] public WheelSeat Seat;

    [ReadOnly] public Flask HeldFlask;

    [field: SyncVar] [field: ShowInInspector] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }
    [field: SyncVar] [field: ShowInInspector] [field: ReadOnly] public float AnalogueMoveScale { get; private set; }

    [Header("Skin materials")]
    [SerializeField] private Renderer[] _skinnedRenderers;

    private List<Vector3> _contactNormals = new();

    [SerializeField] private ActionCurveLine _actionCurveLinePrefab;

    [field: SerializeField] public Transform FlaskPickupTarget { get; private set; }

    // Called when a player object is done being initially setup
    // Does NOT imply the player has just joined
    public static readonly UnityEvent<PlayerController> OnPlayerReady = new();

    // While there are any control blockers, the player won't be able to be controlled
    private static readonly HashSet<Object> _controlBlockers = new();
    private static CinemachineInputAxisController _cinemachineInput;

    public static bool ControlsEnabled => _controlBlockers.Count == 0;

    [SerializeField] private Transform _cameraObstructionDithererRayEndPosition;

    public static void AddControlBlocker(Object blocker)
    {
        _controlBlockers.Add(blocker);

        if (_controlBlockers.Count == 1)
        {
            if (_cinemachineInput) _cinemachineInput.enabled = false;
        }
    }

    public static void RemoveControlBlocker(Object blocker)
    {
        _controlBlockers.Remove(blocker);

        if (_controlBlockers.Count == 0)
        {
            if (_cinemachineInput) _cinemachineInput.enabled = true;
        }
    }

    private void Awake()
    {
        LoadedSkins ??= Resources.LoadAll<SkinData>("PlayerSkins");

        Rb = GetComponent<Rigidbody>();
        _networkAnimator = GetComponent<NetworkAnimator>();
        WwiseAnimationEvents = GetComponent<WwiseAnimationEvents>();

        Checkpoint.RespawnEvent.AddListener(OnRespawn);
    }

    public override void OnStartClient()
    {
        foreach (Renderer renderer in _skinnedRenderers)
        {
            renderer.sharedMaterial = LoadedSkins[PlayerSkinIndex].Material;
        }

        _cinemachineCamera = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
        _playerNameText.text = PlayerName;

        OnPlayerReady.Invoke(this);
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer) return;
        PlayerPresenceFeed.OnPlayerLeave.Invoke(this);
    }

    public override void OnStartLocalPlayer()
    {
        // Initialise statics
        if (!_cinemachineInput)
        {
            _cinemachineInput = FindAnyObjectByType<CinemachineInputAxisController>(FindObjectsInactive.Include);
        }

        if (!_camera)
        {
            _camera = Camera.main;
        }

        _pitch = 0.0f;
        _firstPersonLookAction = _cinemachineInput.Controllers[0].Input.InputAction;

        Cursor.lockState = CursorLockMode.Locked;

        // Set camera follow target
        if (!_cinemachineCamera.Follow || !_cinemachineCamera.LookAt)
        {
            _cinemachineCamera.gameObject.SetActive(true);
            _cinemachineCamera.Follow = transform;
            _cinemachineCamera.LookAt = transform;

            var orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            orbitalFollow.HorizontalAxis.Value = transform.eulerAngles.y;
        }

        // Hide nameplate for local player
        _nameplateCanvas.gameObject.SetActive(false);

        // Set default highlight states for interactables
        Highlight.SetHighlightable("Flask", true);
        Highlight.SetHighlightable("FlaskCarrier", false);

        // todo this sucks
        // eventually we should just link carts to 2 players so we can have an arbitrary number of carts/players
        var wheels = FindObjectsByType<WheelSeat>(FindObjectsSortMode.InstanceID);
        var assignedWheel = wheels[PlayerIndex % wheels.Length];

        var onboardingJumpLine = Instantiate(_actionCurveLinePrefab, null);
        onboardingJumpLine.StartFollowTarget = transform;
        onboardingJumpLine.StartTrackingOffset = Vector3.up * 0.5f;
        onboardingJumpLine.EndFollowTarget = assignedWheel.transform;
        onboardingJumpLine.EndTrackingOffset = assignedWheel.transform.InverseTransformPoint(assignedWheel.SeatedPosition);
        onboardingJumpLine.PromptLabel = "Hop on with <b>[Space]</b>!";
        onboardingJumpLine.ShouldDestroy = () => Seat; // if we're sat, job's done

        // Only local player gets a Wwise audio listener
        gameObject.AddComponent<AkAudioListener>();

        ObstructionDitherer.PlayerTransform = _cameraObstructionDithererRayEndPosition;
    }

    public override void OnStopLocalPlayer()
    {
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        Checkpoint.RespawnEvent.RemoveListener(OnRespawn);
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        if (!authority || Seat) return;

        Transform newTransform = checkpoint.playerRespawnLocalTransforms[PlayerIndex % checkpoint.playerRespawnLocalTransforms.Length];

        Rb.position = newTransform.position;
        Rb.rotation = newTransform.rotation;
        Rb.linearVelocity = Vector3.zero;
        Rb.angularVelocity = Vector3.zero;

        _cinemachineCamera.PreviousStateIsValid = false;
    }

    private void OnEnable()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.hasModifiableContacts = true;

        _playerRbIds.Add(Rb.GetInstanceID());
    }

    private void OnDisable()
    {
        _playerRbIds.Remove(Rb.GetInstanceID());
    }

    private void Update()
    {
        if (!authority) return;

        //First-person controls
        if (CameraZoomController.FirstPerson && ControlsEnabled)
        {
            Vector2 scaledMouseDelta = _firstPersonLookAction.action.ReadValue<Vector2>() * _firstPersonSensitivity;
            _pitch = Mathf.Clamp(_pitch - scaledMouseDelta.y, -89.0f, 89.0f);

            transform.rotation *= Quaternion.Euler(0f, scaledMouseDelta.x, 0f);
        }

        _contactNormals.Clear();

        _jumpPressed |= JumpAction.action.WasPressedThisFrame();

        if (CrosshairDetection.TargetedTransform)
        {
            if (!HeldFlask)
            {
                if (Seat || !CrosshairDetection.TargetedTransform.CompareTag("Flask")) return;

                Flask newFlask = CrosshairDetection.TargetedTransform.GetComponentInParent<Flask>();
                if (newFlask.State != Flask.FlaskState.Idle) return;

                if (InteractAction.action.WasPressedThisFrame())
                {
                    newFlask.CmdTryPickup();
                }
            }
            else if (HeldFlask.State == Flask.FlaskState.Held && CrosshairDetection.TargetedTransform.CompareTag("FlaskCarrier"))
            {
                FlaskPutdownTarget carrierTarget = CrosshairDetection.TargetedTransform.GetComponentInChildren<FlaskPutdownTarget>();
                if (InteractAction.action.WasPressedThisFrame())
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
            Rb.position = Seat.SeatedPosition;
            transform.position = Seat.SeatedPosition;
        }

        if (!isLocalPlayer)
        {
            _nameplateCanvas.transform.rotation = Quaternion.LookRotation(_nameplateCanvas.transform.position - _cinemachineCamera.transform.position);
        }

        if (isLocalPlayer && CameraZoomController.FirstPerson)
        {
            _camera.transform.position = _firstPersonCameraViewPosition.position;
            _camera.transform.rotation = transform.rotation * Quaternion.Euler(_pitch, 0f, 0f);
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
        // Audio state for all clients
        if (!Seat && Physics.Raycast(Rb.position, Vector3.down, out var hit, 0.1f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
        {
            Renderer hitRenderer = hit.transform.GetComponentInChildren<Renderer>();
            if (hitRenderer)
            {
                switch (hitRenderer.sharedMaterial.name)
                {
                    case "Sand_SD" or "Sand_Background":
                        AkUnitySoundEngine.SetSwitch("Footsteps", "Sand", gameObject);
                        break;
                    case "Stone Floor Light" or "Stone Floor Dark" or "Prototype_512x512_White":
                        AkUnitySoundEngine.SetSwitch("Footsteps", "Stone", gameObject);
                        break;
                    case "Bricks_SD":
                        AkUnitySoundEngine.SetSwitch("Footsteps", "Wood", gameObject);
                        break;
                }
            }
        }

        if (WwiseAnimationEvents.GlideTriggered && !_networkAnimator.animator.GetBool(GlideState))
        {
            WwiseAnimationEvents.ResetGlideTrigger();
        }

        if (!authority) return;

        //Movement input
        Vector2 inputDirection = ControlsEnabled ? MoveAction.action.ReadValue<Vector2>() : Vector2.zero; //no input when controls are blocked
        AnalogueMoveScale = inputDirection.magnitude; //input system has a normalise processor on the move input action
        if (CameraZoomController.FirstPerson)
        {
            Vector3 forward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 right = transform.right;
            WorldSpaceMoveDir = (forward * inputDirection.y + right * inputDirection.x).normalized;
        }
        else
        {
            Quaternion cameraOrientation = _cinemachineCamera ? _cinemachineCamera.State.GetFinalOrientation() : Quaternion.identity;
            Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 cameraRight = cameraOrientation * Vector3.right;

            WorldSpaceMoveDir = (cameraForward * inputDirection.y + cameraRight * inputDirection.x).normalized;
            if (WorldSpaceMoveDir.sqrMagnitude > 0)
            {
                Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
            }
        }

        _networkAnimator.animator.SetBool(RunningState, WorldSpaceMoveDir.sqrMagnitude > 0);

        //Unsitting
        if (Seat && ControlsEnabled && _jumpPressed)
        {
            Seat.CmdUnsitPlayer();

            CleanupFixedUpdate();
            return;
        }

        //Grounded
        var groundedHits = Physics.OverlapSphereNonAlloc(Rb.position, _groundedSphereRadius, _groundedCheckColliderBuffer, ~0, QueryTriggerInteraction.Ignore);
        bool grounded = false;
        for (int i = 0; i < groundedHits; i++)
        {
            // ignore self but *do* find other players
            // in a just world this would just be a T[].AsSpan().Any() call but noOoOo Mono doesn't have the technology
            if (_groundedCheckColliderBuffer[i].transform.root == transform) continue;
            grounded = true;
            break;
        }

        bool groundedOnBumpy = Physics.CheckSphere(Rb.position, _groundedSphereRadius, LayerMask.GetMask("Bumpy"), QueryTriggerInteraction.Ignore);
        Rb.useGravity = !groundedOnBumpy;
        _networkAnimator.animator.SetBool(GroundedState, grounded || groundedOnBumpy);

        //Movement
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

        bool isFalling = Rb.linearVelocity.y < _fallAnimationMinDownardsVelocity;

        //Player is falling - are they gliding?
        if (ControlsEnabled && isFalling && JumpAction.action.IsPressed())
        {
            //Player is gliding
            float gravityNegationPercentage01 = gravityNegationPercentage / 100.0f;
            Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);

            _networkAnimator.animator.SetBool(FallState, false);
            _networkAnimator.animator.SetBool(GlideState, true);
        }
        else if (isFalling)
        {
            //Player is not gliding, they are just falling
            _networkAnimator.animator.SetBool(FallState, true);
            _networkAnimator.animator.SetBool(GlideState, false);
        }
        else
        {
            //Player is not falling at all
            _networkAnimator.animator.SetBool(FallState, false);
            _networkAnimator.animator.SetBool(GlideState, false);
        }

        //Jumping
        if (ControlsEnabled && _jumpPressed && (grounded || groundedOnBumpy))
        {
            _networkAnimator.SetTrigger(JumpTrigger);
            Rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }

        CleanupFixedUpdate();
    }

    [Command]
    public void CmdReturnToCart()
    {
        if (Seat) return;

        var cart = FindAnyObjectByType<Cart>(); // todo use linked cart

        const float radius = 6f;
        Vector3 newPosition = default;

        const int maxTries = 50;
        var tries = 0;
        while (newPosition == default && tries++ < maxTries)
        {
            var circularPos = Random.insideUnitCircle * radius;
            var attemptedPosition = cart.transform.position + new Vector3(circularPos.x, 0.5f, circularPos.y);

            if (Physics.CheckSphere(attemptedPosition, 0.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            newPosition = attemptedPosition;
        }

        if (newPosition == default)
        {
            Debug.LogWarning($"Failed to find a caravan return position after {maxTries} tries. Somehow.");
            return;
        }

        var newRotation = Quaternion.LookRotation(cart.transform.position - newPosition, Vector3.up);
        newRotation = Quaternion.Euler(0, newRotation.eulerAngles.y, 0); // flatten angle
        GetComponent<NetworkTransformBase>().CmdTeleport(newPosition, newRotation);
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
            newSeat.CmdTrySitPlayer();
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