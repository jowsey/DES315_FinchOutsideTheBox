using System.Collections.Generic;
using System.Linq;
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
    private struct TreasureSnapshot
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

    [SerializeField] private int _lowTreasuresThreshold = 3;

    // UI
    private Transform _uiCanvas;

    // Treasure carrying
    [SerializeField] [Required] private Collider _treasureBounds;

    // Populated on server, unnecessary on clients
    private Dictionary<Treasure, TreasureSnapshot>[] _treasuresAtCheckpoint;
    public readonly HashSet<Treasure> CarriedTreasures = new();

    [field: SyncVar(hook = nameof(OnNumCarriedTreasuresChanged))] public int NumCarriedTreasures { get; private set; }
    public readonly SyncList<int> CheckpointTreasureCounts = new();

    //Sound effects
    [SerializeField] [Required] private AK.Wwise.Event _carSound;
    [SerializeField] [Required] private AK.Wwise.Event _carOnSurface;
    [SerializeField] [Required] private AK.Wwise.Event _glassInVehicle;
    [SerializeField] [Required] private AK.Wwise.RTPC _cartSpeedRTPC;
    [SerializeField] [Required] private AK.Wwise.RTPC _numCarriedTreasuresRTPC;

    [SerializeField] [Required] private WorldFollowUI _lowTreasureWarningPrefab;
    private WorldFollowUI _lowTreasureWarningUI;

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

        _treasuresAtCheckpoint = new Dictionary<Treasure, TreasureSnapshot>[Checkpoints.Count];
        for (int i = 0; i < _treasuresAtCheckpoint.Length; ++i)
        {
            _treasuresAtCheckpoint[i] = new Dictionary<Treasure, TreasureSnapshot>();
        }

        CheckpointTreasureCounts.AddRange(Enumerable.Repeat(-1, Checkpoints.Count).ToArray());

        // First checkpoint runs on Frame 0 before treasures run OnTriggerEnter so we need to manually init
        // - Bounds check isn't perfectly accurate, but we can reasonably assume
        // there won't be treasures in the level that are both within the bounds of
        // the treasure carrier on scene start yet not meant to be in the treasure
        var allTreasures = FindObjectsByType<Treasure>(FindObjectsSortMode.None);
        foreach (var treasure in allTreasures)
        {
            if (_treasureBounds.bounds.Contains(treasure.transform.position))
            {
                AddCarriedTreasure(treasure);
            }
        }

        CaptureCheckpointTreasuresSnapshot();
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
#if UNITY_EDITOR
        if (_devCheckpointBackAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex - 1);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != Checkpoints.Count - 1)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex + 1);
        }
#endif

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

    // Records the local positions of all CarriedTreasures and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointTreasuresSnapshot()
    {
        Debug.Log($"Capturing snapshot: {NumCarriedTreasures} treasures carried at checkpoint {CurrentCheckpointIndex}");

        Physics.SyncTransforms();
        foreach (Treasure treasure in CarriedTreasures)
        {
            _treasuresAtCheckpoint[CurrentCheckpointIndex][treasure] = new TreasureSnapshot
            {
                LocalPosition = transform.InverseTransformPoint(treasure.transform.position),
                Rotation = treasure.transform.rotation
            };
        }

        CheckpointTreasureCounts[CurrentCheckpointIndex] = NumCarriedTreasures;
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
                    CaptureCheckpointTreasuresSnapshot();
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

        ResetTreasures();
    }

    public void ResetTreasures()
    {
        if (!isServer) return;

        foreach (Treasure treasure in CarriedTreasures)
        {
            if (!_treasuresAtCheckpoint[CurrentCheckpointIndex].ContainsKey(treasure))
            {
                //This treasure is currently in the carrier but wasn't in the carrier when the checkpoint was reached, disable it instead of letting it smash
                treasure.State = Treasure.TreasureState.Inactive;
            }
        }

        foreach (KeyValuePair<Treasure, TreasureSnapshot> treasureState in _treasuresAtCheckpoint[CurrentCheckpointIndex])
        {
            treasureState.Key.transform.position = transform.TransformPoint(treasureState.Value.LocalPosition);
            treasureState.Key.transform.rotation = treasureState.Value.Rotation;
            Physics.SyncTransforms();

            treasureState.Key.State = Treasure.TreasureState.Idle;
        }
    }

    public void AddCarriedTreasure(Treasure treasure)
    {
        CarriedTreasures.Add(treasure);
        NumCarriedTreasures = CarriedTreasures.Count;
    }

    public void RemoveCarriedTreasure(Treasure treasure)
    {
        CarriedTreasures.Remove(treasure);
        NumCarriedTreasures = CarriedTreasures.Count;
    }

    private void OnNumCarriedTreasuresChanged(int oldValue, int newValue)
    {
        if (newValue <= _lowTreasuresThreshold && !_lowTreasureWarningUI)
        {
            //_lowTreasureWarningUI = Instantiate(_lowTreasureWarningPrefab, _uiCanvas);
            //_lowTreasureWarningUI.TrackingTarget = transform;
            //_lowTreasureWarningUI.TrackingOffset = new Vector3(0, 5.5f, 0);
            //_lowTreasureWarningUI.ApplyOffsetLocally = true;
            
            //_lowTreasureWarningUI.transform.SetAsFirstSibling(); // send to back layer
        }
        else if (newValue > _lowTreasuresThreshold && _lowTreasureWarningUI)
        {
            Destroy(_lowTreasureWarningUI.gameObject);
            _lowTreasureWarningUI = null;
        }

        _numCarriedTreasuresRTPC.SetGlobalValue(newValue);
    }

#if UNITY_EDITOR
    [Command(requiresAuthority = false)]
#else
    [Command]
#endif
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
        if (isServer && CheckpointTreasureCounts[CurrentCheckpointIndex] == -1)
        {
            CaptureCheckpointTreasuresSnapshot();
        }

        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }
}