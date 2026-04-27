using Mirror;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using TMPro;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using Util;
using VoIP;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    private static readonly int RunningState = Animator.StringToHash("Running");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int GroundedState = Animator.StringToHash("Grounded");
    private static readonly int FallState = Animator.StringToHash("Fall");
    private static readonly int GlideState = Animator.StringToHash("Glide");

    public static SkinData[] LoadedSkins;

    public static PlayerController LocalPlayer { get; private set; }

    private static readonly HashSet<int> _playerRbIds = new();
    public static bool IsPlayerRb(int id) => _playerRbIds.Contains(id);

    [Header("Network")]
    [SyncVar] [ReadOnly] public int PlayerIndex;

    [SyncVar] [ReadOnly] public string PlayerUID;

    [SyncVar(hook = nameof(OnPlayerNameChanged))][ReadOnly] public string PlayerName;
    [SyncVar] [ReadOnly] public int PlayerSkinIndex;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }

    private NetworkAnimator _networkAnimator;
    [SerializeField] private Canvas _nameplateCanvas;
    [SerializeField] public TextMeshProUGUI PlayerNameText; //Public for cutscene puppeteering

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
    private Camera _camera;

    private CinemachineCamera _cinemachineCamera;

    [SerializeField] private Transform _firstPersonCameraViewPosition;
    [SerializeField] [Required] private InputActionReference _firstPersonLookAction;
    private float _cameraPitch;
    private float _cameraYawAccumulator; //Accumulate in Update() from input, apply in FixedUpdate(), restore in CleanupFixedUpdate()

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
    [SyncVar] public float AnalogueMoveScale;

    [Header("Skin materials")]
    public Renderer[] SkinnedRenderers;

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

    public bool FlaskPickupAllowed => ControlsEnabled && !Seat && !HeldFlask;
    public bool FlaskPutdownAllowed => ControlsEnabled && !Seat && HeldFlask && HeldFlask.State == Flask.FlaskState.Held;

    //Set in inspector to true if this player will only exist in cutscenes
    public bool CutscenePlayer;

    //Cutscene puppeting
    [HideInInspector] public Vector3 PuppetWorldSpaceMoveDir;
    [HideInInspector] public bool PuppetRequestJump;
    [HideInInspector] public float PuppetGravityMultiplier;
    [HideInInspector] public float PuppetJumpForceMultiplier;
    [HideInInspector] public bool IsPuppet;

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

        foreach (CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (cam.CompareTag("FreeLookCam"))
            {
                _cinemachineCamera = cam;
                break;
            }
        }

        Rb = GetComponent<Rigidbody>();
        _networkAnimator = GetComponent<NetworkAnimator>();
        WwiseAnimationEvents = GetComponentInChildren<WwiseAnimationEvents>(true);

        Checkpoint.RespawnEvent.AddListener(OnRespawn);

        PuppetGravityMultiplier = 1.0f;
        PuppetJumpForceMultiplier = 1.0f;
        IsPuppet = false;
        PuppetWorldSpaceMoveDir = Vector3.zero;
        PuppetRequestJump = false;
    }

    private void Start()
    {
        // doesn't work in Awake on non-host. "huh?" don't worry about it
        _camera = Camera.main;

        PlayableDirector director = FindAnyObjectByType<PlayableDirector>();
        if (director)
        {
            director.played += OnCutsceneStarted;
            director.stopped += OnCutsceneStopped;
        }

        if (director.state == PlayState.Paused && director.time == director.initialTime)
        {
            //The cutscene hasn't started yet
            if (NetworkServer.connections.Count == 1)
            {
                //We are player 1
                CutscenePuppeteer puppeteer = FindAnyObjectByType<CutscenePuppeteer>();
                puppeteer.SetPlayer1Name(PlayerName);

                //Default (in case you somehow beat the game by yourself without anybody else joining?)
                puppeteer.SetPlayer2Name("DefaultCat");
                puppeteer.SetPlayer2SkinIndex(1);
            }
            else if (NetworkServer.connections.Count == 2)
            {
                //We are player 2
                CutscenePuppeteer puppeteer = FindAnyObjectByType<CutscenePuppeteer>();
                puppeteer.SetPlayer2Name(PlayerName);
                puppeteer.SetPlayer2SkinIndex(PlayerSkinIndex);
            }
        }
        else
        {
            //The cutscene has already started
            OnCutsceneStarted(director);
        }
    }

    public override void OnStartClient()
    {
        foreach (Renderer renderer in SkinnedRenderers)
        {
            if (renderer.transform.name == "eyes_MESH") { continue; }
            renderer.sharedMaterial = LoadedSkins[PlayerSkinIndex].Material;
        }

        PlayerNameText.text = PlayerName;

        if (!CutscenePlayer) { OnPlayerReady.Invoke(this); }
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer) return;
        PlayerPresenceFeed.OnPlayerLeave.Invoke(this);
    }

    public override void OnStartLocalPlayer()
    {
        // Initialise statics
        if (!_cinemachineInput) _cinemachineInput = FindAnyObjectByType<CinemachineInputAxisController>(FindObjectsInactive.Include);
        LocalPlayer = this;

        Cursor.lockState = CursorLockMode.Locked;

        // Set camera follow target
        if (!_cinemachineCamera.Follow || !_cinemachineCamera.LookAt)
        {
            _cinemachineCamera.gameObject.SetActive(true);
            _cinemachineCamera.Follow = transform;
            _cinemachineCamera.LookAt = transform;
            var orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();
            orbitalFollow.HorizontalAxis.Value = transform.eulerAngles.y;

            // Snap to position
            var brain = FindAnyObjectByType<CinemachineBrain>(FindObjectsInactive.Include);
            var prevUpdateMethod = brain.UpdateMethod;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.ManualUpdate();
            brain.UpdateMethod = prevUpdateMethod;
            _cinemachineCamera.PreviousStateIsValid = false;
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
        onboardingJumpLine.PromptLabel = "Hop on with";
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

        PlayableDirector director = FindAnyObjectByType<PlayableDirector>();
        if (director)
        {
            director.played += OnCutsceneStarted;
            director.stopped += OnCutsceneStopped;
        }
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

    private void OnPlayerNameChanged(string oldValue, string newValue)
    {
        if (PlayerNameText) { PlayerNameText.text = newValue; }
        LayoutRebuilder.ForceRebuildLayoutImmediate(_nameplateCanvas.GetComponent<RectTransform>());
    }

    private void Update()
    {
        if (!authority && !IsPuppet) return;

        //First-person controls
        if (CameraZoomController.FirstPerson && ControlsEnabled)
        {
            Vector2 scaledMouseDelta = _firstPersonLookAction.action.ReadValue<Vector2>() * (SettingsManager.ActiveSettings.FirstPersonSensPercent * 0.01f);
            _cameraPitch = Mathf.Clamp(_cameraPitch - scaledMouseDelta.y, -89.0f, 89.0f);
            _cameraYawAccumulator += scaledMouseDelta.x;
        }

        _contactNormals.Clear();

        _jumpPressed |= JumpAction.action.WasPressedThisFrame();

        if (CrosshairDetection.TargetedTransform)
        {
            if (FlaskPickupAllowed)
            {
                if (!CrosshairDetection.TargetedTransform.CompareTag("Flask")) return;

                Flask newFlask = CrosshairDetection.TargetedTransform.GetComponentInParent<Flask>();
                if (newFlask.State != Flask.FlaskState.Idle) return;

                if (InteractAction.action.WasPressedThisFrame())
                {
                    newFlask.CmdTryPickup();
                }
            }
            else if (FlaskPutdownAllowed)
            {
                if (!CrosshairDetection.TargetedTransform.CompareTag("FlaskCarrier")) return;

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

        if (!isLocalPlayer || CutscenePlayer)
        {
            _nameplateCanvas.transform.rotation = Quaternion.LookRotation(_nameplateCanvas.transform.position - _camera.transform.position);
        }

        if (isLocalPlayer && CameraZoomController.FirstPerson)
        {
            _camera.transform.position = _firstPersonCameraViewPosition.position;
            _camera.transform.rotation = transform.rotation * Quaternion.Euler(_cameraPitch, _cameraYawAccumulator, 0f);
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
        if (!Seat && Physics.Raycast(Rb.position + (Vector3.up * 0.01f), Vector3.down, out var hit, 0.1f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
        {
            Renderer hitRenderer = hit.transform.GetComponentInChildren<Renderer>();
            if (hitRenderer)
            {
                if (hitRenderer.sharedMaterial.name.StartsWith("Sand_"))
                {
                    AkUnitySoundEngine.SetSwitch("Footsteps", "Sand", gameObject);
                }
                else if (hitRenderer.sharedMaterial.name.StartsWith("Stone_") ||
                         hitRenderer.sharedMaterial.name == "Prototype_512x512_White" ||
                         hitRenderer.sharedMaterial.name == "Bricks_SD")
                {
                    AkUnitySoundEngine.SetSwitch("Footsteps", "Stone", gameObject);
                }
            }
        }

        if (WwiseAnimationEvents.GlideTriggered && !_networkAnimator.animator.GetBool(GlideState))
        {
            WwiseAnimationEvents.ResetGlideTrigger();
        }

        if (!authority && !IsPuppet) return;

        //Movement input
        if (IsPuppet)
        {
            if (PuppetWorldSpaceMoveDir.sqrMagnitude > 0)
            {
                Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(PuppetWorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * rotationSmoothingSpeed));
            }
            else
            {
                AnalogueMoveScale = 0.0f;
            }
            _networkAnimator.animator.SetBool(RunningState, PuppetWorldSpaceMoveDir.sqrMagnitude > 0);
            WorldSpaceMoveDir = PuppetWorldSpaceMoveDir;
        }
        else
        {
            Vector2 inputDirection = ControlsEnabled ? MoveAction.action.ReadValue<Vector2>() : Vector2.zero; //no input when controls are blocked
            AnalogueMoveScale = inputDirection.magnitude; //input system has a normalise processor on the move input action
            if (CameraZoomController.FirstPerson)
            {
                Vector3 forward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
                Vector3 right = transform.right;
                WorldSpaceMoveDir = (forward * inputDirection.y + right * inputDirection.x).normalized;
                Rb.MoveRotation(Rb.rotation * Quaternion.Euler(0f, _cameraYawAccumulator, 0f));
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
        }


        //Unsitting
        if (Seat && ControlsEnabled && _jumpPressed)
        {
            Seat.CmdUnsitPlayer();

            CleanupFixedUpdate();
            return;
        }

        //Grounded
        int groundedHits = Physics.OverlapSphereNonAlloc(Rb.position, _groundedSphereRadius, _groundedCheckColliderBuffer, ~0, QueryTriggerInteraction.Ignore);
        bool grounded = false;
        for (int i = 0; i < groundedHits; i++)
        {
            // ignore self but *do* find other players
            //Check all parents
            bool self = false;
            Transform t = _groundedCheckColliderBuffer[i].transform;
            do
            {
                if (t == transform)
                {
                    self = true;
                    break;
                }
                t = t.parent;
            } while (t != null);

            if (!self)
            {
                //Player has collided with something other than themself
                grounded = true;
                break;
            }
        }

        bool groundedOnBumpy = Physics.CheckSphere(Rb.position, _groundedSphereRadius, LayerMask.GetMask("Bumpy"), QueryTriggerInteraction.Ignore);
        Rb.useGravity = !groundedOnBumpy;
        _networkAnimator.animator.SetBool(GroundedState, grounded || groundedOnBumpy);
        if (IsPuppet && !grounded && !groundedOnBumpy && PuppetGravityMultiplier > 1f)
        {
            Rb.AddForce(Physics.gravity * (PuppetGravityMultiplier - 1f), ForceMode.Acceleration);
        }

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
        if ((ControlsEnabled || IsPuppet) && (_jumpPressed || PuppetRequestJump) && (grounded || groundedOnBumpy))
        {
            _networkAnimator.animator.SetTrigger(JumpTrigger);
            float jumpMultiplier = IsPuppet ? PuppetJumpForceMultiplier : 1f;
            Rb.AddForce(Vector3.up * (_jumpForce * jumpMultiplier), ForceMode.Impulse);
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
        while (tries++ < maxTries)
        {
            var circularPos = Random.insideUnitCircle * radius;
            var attemptedPosition = cart.transform.TransformPoint(new Vector3(circularPos.x, 0.5f, circularPos.y));

            // collision check
            if (Physics.CheckSphere(attemptedPosition, 0.45f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            // grounded check
            if (!Physics.Raycast(attemptedPosition, Vector3.down, out _, 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            newPosition = attemptedPosition;
            break;
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
        _cameraYawAccumulator = 0.0f;
        PuppetRequestJump = false;
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

    private void OnCutsceneStarted(PlayableDirector _)
    {
        foreach (SkinnedMeshRenderer renderer in SkinnedRenderers)
        {
            renderer.enabled = CutscenePlayer;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = CutscenePlayer;
        }
        _nameplateCanvas.enabled = CutscenePlayer;
        Rb.isKinematic = !CutscenePlayer;
    }

    private void OnCutsceneStopped(PlayableDirector _)
    {
        foreach (SkinnedMeshRenderer renderer in SkinnedRenderers)
        {
            renderer.enabled = !CutscenePlayer;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = !CutscenePlayer;
        }
        _nameplateCanvas.enabled = !CutscenePlayer;
        Rb.isKinematic = CutscenePlayer;
    }

    private void OnDrawGizmos()
    {
        if (Rb)
        {
            Gizmos.DrawSphere(Rb.position, _groundedSphereRadius);
        }
    }
}