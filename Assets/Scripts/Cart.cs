using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    private struct FlaskSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private Rigidbody _rb;

    [ValidateInput("@gameObject.scene.isLoaded ? $value.Count > 0 : true", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [field: SerializeField] public List<Checkpoint> Checkpoints { get; private set; }

    [field: SerializeField] public int CurrentCheckpointIndex { get; private set; } = -1;

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;

    [SerializeField] [Required] private InputActionReference _devCheckpointBackAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointForwardAction;

    [Tooltip("Base amount of tilt-correct to apply. Higher reduces overall amount of tilting.")]
    [SerializeField] private float _tiltCorrection = 1.1f;

    [Tooltip("Exponent for how much the amount of tilt-correction increases in response to tilting. 1 means consistent, higher makes it kick in far more when tilting more.")]
    [SerializeField] private float _tiltCorrectionScaling = 2f;

    // UI
    private Transform _uiCanvas;

    // Flask carrying
    [SerializeField] [Required] private Collider _flaskBounds;
    private Dictionary<Flask, FlaskSnapshot>[] _flasksAtCheckpoint;
    public HashSet<Flask> CarriedFlasks = new();

    //sound for the cart
    public AK.Wwise.Event CarSound = new();
    public AK.Wwise.RTPC RTPCSpeed;

    //cart for when on surface
    public AK.Wwise.Event CarOnSurface = new();

    //sound for the flasks
    public AK.Wwise.Event glassInVehicle = new();
    public AK.Wwise.RTPC glassInDaVehicle = new();

    // The number of flasks we'll respawn with
    public int FlasksOnRespawn => _flasksAtCheckpoint[Mathf.Clamp(CurrentCheckpointIndex, 0, _flasksAtCheckpoint.Length - 1)].Count;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;

        _flasksAtCheckpoint = new Dictionary<Flask, FlaskSnapshot>[Checkpoints.Count];
        for (int i = 0; i < _flasksAtCheckpoint.Length; ++i)
        {
            _flasksAtCheckpoint[i] = new Dictionary<Flask, FlaskSnapshot>();
        }

        // First checkpoint runs on Frame 0 before flasks run OnTriggerEnter so we need to manually init
        // - Bounds check isn't perfectly accurate, but we can reasonably assume
        // there won't be flasks in the level that are both within the bounds of
        // the flask carrier on scene start yet not meant to be in the flask
        var allFlasks = FindObjectsByType<Flask>(FindObjectsSortMode.None);
        foreach (var flask in allFlasks)
        {
            if (_flaskBounds.bounds.Contains(flask.transform.position))
            {
                CarriedFlasks.Add(flask);
            }
        }
    }

    public override void OnStartServer()
    {
        Checkpoint.RespawnEvent.AddListener(OnRespawn);

        CarSound.Post(gameObject);
        glassInVehicle.Post(gameObject);
        CarOnSurface.Post(gameObject);
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

        //Car speed
        RTPCSpeed.SetGlobalValue(_rb.linearVelocity.magnitude*20);
        glassInDaVehicle.SetGlobalValue(CarriedFlasks.Count);
        
    }

    private void FixedUpdate()
    {
        // Re-center rotation around local Z axis
        var rot = Mathf.DeltaAngle(transform.eulerAngles.z, 0);
        var rotExp = Mathf.Sign(rot) * Mathf.Pow(Mathf.Abs(rot), _tiltCorrectionScaling);
        _rb.AddTorque(_tiltCorrection * rotExp * transform.forward);
    }

    // Records the local positions of all CarriedFlasks and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointFlasksSnapshot()
    {
        Debug.Log($"Capturing snapshot: {CarriedFlasks.Count} flasks carried, existing list has {_flasksAtCheckpoint[CurrentCheckpointIndex].Count} entries");

        _flasksAtCheckpoint[CurrentCheckpointIndex].Clear();

        Physics.SyncTransforms();
        foreach (Flask flask in CarriedFlasks)
        {
            _flasksAtCheckpoint[CurrentCheckpointIndex][flask] = new FlaskSnapshot
            {
                Position = transform.InverseTransformPoint(flask.transform.position),
                Rotation = flask.transform.rotation
            };
        }
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
                checkpointBanner.IsFirst = newIndex == 0;

                CaptureCheckpointFlasksSnapshot();
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
            flaskState.Key.transform.position = transform.TransformPoint(flaskState.Value.Position);
            flaskState.Key.transform.rotation = flaskState.Value.Rotation;
            Physics.SyncTransforms();
            
            flaskState.Key.RpcUnsmash();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdInvokeRespawnEvent(int newCheckpointIndex)
    {
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

        CurrentCheckpointIndex = newCheckpointIndex;

        // should only proc when using dev keys, otherwise will naturally be populated
        if (_flasksAtCheckpoint[CurrentCheckpointIndex].Count == 0)
        {
            CaptureCheckpointFlasksSnapshot();
        }

        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }
}