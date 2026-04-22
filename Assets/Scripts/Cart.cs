using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ShowInInspector = Sirenix.OdinInspector.ShowInInspectorAttribute;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    private struct FlaskSnapshot
    {
        public Vector3 LocalPosition;
        public Quaternion Rotation;
    }

    public Rigidbody Rb;

    [ValidateInput("@gameObject.scene.isLoaded ? $value.Count > 0 : true", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [field: SerializeField] public List<Checkpoint> Checkpoints { get; private set; }

    [field: ShowInInspector] public int CurrentCheckpointIndex { get; private set; }

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;

    [SerializeField] [Required] private InputActionReference _devCheckpointBackAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointForwardAction;

    [Tooltip("Base amount of tilt-correct to apply. Higher reduces overall amount of tilting.")]
    [SerializeField] private float _tiltCorrection = 1.1f;

    [Tooltip("Exponent for how much the amount of tilt-correction increases in response to tilting. 1 means consistent, higher makes it kick in far more when tilting more.")]
    [SerializeField] private float _tiltCorrectionScaling = 2f;

    [SerializeField] private int _lowFlasksThreshold = 3;

    // UI
    private Transform _uiCanvas;

    // Flask carrying
    [SerializeField] [Required] private Collider _flaskBounds;

    // Populated on server, unnecessary on clients
    private Dictionary<Flask, FlaskSnapshot>[] _flasksAtCheckpoint;
    private readonly HashSet<Flask> _carriedFlasks = new();

    [field: SyncVar(hook = nameof(OnNumCarriedFlasksChanged))] public int NumCarriedFlasks { get; private set; }
    public readonly SyncList<int> CheckpointFlaskCounts = new();

    //Sound effects
    [SerializeField] [Required] private AK.Wwise.Event _carSound;
    [SerializeField] [Required] private AK.Wwise.Event _carOnSurface;
    [SerializeField] [Required] private AK.Wwise.Event _glassInVehicle;
    [SerializeField] [Required] private AK.Wwise.RTPC _cartSpeedRTPC;
    [SerializeField] [Required] private AK.Wwise.RTPC _numCarriedFlasksRTPC;

    [SerializeField] [Required] private WorldFollowUI _lowFlaskWarningPrefab;
    private WorldFollowUI _lowFlaskWarningUI;

    //Velocity doesn't exist on non-authed client, so we use this to calculate our own rough speed
    private Vector3 _positionLastFrame;

    // todo make non-static and check specific carts
    public static UnityEvent<Checkpoint> OnReachCheckpoint = new();

    public bool IsPuppet;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
        IsPuppet = false;
    }

    public override void OnStartServer()
    {
        Checkpoint.RespawnEvent.AddListener(OnRespawn);

        _flasksAtCheckpoint = new Dictionary<Flask, FlaskSnapshot>[Checkpoints.Count];
        for (int i = 0; i < _flasksAtCheckpoint.Length; ++i)
        {
            _flasksAtCheckpoint[i] = new Dictionary<Flask, FlaskSnapshot>();
        }

        CheckpointFlaskCounts.AddRange(new int[Checkpoints.Count]);

        // First checkpoint runs on Frame 0 before flasks run OnTriggerEnter so we need to manually init
        // - Bounds check isn't perfectly accurate, but we can reasonably assume
        // there won't be flasks in the level that are both within the bounds of
        // the flask carrier on scene start yet not meant to be in the flask
        var allFlasks = FindObjectsByType<Flask>(FindObjectsSortMode.None);
        foreach (var flask in allFlasks)
        {
            if (_flaskBounds.bounds.Contains(flask.transform.position))
            {
                AddCarriedFlask(flask);
            }
        }

        CaptureCheckpointFlasksSnapshot();
    }

    public override void OnStartClient()
    {
        _carSound.Post(gameObject);
        _carOnSurface.Post(gameObject);
        _glassInVehicle.Post(gameObject);

        _positionLastFrame = transform.position;
    }

    private void OnDestroy()
    {
        if (isServer) Checkpoint.RespawnEvent.RemoveListener(OnRespawn);
    }

    private void Update()
    {

        if (_devCheckpointBackAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex - 1);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != Checkpoints.Count - 1)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex + 1);
        }

        // manually calculate velocity since we don't have the luxury of knowing it on all clients
        var linearVelocity = (transform.position - _positionLastFrame) / Time.fixedDeltaTime;
        _positionLastFrame = transform.position;

        _cartSpeedRTPC.SetGlobalValue(linearVelocity.magnitude * 20);
    }

    private void FixedUpdate()
    {
        if (!isServer && !IsPuppet) return;
        // Re-center rotation around local Z axis
        var localWorldUp = transform.InverseTransformDirection(Vector3.up);
        var rollError = -Mathf.Atan2(localWorldUp.x, localWorldUp.y) * Mathf.Rad2Deg;
        var rotExp = Mathf.Sign(rollError) * Mathf.Pow(Mathf.Abs(rollError), _tiltCorrectionScaling);
        Rb.AddTorque(_tiltCorrection * rotExp * transform.forward);
    }

    // Records the local positions of all CarriedFlasks and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointFlasksSnapshot()
    {
        Debug.Log($"Capturing snapshot: {NumCarriedFlasks} flasks carried at checkpoint {CurrentCheckpointIndex}");

        Physics.SyncTransforms();
        foreach (Flask flask in _carriedFlasks)
        {
            _flasksAtCheckpoint[CurrentCheckpointIndex][flask] = new FlaskSnapshot
            {
                LocalPosition = transform.InverseTransformPoint(flask.transform.position),
                Rotation = flask.transform.rotation
            };
        }

        CheckpointFlaskCounts[CurrentCheckpointIndex] = NumCarriedFlasks;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            var newIndex = Checkpoints.IndexOf(checkpoint);

            if (newIndex > CurrentCheckpointIndex)
            {
                // New checkpoint reached
                CurrentCheckpointIndex = newIndex;
                Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");
                var checkpointBanner = Instantiate(_checkpointBannerPrefab, _uiCanvas.transform);
                checkpointBanner.Checkpoint = checkpoint;

                OnReachCheckpoint.Invoke(checkpoint);

                if (isServer)
                {
                    CaptureCheckpointFlasksSnapshot();
                }
            }
        }
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        if (!isServer) return;

        Transform newTransform = checkpoint.cartRespawnLocalTransform;

        var rbs = GetComponentsInChildren<Rigidbody>();
        var wasNonKinematic = new List<Rigidbody>();

        foreach (var rb in rbs)
        {
            if (rb.isKinematic) continue;

            rb.isKinematic = true;
            wasNonKinematic.Add(rb);
        }

        transform.position = newTransform.position;
        transform.rotation = newTransform.rotation;

        Physics.SyncTransforms();

        foreach (var rb in wasNonKinematic)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        ResetFlasks();
    }

    public void ResetFlasks()
    {
        if (!isServer) return;

        foreach (KeyValuePair<Flask, FlaskSnapshot> flaskState in _flasksAtCheckpoint[CurrentCheckpointIndex])
        {
            flaskState.Key.transform.position = transform.TransformPoint(flaskState.Value.LocalPosition);
            flaskState.Key.transform.rotation = flaskState.Value.Rotation;
            Physics.SyncTransforms();

            flaskState.Key.State = Flask.FlaskState.Idle;
        }
    }

    public void AddCarriedFlask(Flask flask)
    {
        _carriedFlasks.Add(flask);
        NumCarriedFlasks = _carriedFlasks.Count;
    }

    public void RemoveCarriedFlask(Flask flask)
    {
        _carriedFlasks.Remove(flask);
        NumCarriedFlasks = _carriedFlasks.Count;
    }

    private void OnNumCarriedFlasksChanged(int oldValue, int newValue)
    {
        if (newValue <= _lowFlasksThreshold && !_lowFlaskWarningUI)
        {
            //_lowFlaskWarningUI = Instantiate(_lowFlaskWarningPrefab, _uiCanvas);
            //_lowFlaskWarningUI.TrackingTarget = transform;
            //_lowFlaskWarningUI.TrackingOffset = new Vector3(0, 5.5f, 0);
            //_lowFlaskWarningUI.ApplyOffsetLocally = true;
            
            //_lowFlaskWarningUI.transform.SetAsFirstSibling(); // send to back layer
        }
        else if (newValue > _lowFlasksThreshold && _lowFlaskWarningUI)
        {
            Destroy(_lowFlaskWarningUI.gameObject);
            _lowFlaskWarningUI = null;
        }

        _numCarriedFlasksRTPC.SetGlobalValue(newValue);
    }

    [Command(requiresAuthority = false)]
    public void CmdInvokeRespawnEvent(int newCheckpointIndex)
    {
        // todo we can definitely simplify this a bunch, we've got clients subscribing to OnRespawn just to do isServer checks and such
        // also we should make respawn require authority and have the debug hotkeys be separate unauthed paths removed in prod builds
        RpcInvokeRespawnEvent(newCheckpointIndex);
    }

    [ClientRpc]
    private void RpcInvokeRespawnEvent(int newCheckpointIndex)
    {
        if (newCheckpointIndex < 0 || newCheckpointIndex >= Checkpoints.Count)
        {
            Debug.LogWarning($"Tried to respawn at invalid checkpoint index {newCheckpointIndex}");
            return;
        }

        // Respawning at a different checkpoint, almost certainly from dev hotkeys
        if (CurrentCheckpointIndex != newCheckpointIndex)
        {
            OnReachCheckpoint.Invoke(Checkpoints[CurrentCheckpointIndex]);
        }

        CurrentCheckpointIndex = newCheckpointIndex;

        // fallback for dev hotkeys, otherwise will naturally be populated
        if (isServer && CheckpointFlaskCounts[CurrentCheckpointIndex] == 0)
        {
            CaptureCheckpointFlasksSnapshot();
        }

        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }
}