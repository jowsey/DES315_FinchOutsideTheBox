using Mirror;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ShowInInspector = Sirenix.OdinInspector.ShowInInspectorAttribute;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    private struct ObjectSnapshot
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
    //todo: a lot of the treasure and item separation can probs be consolidated into structures for the Holdable base class, ive just kept it separate for now because we have a lot of treasure specific logic
    private Dictionary<Treasure, ObjectSnapshot>[] _treasuresAtCheckpoint;
    public readonly HashSet<Treasure> CarriedTreasures = new();
    public readonly SyncDictionary<TreasureType, int> CarriedTreasureCounts = new SyncDictionary<TreasureType, int>(); //todo: this is maybe unnecessary, i just added it in case u (@jowsey) wanted to have some ui for the specific set of treasures the cart has
    [field: SyncVar(hook = nameof(OnTotalCarriedTreasuresChanged))] public int TotalCarriedTreasures { get; private set; }
    public readonly SyncList<int> CheckpointTotalTreasures = new();
    private Dictionary<Item, ObjectSnapshot>[] _itemsAtCheckpoint;
    public readonly HashSet<Item> CarriedItems = new();

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

#if UNITY_EDITOR
    private WheelSeat[] _wheelSeats;
    [SerializeField] private InputActionReference _alternateWheelMoveAction;
#endif

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _uiCanvas = GameObject.FindGameObjectWithTag("UICanvas").transform;
        IsPuppet = false;

#if UNITY_EDITOR
        _wheelSeats = GetComponentsInChildren<WheelSeat>();
