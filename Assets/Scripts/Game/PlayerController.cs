using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Items;
using Game.Items.Equipments;
using Mirror;
using Sirenix.OdinInspector;
using TMPro;
using UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using Util;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : NetworkBehaviour
{
    public class PlayerStatusEffect
    {
        public enum StatusEffectTarget
        {
            MoveSpeed
        }

        public enum StatusEffectType
        {
            Add,
            Multiply
        }

        public string DisplayName;
        public float StartTime;
        public float Duration;
        public StatusEffectTarget Target;
        public float Effect;
        public StatusEffectType Type;
        
        public PlayerStatusEffect(string name, float duration, StatusEffectTarget target, float effect, StatusEffectType type)
        {
            DisplayName = name;
            StartTime = Time.time;
            Duration = duration;
            Target = target;
            Effect = effect;
            Type = type;
        }

        public float GetProgress() => Mathf.Clamp01((Time.time - StartTime) / Duration);
    }
    
    private static readonly int RunningState = Animator.StringToHash("Running");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");
    private static readonly int GroundedState = Animator.StringToHash("Grounded");
    private static readonly int FallState = Animator.StringToHash("Fall");
    private static readonly int GlideState = Animator.StringToHash("Glide");
    private static readonly int RelativeRunSpeed = Animator.StringToHash("RelativeRunSpeed");

    public static SkinData[] LoadedSkins;

    public static PlayerController LocalPlayer { get; private set; }

    private static readonly HashSet<int> _playerRbIds = new();
    public static bool IsPlayerRb(int id) => _playerRbIds.Contains(id);

    [Header("Network")]
    [SyncVar] [ReadOnly] public int PlayerIndex;

    [SyncVar] [ReadOnly] public string PlayerUID;

    [SyncVar(hook = nameof(OnPlayerNameChanged))] [ReadOnly] public string PlayerName;
    [SyncVar] [ReadOnly] public int PlayerSkinIndex;

    [Header("Components")]
    public Rigidbody Rb { get; private set; }

    private NetworkAnimator _networkAnimator;
    [SerializeField] private Canvas _nameplateCanvas;
    [SerializeField] public TextMeshProUGUI PlayerNameText; //Public for cutscene puppeteering
    [SerializeField] public Image NameplateIcon;

    [Header("Animation")]
    [Tooltip("The minimum velocity required to initiate the gliding animation (should be negative)")]   
    [SerializeField] private float _fallAnimationMinDownardsVelocity;

    [Header("Input")]
    private bool _jumpPressed;
    
    public InputActionReference MoveAction;
    public InputActionReference JumpAction;
    public InputActionReference InteractAction;
    public InputActionReference DropItemAction;
    public InputActionReference UseItemAction;
    
    public WwiseAnimationEvents WwiseAnimationEvents { get; private set; }

    [Tooltip("Percentage of gravity to negate when gliding")]
    [SerializeField] [Range(0, 100)] private float _gravityNegationPercentage;

    [SerializeField] private float _rotationSmoothingSpeed;

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

    [SerializeField] [SuffixLabel("seconds")]
    private float _coyoteTime;
    
    [Header("State")]
    [ReadOnly] public WheelSeat Seat;

    [ReadOnly] public Item HeldObject;

    [field: SyncVar] [field: ShowInInspector] [field: ReadOnly] public Vector3 WorldSpaceMoveDir { get; private set; }
    [SyncVar] public float AnalogueMoveScale;

    [Header("Skin materials")]
    public Renderer[] SkinnedRenderers;

    private List<Vector3> _contactNormals = new();
    
    [field: SerializeField] public Transform HeldObjectPickupTarget { get; private set; }

    private float _lastGroundedTime;
    
    [SerializeField] private LayerMask _fanLifterMask;
    private bool _insideFanLifter;

    [SerializeField] private TrajectoryRenderer _trajectoryRendererPrefab;
    private TrajectoryRenderer _trajectoryRendererInstance;

    [SerializeField] private float _throwHoldMaxTime;
    [SerializeField] private float _throwDeadzone;
    
    private float _throwHeldTime;

    [SerializeField] private DecalProjector _dropShadowProjector;
    
    // Called when a player object is done being initially setup
    // Does NOT imply the player has just joined
    public static readonly UnityEvent<PlayerController> OnPlayerReady = new();

    //VFX
    [SerializeField] private GameObject _groundImpactVFX;
    [SerializeField] private GameObject _dustVFX;
    
    private bool _wasGrounded;

    //While there are any control blockers for a given action, that action will be blocked
    [Flags]
    public enum ControlBlockerFlags
    {
        None = 0,
        Move = 1 << 0,
        Jump = 1 << 1,
        Interact = 1 << 2,
        Look = 1 << 3,
        ChangePerspective = 1 << 4,
        CameraZoom = 1 << 5,
        Glide = 1 << 6,
        Pause = 1 << 7,
        Ping = 1 << 8,
        ToggleTextChat = 1 << 9,
        Respawn = 1 << 10,
        Emote = 1 << 11,
        All = ~0
    }
    
    private static readonly Dictionary<Object, ControlBlockerFlags> _controlBlockers = new();
    
    public static ControlBlockerFlags ActiveBlockers
    {
        get
        {
            ControlBlockerFlags flags = ControlBlockerFlags.None;
            foreach (ControlBlockerFlags flag in _controlBlockers.Values)
            {
                flags |= flag;
            }
            return flags;
        }
    }

    public static bool ControlEnabled(ControlBlockerFlags flag)
    {
        if (flag == ControlBlockerFlags.None) return true;
        return (ActiveBlockers & flag) == 0;
    }
    
    private static CinemachineInputAxisController _cinemachineInput;

    [SerializeField] private Transform _cameraObstructionDithererRayEndPosition;

    public bool PickupAllowed => ControlEnabled(ControlBlockerFlags.Interact) && !Seat && !HeldObject;
    public bool PutdownAllowed => ControlEnabled(ControlBlockerFlags.Interact) && HeldObject?.StateData is Item.HeldStateData;
    public bool UseAllowed => ControlEnabled(ControlBlockerFlags.Interact) && !Seat && HeldObject?.StateData is Item.HeldStateData && HeldObject is Equipment;
    public bool HookAllowed => ControlEnabled(ControlBlockerFlags.Interact) && !Seat && HeldObject?.StateData is Item.HeldStateData && HeldObject is YarnEquipment;

    public YarnSegment ActivePullSegment { get; private set; }
    private float _lastPullAckTime = -Mathf.Infinity;
    
    //Set in inspector to true if this player will only exist in cutscenes
    public bool CutscenePlayer;

    //Cutscene puppeting
    [HideInInspector] public Vector3 PuppetWorldSpaceMoveDir;
    [HideInInspector] public bool PuppetRequestJump;
    [HideInInspector] public float PuppetGravityMultiplier;
    [HideInInspector] public float PuppetJumpForceMultiplier;
    [HideInInspector] public bool IsPuppet;

    //Shop
    [ReadOnly] public Shop ActiveShop; //The shop the player is currently in
    
    public Emoter Emoter { get; private set; }

    private ControlReference _dropControlReference;
    private ControlReference _useControlReference;

    // Control blockers
    public static void AddControlBlockerFlags(Object blocker, ControlBlockerFlags flags)
    {
        if (_controlBlockers.TryGetValue(blocker, out ControlBlockerFlags existing))
        {
            _controlBlockers[blocker] = existing | flags;
        }
        else
        {
            _controlBlockers.Add(blocker, flags);
        }

        if (_cinemachineInput && _cinemachineInput.enabled && flags.HasFlag(ControlBlockerFlags.Look))
        {
            _cinemachineInput.enabled = false;
        }
    }

    public static void RemoveControlBlockerFlags(Object blocker, ControlBlockerFlags flags)
    {
        if (_controlBlockers.TryGetValue(blocker, out ControlBlockerFlags existing))
        {
            ControlBlockerFlags updated = existing & ~flags;
            if (updated == ControlBlockerFlags.None)
            {
                _controlBlockers.Remove(blocker);
            }
            else
            {
                _controlBlockers[blocker] = updated;
            }
        }

        if (_cinemachineInput && !_cinemachineInput.enabled && ControlEnabled(ControlBlockerFlags.Look))
        {
            _cinemachineInput.enabled = true;
        }
    }

    public static void RemoveAllControlBlockerFlags(Object blocker)
    {
        _controlBlockers.Remove(blocker);

        if (_cinemachineInput && !_cinemachineInput.enabled && ControlEnabled(ControlBlockerFlags.Look))
        {
            _cinemachineInput.enabled = true;
        }
    }

    public static void ClearAllControlBlockerFlags()
    {
        foreach (var blocker in _controlBlockers.Keys)
        {
            RemoveAllControlBlockerFlags(blocker);
        }

        _controlBlockers.Clear();
    }
    
    [SerializeField] private StatusEffectBox _statusEffectBoxPrefab;
    private Transform _statusEffectList;
    
    private Dictionary<PlayerStatusEffect, StatusEffectBox> _statusEffectBoxes = new();
    
    private List<PlayerStatusEffect> _activeStatusEffects = new();
    
    private Vector3 _throwDir => CameraZoomController.FirstPerson ? _camera.transform.forward : _cinemachineCamera.State.GetFinalOrientation() * Vector3.forward + Vector3.up * 0.3f;

    // Status effects
    public void AddStatusEffect(PlayerStatusEffect effect)
    {
        _activeStatusEffects.Add(effect);

        var box = Instantiate(_statusEffectBoxPrefab, _statusEffectList);
        box.Build(effect);

        _statusEffectBoxes[effect] = box;
    }

    private float EffectiveMoveForce => _activeStatusEffects
        .Where(e => e.Target == PlayerStatusEffect.StatusEffectTarget.MoveSpeed)
        .Aggregate(_moveForce, (acc, effect) =>
        {
            if (effect.Target != PlayerStatusEffect.StatusEffectTarget.MoveSpeed) return acc;
            return effect.Type switch
            {
                PlayerStatusEffect.StatusEffectType.Add => acc + effect.Effect,
                PlayerStatusEffect.StatusEffectType.Multiply => acc * effect.Effect,
                _ => acc
            };
        });

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
        Rb.sleepThreshold = 0.0f;
        _networkAnimator = GetComponent<NetworkAnimator>();
        WwiseAnimationEvents = GetComponentInChildren<WwiseAnimationEvents>(true);

        RespawnTarget.OnRespawn.AddListener(OnRespawn);

        PuppetGravityMultiplier = 1.0f;
        PuppetJumpForceMultiplier = 1.0f;
        IsPuppet = false;
        PuppetWorldSpaceMoveDir = Vector3.zero;
        PuppetRequestJump = false;

        Emoter = GetComponent<Emoter>();
        
        _statusEffectList = GameObject.FindGameObjectWithTag("StatusEffectList").transform;
    }

    private void Start()
    {
        // can't be in awake because camera has a NetworkIdentity meaning it's inactive until network ready
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
                puppeteer.SetPlayer2Name("Cat");
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
        name = $"Player ({PlayerName})";

        if (!CutscenePlayer)
        {
            NameplateIcon.sprite = LoadedSkins[PlayerSkinIndex].VCIcon;
            OnPlayerReady.Invoke(this);
        }
    }

    public override void OnStopClient()
    {
        if (!isLocalPlayer && !CutscenePlayer) PlayerPresenceFeed.OnPlayerLeave.Invoke(this);
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
        Highlight.SetHighlightable("Item", true);
        Highlight.SetHighlightable("TreasureCarrier", false);
        
        // Only local player gets a Wwise audio listener
        gameObject.AddComponent<AkAudioListener>();

        ObstructionDitherer.PlayerTransform = _cameraObstructionDithererRayEndPosition;

        // Item prompts
        _dropControlReference = GameObject.FindWithTag("DropItemControlReference").GetComponent<ControlReference>();
        _useControlReference = GameObject.FindWithTag("UseItemControlReference").GetComponent<ControlReference>();
        
        _dropControlReference.ToggleVisible(false, true);
        _useControlReference.ToggleVisible(false, true);
    }

    public override void OnStopLocalPlayer()
    {
        ActiveShop?.LeaveShop();
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        RespawnTarget.OnRespawn.RemoveListener(OnRespawn);

        PlayableDirector director = FindAnyObjectByType<PlayableDirector>();
        if (director)
        {
            director.played += OnCutsceneStarted;
            director.stopped += OnCutsceneStopped;
        }
    }

    private void OnRespawn(RespawnTarget target)
    {
        if (!isLocalPlayer) return;
        _cinemachineCamera.PreviousStateIsValid = false;
        ActiveShop?.LeaveShop();

        PruneStatusEffects(true);
        
        // Cart will take us with it
        if (Seat) return;

        Transform newTransform = target.PlayerSpawnPoints[PlayerIndex % target.PlayerSpawnPoints.Length];
        Rb.position = newTransform.position;
        Rb.rotation = newTransform.rotation;
        Rb.linearVelocity = Vector3.zero;
        Rb.angularVelocity = Vector3.zero;
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
        name = $"Player ({newValue})";

        if (PlayerNameText) PlayerNameText.text = newValue;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_nameplateCanvas.transform);
    }
    
    public Vector3 InputToWorldDir(Vector2 input)
    {
        if (CameraZoomController.FirstPerson)
        {
            Vector3 forward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 right = transform.right;
            return (forward * input.y + right * input.x).normalized;
        }
        else
        {
            Quaternion cameraOrientation = _cinemachineCamera ? _cinemachineCamera.State.GetFinalOrientation() : Quaternion.identity;
            Vector3 cameraForward = Vector3.Scale(cameraOrientation * Vector3.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 cameraRight = cameraOrientation * Vector3.right;

            return (cameraForward * input.y + cameraRight * input.x).normalized;
        }
    }

    private void Update()
    {
        if (!authority && !IsPuppet) return;

        //First-person controls
        if (CameraZoomController.FirstPerson && ControlEnabled(ControlBlockerFlags.Look))
        {
            float scale = (InputDeviceManager.CurrentInputType == InputDeviceManager.InputType.KeyboardMouse ? 0.01f : 0.1f);
            Vector2 scaledMouseDelta = _firstPersonLookAction.action.ReadValue<Vector2>() * (SettingsManager.ActiveSettings.FirstPersonSensPercent * scale);
            _cameraPitch = Mathf.Clamp(_cameraPitch - scaledMouseDelta.y, -89.0f, 89.0f);
            _cameraYawAccumulator += scaledMouseDelta.x;
        }
        
        _jumpPressed |= JumpAction.action.WasPressedThisFrame();

        if (InteractAction.action.WasPressedThisFrame() && InteractDetection.TargetedTransform)
        {
            if (UseAllowed && InteractDetection.TargetedTransform.CompareTag("YarnHookTarget") && HeldObject is YarnEquipment yarnEquipment)
            {
                // todo it sucks that we're hard-coding interaction types here
                
                if (yarnEquipment.IsHooking) return;
                if (InteractDetection.TargetedTransform.TryGetComponent<YarnHookPoint>(out var hookPoint))
                {
                    yarnEquipment.TryStartHook(hookPoint);
                }
            }
            else if (PickupAllowed)
            {
                if (InteractDetection.TargetedTransform.CompareTag("Item"))
                {
                    Item item = InteractDetection.TargetedTransform.GetComponentInParent<Item>();
                    if (item.StateData is not Item.IdleStateData) return;

                    item.CmdTryPickup();
                }
                else if (InteractDetection.TargetedTransform.CompareTag("Sack"))
                {
                    Item item = InteractDetection.TargetedTransform.GetComponentInParent<UpgradeSack>().StoredItem;
                    if (!item) return;

                    item.CmdTryPickup();
                }
            }
            else if (PutdownAllowed)
            {
                if (InteractDetection.TargetedTransform.CompareTag("TreasureCarrier"))
                {
                    var carrierTarget = InteractDetection.TargetedTransform.GetComponentInChildren<HeldObjectPutdownTarget>();
                    HeldObject.CmdTryPutdown(carrierTarget);
                }
                else if (InteractDetection.TargetedTransform.CompareTag("Sack"))
                {
                    var sack = InteractDetection.TargetedTransform.GetComponentInParent<UpgradeSack>();
                    HeldObject.CmdTryStore(sack);
                }
            }
        }
        else if (UseItemAction.action.WasPressedThisFrame() && UseAllowed && HeldObject is Equipment equipment)
        {
            equipment.TryUse();
        }
        else if (DropItemAction.action.WasPressedThisFrame())
        {
            if (PickupAllowed && InteractDetection.TargetedTransform && InteractDetection.TargetedTransform.TryGetComponent<YarnSegment>(out var segment))
            {
                segment.CmdDetachRope();
            }

            _throwHeldTime = 0f;
        }
        else if (DropItemAction.action.IsPressed() && PutdownAllowed)
        {
            _throwHeldTime += Time.deltaTime;

            if (_throwHeldTime > _throwDeadzone && !_trajectoryRendererInstance)
            {
                _trajectoryRendererInstance = Instantiate(_trajectoryRendererPrefab);
            }
            
            if (_trajectoryRendererInstance)
            {
                var impulseForce = Item.MaxThrowForce * Mathf.Clamp01(_throwHeldTime / _throwHoldMaxTime);
                _trajectoryRendererInstance.Build(HeldObject.Rb, _throwDir * impulseForce);
            }
        }
        else if (DropItemAction.action.WasReleasedThisFrame() && PutdownAllowed)
        {
            if (_throwHeldTime < _throwDeadzone)
            {
                HeldObject.CmdTryDrop();
            }
            else
            {
                HeldObject.CmdTryThrow(Mathf.Clamp01(_throwHeldTime / _throwHoldMaxTime), _throwDir);
            }

            if (_trajectoryRendererInstance)
            {
                Destroy(_trajectoryRendererInstance.gameObject);
                _trajectoryRendererInstance = null;
            }
        }

        // Rope pulling
        if (InteractAction.action.IsPressed() && PickupAllowed)
        {
            if (!ActivePullSegment && InteractDetection.TargetedTransform && InteractDetection.TargetedTransform.TryGetComponent<YarnSegment>(out var segment))
            {
                ActivePullSegment = segment;
                _lastPullAckTime = -Mathf.Infinity;
                
                AddControlBlockerFlags(this, ControlBlockerFlags.Move);
            }

            if (ActivePullSegment && Time.time - _lastPullAckTime > YarnSegment.MaxPullAckTimeout * 0.5f)
            {
                ActivePullSegment.CmdContinuePull();
                _lastPullAckTime = Time.time;
            }
        }
        else if (ActivePullSegment && !InteractAction.action.IsPressed())
        {
            ActivePullSegment.CmdStopPull();
            ActivePullSegment = null;
            _lastPullAckTime = -Mathf.Infinity;
            
            RemoveControlBlockerFlags(this, ControlBlockerFlags.Move);
        }
        
        // Prune completed status effects
        PruneStatusEffects();
        
        // Dynamic control references
        var showDrop = HeldObject;
        var showUse = HeldObject is Equipment { HasUseAbility: true };
        
        if (_dropControlReference && _useControlReference)
        {
            if (showDrop && !_dropControlReference.Visible)
                _dropControlReference.ToggleVisible(true);
            else if (!showDrop && _dropControlReference.Visible)
                _dropControlReference.ToggleVisible(false);

            if (showUse && !_useControlReference.Visible)
                _useControlReference.ToggleVisible(true);
            else if (!showUse && _useControlReference.Visible)
                _useControlReference.ToggleVisible(false);
        }
    }

    private void PruneStatusEffects(bool forceEndAll = false)
    {
        var completedEffects = forceEndAll
            ? _activeStatusEffects
            : _activeStatusEffects.Where(effect => Time.time - effect.StartTime >= effect.Duration);

        var boxesToRemove = _statusEffectBoxes.Where(kvp => completedEffects.Contains(kvp.Key)).ToList();
        foreach (var box in boxesToRemove)
        {
            _statusEffectBoxes.Remove(box.Key);
            box.Value.Destroy();
        }

        _activeStatusEffects.RemoveAll(e => completedEffects.Contains(e));
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
            if (Emoter && Emoter.IsEmoting)
            {
                _camera.transform.rotation = _firstPersonCameraViewPosition.rotation;
            }
            else
            {
                _camera.transform.rotation = transform.rotation * Quaternion.Euler(_cameraPitch, _cameraYawAccumulator, 0f);
            }
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
            } while (t);

            if (!self)
            {
                //Player has collided with something other than themselves
                grounded = true;
                break;
            }
        }

        if (grounded)
        {
            _dropShadowProjector.transform.position = Rb.position + Vector3.up * 0.1f;
        }
        else
        {
            if (Physics.Raycast(Rb.position + Vector3.up * 0.1f, Vector3.down, out var groundHit, 100f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
            {
                _dropShadowProjector.enabled = true;
                _dropShadowProjector.transform.position = groundHit.point + Vector3.up * (_dropShadowProjector.size.z * 0.5f);
            }
            else
            {
                _dropShadowProjector.enabled = false;
            }
        }
        
        // Ground vfx
        if (grounded && !_wasGrounded && !Seat && _groundImpactVFX)
        {
            var gv = Instantiate(_groundImpactVFX, Rb.position, Quaternion.identity);
            Destroy(gv, 1f);
            WwiseAnimationEvents.PlayDustCloud();
        }
        
        if (grounded && _networkAnimator.animator.GetBool(RunningState) && !Seat && _dustVFX)
        {
            var gv = Instantiate(_dustVFX, Rb.position, Quaternion.identity);
            Destroy(gv, 0.8f);
        }
        
        WwiseAnimationEvents.EnableFootsteps = !Seat && grounded;
        _wasGrounded = grounded;

        if (!authority && !IsPuppet) return;

        //Movement input
        if (IsPuppet)
        {
            if (PuppetWorldSpaceMoveDir.sqrMagnitude > 0)
            {
                Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(PuppetWorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * _rotationSmoothingSpeed));
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
            Vector2 inputDirection = ControlEnabled(ControlBlockerFlags.Move) ? MoveAction.action.ReadValue<Vector2>() : Vector2.zero; //no input when controls are blocked
            AnalogueMoveScale = inputDirection.magnitude; //input system has a normalise processor on the move input action
            
            WorldSpaceMoveDir = InputToWorldDir(inputDirection);
            if (CameraZoomController.FirstPerson)
            {
                Rb.MoveRotation(Rb.rotation * Quaternion.Euler(0f, _cameraYawAccumulator, 0f));
            }
            else
            {
                if (WorldSpaceMoveDir.sqrMagnitude > 0)
                {
                    Rb.MoveRotation(Quaternion.Slerp(Rb.rotation, Quaternion.LookRotation(WorldSpaceMoveDir, Vector3.up), Time.fixedDeltaTime * _rotationSmoothingSpeed));
                }
            }
            _networkAnimator.animator.SetBool(RunningState, WorldSpaceMoveDir.sqrMagnitude > 0);
        }
        
        //Unsitting
        if (Seat && ControlEnabled(ControlBlockerFlags.Jump) && _jumpPressed)
        {
            Seat.CmdUnsitPlayer();

            CleanupFixedUpdate();
            return;
        }

        _networkAnimator.animator.SetBool(GroundedState, grounded);
        if (IsPuppet && !grounded && PuppetGravityMultiplier > 1f)
        {
            Rb.AddForce(Physics.gravity * (PuppetGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        //Movement
        if (!Seat)
        {
            Vector3 delta = new Vector3(WorldSpaceMoveDir.x, 0.0f, WorldSpaceMoveDir.z) * (Time.fixedDeltaTime * EffectiveMoveForce * AnalogueMoveScale);
            foreach (Vector3 normal in _contactNormals)
            {
                if (Vector3.Dot(delta, normal) < 0) //moving into the surface
                {
                    delta = Vector3.ProjectOnPlane(delta, normal);
                }
            }

            //Only apply movement if there's an active movement delta (this is primarily to stop the player controller from fighting the emoter)
            if (delta.sqrMagnitude > 0)
            {
                Rb.MovePosition(Rb.position + delta);
            }
        }
        
        _networkAnimator.animator.SetFloat(RelativeRunSpeed, EffectiveMoveForce / _moveForce);

        bool isFalling = Rb.linearVelocity.y < _fallAnimationMinDownardsVelocity;

        //Player is falling - are they gliding?
        //todo ideally this should be if isFalling or is currently being pushed by a fan
        if (ControlEnabled(ControlBlockerFlags.Glide) && JumpAction.action.IsPressed() && (isFalling || _insideFanLifter))
        {
            //Player has extended cape
            if (isFalling)
            {
                //Player is falling
                float gravityNegationPercentage01 = _gravityNegationPercentage / 100.0f;
                Rb.AddForce(-Physics.gravity * gravityNegationPercentage01, ForceMode.Acceleration);
            }

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

        if (grounded)
        {
            _lastGroundedTime = Time.time;
        }

        //Jumping
        if (((ControlEnabled(ControlBlockerFlags.Jump) && _jumpPressed) || (IsPuppet && PuppetRequestJump)) && (grounded || Time.time - _lastGroundedTime < _coyoteTime))
        {
            _networkAnimator.animator.SetTrigger(JumpTrigger);
            float jumpMultiplier = IsPuppet ? PuppetJumpForceMultiplier : 1f;
            Rb.AddForce(Vector3.up * (_jumpForce * jumpMultiplier), ForceMode.Impulse);

            // don't allow double-jump
            _lastGroundedTime = 0;
        }

        CleanupFixedUpdate();
    }

    [Command]
    public void CmdReturnToCart()
    {
        if (Seat) return;

        const float radius = 6f;
        Vector3 newPosition = default;

        const int maxTries = 50;
        var tries = 0;
        while (tries++ < maxTries)
        {
            var circularPos = Random.insideUnitCircle * radius;
            var attemptedPosition = Cart.Instance.transform.TransformPoint(new Vector3(circularPos.x, 0.5f, circularPos.y));

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

        if (HeldObject) HeldObject.ServerSetState(new Item.IdleStateData());

        var newRotation = Quaternion.LookRotation(Cart.Instance.transform.position - newPosition, Vector3.up);
        newRotation = Quaternion.Euler(0, newRotation.eulerAngles.y, 0); // flatten angle
        GetComponent<NetworkTransformBase>().CmdTeleport(newPosition, newRotation);
    }

    private void CleanupFixedUpdate()
    {
        _jumpPressed = false;
        _cameraYawAccumulator = 0.0f;
        PuppetRequestJump = false;
        
        _contactNormals.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!authority) return;
        
        var isFanLifter = (_fanLifterMask.value & (1 << other.gameObject.layer)) != 0;
        if (isFanLifter) _insideFanLifter = true;

        WheelSeat newSeat = other.GetComponentInParent<WheelSeatTrigger>()?.Seat;
        if (newSeat && !Seat)
        {
            newSeat.CmdTrySitPlayer();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var isFanLifter = (_fanLifterMask.value & (1 << other.gameObject.layer)) != 0;
        if (isFanLifter) _insideFanLifter = false;
    }

    private void OnCutsceneStarted(PlayableDirector _)
    {
        foreach (SkinnedMeshRenderer smr in SkinnedRenderers) smr.enabled = CutscenePlayer;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = CutscenePlayer;
        
        _nameplateCanvas.enabled = CutscenePlayer;
        Rb.isKinematic = !CutscenePlayer;
    }

    private void OnCutsceneStopped(PlayableDirector _)
    {
        foreach (SkinnedMeshRenderer smr in SkinnedRenderers) smr.enabled = !CutscenePlayer;
        foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = !CutscenePlayer;
        
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