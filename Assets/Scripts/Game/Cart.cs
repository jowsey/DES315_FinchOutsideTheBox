using System.Collections.Generic;
using System.Linq;
using Game.Items;
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

    // UI
    private Transform _uiCanvas;

    // Treasure carrying
    [SerializeField] [Required] private Collider _carryBounds;

    // Populated on server, unnecessary on clients
    private Dictionary<Item, ObjectSnapshot>[] _checkpointSnapshots;
    public readonly HashSet<Item> CarriedItems = new();
    public readonly SyncList<int> NumItemsAtCheckpoint = new();
    [field: SyncVar(hook = nameof(OnTotalCarriedItemsChanged))] public int TotalCarriedItems { get; private set; }

    [SyncVar] public int ExpectedTotalItemSellPrice;
    
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        _checkpointSnapshots = new Dictionary<Item, ObjectSnapshot>[Checkpoints.Count];
        for (int i = 0; i < _checkpointSnapshots.Length; ++i)
        {
            _checkpointSnapshots[i] = new Dictionary<Item, ObjectSnapshot>();
        }

        NumItemsAtCheckpoint.AddRange(Enumerable.Repeat(-1, Checkpoints.Count).ToArray());

        // First checkpoint runs on Frame 0 before treasures run OnTriggerEnter so we need to manually init
        // - Bounds check isn't perfectly accurate, but we can reasonably assume
        // there won't be treasures in the level that are both within the bounds of
        // the treasure carrier on scene start yet not meant to be in the treasure
        var allItems = FindObjectsByType<Item>(FindObjectsSortMode.None);
        foreach (var item in allItems)
        {
            if (_carryBounds.bounds.Contains(item.transform.position))
            {
                AddCarriedItem(item);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Move))
        {
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

    // Records the local positions of all CarriedItems and writes them to the current checkpoint's snapshot
    private void CaptureCheckpointSnapshot()
    {
        Physics.SyncTransforms();

        foreach (Item item in CarriedItems)
        {
            _checkpointSnapshots[CurrentCheckpointIndex][item] = new ObjectSnapshot
            {
                LocalPosition = transform.InverseTransformPoint(item.transform.position),
                Rotation = item.transform.rotation
            };
        }

        NumItemsAtCheckpoint[CurrentCheckpointIndex] = TotalCarriedItems;
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

        RevertItemsToCurrentSnapshot();
    }

    public void RevertItemsToCurrentSnapshot()
    {
        if (!isServer) return;

        List<Item> itemsToDrop = CarriedItems.ToList();
        foreach (Item item in itemsToDrop)
        {
            if (!_checkpointSnapshots[CurrentCheckpointIndex].ContainsKey(item))
            {
                item.State = Item.ItemState.Inactive;
                RemoveCarriedItem(item);
            }
        }

        foreach (var (item, snapshot) in _checkpointSnapshots[CurrentCheckpointIndex])
        {
            item.transform.position = transform.TransformPoint(snapshot.LocalPosition);
            item.transform.rotation = snapshot.Rotation;
            Physics.SyncTransforms();
            item.State = Item.ItemState.Idle;
        }
    }

    [Server]
    public void AddCarriedItem(Item item)
    {
        CarriedItems.Add(item);
        TotalCarriedItems = CarriedItems.Count;

        // we sync this since we don't sync individual items
        ExpectedTotalItemSellPrice = EvaluateTotalItemSellPrice();
    }

    [Server]
    public void RemoveCarriedItem(Item item)
    {
        CarriedItems.Remove(item);
        TotalCarriedItems = CarriedItems.Count;

        ExpectedTotalItemSellPrice = EvaluateTotalItemSellPrice();
    }

    [Server]
    public int EvaluateTotalItemSellPrice() => CarriedItems.Sum(item => item.Data.SellPrice);

    private void OnTotalCarriedItemsChanged(int oldValue, int newValue)
    {
        _numCarriedTreasuresRTPC.SetGlobalValue(newValue);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        if (isServer && NumItemsAtCheckpoint[CurrentCheckpointIndex] == -1)
        {
            CaptureCheckpointSnapshot();
        }

        Checkpoint.RespawnEvent.Invoke(Checkpoints[CurrentCheckpointIndex]);
    }

    [Server]
    public void RemoveAllTreasures()
    {
        //To prevent iterator invalidation from setting the state (which disables collider which runs OnTriggerExit which removes the treasure from Cart.CarriedTreasures)
        List<Treasure> treasuresToRemove = CarriedItems.OfType<Treasure>().ToList();
        foreach (Treasure treasure in treasuresToRemove)
        {
            //SyncVar hook hides the mesh, make it kinematic, and disable the collider
            treasure.State = Item.ItemState.Inactive;
            RemoveCarriedItem(treasure);
        }
    }
}