#endif
    }

    public override void OnStartServer()
    {
        Checkpoint.RespawnEvent.AddListener(OnRespawn);

        _treasuresAtCheckpoint = new Dictionary<Treasure, ObjectSnapshot>[Checkpoints.Count];
        _itemsAtCheckpoint = new Dictionary<Item, ObjectSnapshot>[Checkpoints.Count];
        for (int i = 0; i < _treasuresAtCheckpoint.Length; ++i)
        {
            _treasuresAtCheckpoint[i] = new Dictionary<Treasure, ObjectSnapshot>();
            _itemsAtCheckpoint[i] = new Dictionary<Item, ObjectSnapshot>();
        }

        CheckpointTotalTreasures.AddRange(Enumerable.Repeat(-1, Checkpoints.Count).ToArray());

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

        CurrentCheckpointIndex = 0;
        Debug.Log($"Hit checkpoint 0: {Checkpoints[0].AreaName}");
        OnReachCheckpoint.Invoke(Checkpoints[0]);
        if (isServer)
        {
            CaptureCheckpointSnapshot();
        }
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

#if UNITY_EDITOR
        var altMove = _alternateWheelMoveAction.action.ReadValue<Vector2>();
        if (altMove.sqrMagnitude > 0)
        {
            var worldSpaceMoveDir = PlayerController.LocalPlayer.InputToWorldDir(altMove);
            foreach (var wheelSeat in _wheelSeats)
            {
                if (wheelSeat.SeatedPlayer) continue;
                wheelSeat.ApplyDrive(worldSpaceMoveDir, worldSpaceMoveDir.magnitude);
            }
        }
#endif
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

    // Records the local positions of all CarriedTreasures and CarriedItems and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointSnapshot()
    {
        Physics.SyncTransforms();
        
        foreach (Treasure treasure in CarriedTreasures)
        {
            _treasuresAtCheckpoint[CurrentCheckpointIndex][treasure] = new ObjectSnapshot
            {
                LocalPosition = transform.InverseTransformPoint(treasure.transform.position),
                Rotation = treasure.transform.rotation
            };
        }
        CheckpointTotalTreasures[CurrentCheckpointIndex] = TotalCarriedTreasures;

        foreach (Item item in CarriedItems)
        {
            _itemsAtCheckpoint[CurrentCheckpointIndex][item] = new ObjectSnapshot
            {
                LocalPosition = transform.InverseTransformPoint(item.transform.position),
                Rotation = item.transform.rotation
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

                OnReachCheckpoint.Invoke(checkpoint);

                if (isServer)
                {
                    CaptureCheckpointSnapshot();
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

        ResetObjects();
    }

    public void ResetObjects()
    {
        if (!isServer) return;

        List<Treasure> treasuresToDrop = CarriedTreasures.ToList();
        foreach (Treasure treasure in treasuresToDrop)
        {
            if (!_treasuresAtCheckpoint[CurrentCheckpointIndex].ContainsKey(treasure))
            {
                treasure.State = Holdable.HoldableState.Inactive;
                RemoveCarriedTreasure(treasure);
            }
        }

        foreach (KeyValuePair<Treasure, ObjectSnapshot> HoldableState in _treasuresAtCheckpoint[CurrentCheckpointIndex])
        {
            HoldableState.Key.transform.position = transform.TransformPoint(HoldableState.Value.LocalPosition);
            HoldableState.Key.transform.rotation = HoldableState.Value.Rotation;
            Physics.SyncTransforms();
            HoldableState.Key.State = Treasure.HoldableState.Idle;
        }

        List<Item> itemsToDrop = CarriedItems.ToList();
        foreach (Item item in itemsToDrop)
        {
            if (!_itemsAtCheckpoint[CurrentCheckpointIndex].ContainsKey(item))
            {
                //Don't need to set it to inactive as the shop will move it back to its correct place
                RemoveCarriedItem(item);
            }
        }

        foreach (KeyValuePair<Item, ObjectSnapshot> state in _itemsAtCheckpoint[CurrentCheckpointIndex])
        {
            state.Key.transform.position = transform.TransformPoint(state.Value.LocalPosition);
            state.Key.transform.rotation = state.Value.Rotation;
            Physics.SyncTransforms();
            state.Key.State = Holdable.HoldableState.Idle;
        }
    }

    public void AddCarriedTreasure(Treasure treasure)
    {
        CarriedTreasures.Add(treasure);
        TotalCarriedTreasures = CarriedTreasures.Count;
        if (CarriedTreasureCounts.ContainsKey(treasure.Type)) { CarriedTreasureCounts[treasure.Type]++; }
        else { CarriedTreasureCounts.Add(treasure.Type, 1); }
    }

    public void RemoveCarriedTreasure(Treasure treasure)
    {
        //Put behind if statement in case of double subtraction with CmdRemoveAllTreasures() setting the state which removes the collider which triggers the OnTriggerExit which calls this?
        if (CarriedTreasures.Remove(treasure))
        {
            TotalCarriedTreasures = CarriedTreasures.Count;
            if (CarriedTreasureCounts.ContainsKey(treasure.Type)) { CarriedTreasureCounts[treasure.Type]--; }
            if (CarriedTreasureCounts.ContainsKey(treasure.Type) && CarriedTreasureCounts[treasure.Type] <= 0) { CarriedTreasureCounts.Remove(treasure.Type); }
        }
    }

    private void OnTotalCarriedTreasuresChanged(int oldValue, int newValue)
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

    public void AddCarriedItem(Item item)
    {
        CarriedItems.Add(item);
    }

    public void RemoveCarriedItem(Item item)
    {
        CarriedItems.Remove(item);
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
        if (isServer && CheckpointTotalTreasures[CurrentCheckpointIndex] == -1)
        {
            CaptureCheckpointSnapshot();
        }

        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }

    [Server]
    public void RemoveAllTreasures()
    {
        //To prevent iterator invalidation from setting the state (which disables collider which runs OnTriggerExit which removes the treasure from Cart.CarriedTreasures)
        List<Treasure> treasuresToRemove = CarriedTreasures.ToList();
        foreach (Treasure treasure in treasuresToRemove)
        {
            //SyncVar hook hides the mesh, make it kinematic, and disable the collider
            treasure.State = Treasure.HoldableState.Inactive;
            RemoveCarriedTreasure(treasure);
        }
    }
}