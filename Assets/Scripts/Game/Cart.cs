using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Items;
using Game.Items.Equipments;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb;

    public static Cart Instance { get; private set; }

    [ValidateInput("@gameObject.scene.isLoaded ? $value.Count > 0 : true", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [field: SerializeField] public List<Checkpoint> Checkpoints { get; private set; }

    [field: SyncVar] public RespawnTarget CurrentRespawnTarget { get; private set; }

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
    public readonly HashSet<Item> CarriedItems = new();

    [field: SyncVar(hook = nameof(OnTotalCarriedItemsChanged))] public int TotalCarriedItems { get; private set; }

    [SyncVar] public int ExpectedTotalItemSellPrice;

    [field: SerializeField] public UpgradeSack SackPrefab { get; private set; }

    [field: SerializeField] public List<Transform> SackPositions { get; private set; } = new();

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
        Instance = this;

        foreach (var pos in SackPositions) pos.gameObject.SetActive(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        _wheelSeats = GetComponentsInChildren<WheelSeat>();
#endif
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RespawnTarget.OnRespawn.AddListener(OnRespawn);
        RespawnTarget.OnReachNewTarget.AddListener(OnReachNewTarget);

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

        StartCoroutine(SetInitialRespawnTarget());
        return;

        // Cart may have spawned before first checkpoint on the server,
        // wait for it, since the snapshot code sets syncvars and whatnot
        IEnumerator SetInitialRespawnTarget()
        {
            yield return new WaitUntil(() => Checkpoints[0].isServer);

            Debug.Log($"Hit checkpoint 0: {Checkpoints[0].AreaName}");
            SetActiveRespawnTarget(Checkpoints[0]);
        }
    }

    public override void OnStartClient()
    {
        _carSound.Post(gameObject);
        _carOnSurface.Post(gameObject);
        _glassInVehicle.Post(gameObject);

        _positionLastFrame = transform.position;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
        RespawnTarget.OnReachNewTarget.RemoveListener(OnReachNewTarget);
    }

    private bool GetPreviousRespawnTarget(RespawnTarget target, out RespawnTarget previousTarget)
    {
        if (target is Sandcastle sandcastle)
        {
            var siblingIndex = sandcastle.Parent.Sandcastles.IndexOf(sandcastle);
            previousTarget = siblingIndex > 0 ? sandcastle.Parent.Sandcastles[siblingIndex - 1] : sandcastle.Parent;
            return true;
        }

        if (target is Checkpoint checkpoint)
        {
            var index = Checkpoints.IndexOf(checkpoint);
            if (index > 0)
            {
                var previousCheckpoint = Checkpoints[index - 1];
                previousTarget = previousCheckpoint.Sandcastles.Count > 0 ? previousCheckpoint.Sandcastles[^1] : previousCheckpoint;
                return true;
            }

            previousTarget = null;
            return false;
        }

        Debug.LogWarning($"Current respawn target {target} is not a checkpoint or sandcastle, can't get previous");
        previousTarget = null;
        return false;
    }

    private bool GetNextRespawnTarget(RespawnTarget target, out RespawnTarget nextTarget)
    {
        if (target is Sandcastle sandcastle)
        {
            var siblingIndex = sandcastle.Parent.Sandcastles.IndexOf(sandcastle);
            if (siblingIndex < sandcastle.Parent.Sandcastles.Count - 1)
            {
                nextTarget = sandcastle.Parent.Sandcastles[siblingIndex + 1];
                return true;
            }

            var parentIndex = Checkpoints.IndexOf(sandcastle.Parent);
            nextTarget = parentIndex < Checkpoints.Count - 1 ? Checkpoints[parentIndex + 1] : null;
            return true;
        }

        if (target is Checkpoint checkpoint)
        {
            if (checkpoint.Sandcastles.Count > 0)
            {
                nextTarget = checkpoint.Sandcastles[0];
                return true;
            }

            var index = Checkpoints.IndexOf(checkpoint);
            nextTarget = index < Checkpoints.Count - 1 ? Checkpoints[index + 1] : null;
            return true;
        }

        Debug.LogWarning($"Current respawn target {target} is not a checkpoint or sandcastle, can't get next");
        nextTarget = null;
        return false;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_devCheckpointBackAction.action.WasPressedThisFrame() && GetPreviousRespawnTarget(CurrentRespawnTarget, out var prevTarget))
        {
            CmdInvokeRespawnEvent(prevTarget);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && GetNextRespawnTarget(CurrentRespawnTarget, out var nextTarget))
        {
            CmdInvokeRespawnEvent(nextTarget);
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            var newIndex = Checkpoints.IndexOf(checkpoint);

            var currentIndex = CurrentRespawnTarget switch
            {
                Checkpoint currentCheckpoint => Checkpoints.IndexOf(currentCheckpoint),
                Sandcastle currentSandcastle => Checkpoints.IndexOf(currentSandcastle.Parent),
                _ => -1
            };

            if (newIndex <= currentIndex) return;
            Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");

            // New checkpoint reached
            if (isServer)
            {
                SetActiveRespawnTarget(checkpoint);
            }

            var checkpointBanner = Instantiate(_checkpointBannerPrefab, _uiCanvas.transform);
            checkpointBanner.Checkpoint = checkpoint;
        }
    }

    private void OnRespawn(RespawnTarget target)
    {
        if (!isServer) return;

        Transform newTransform = target.CartSpawnPoint;

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

    [Server]
    public void SetActiveRespawnTarget(RespawnTarget target)
    {
        var newCheckpoint = target switch
        {
            Checkpoint checkpoint => checkpoint,
            Sandcastle sandcastle => sandcastle.Parent,
            _ => null
        };

        if (!newCheckpoint)
        {
            Debug.LogWarning($"Can't set respawn target to {target}, invalid");
            return;
        }

        CurrentRespawnTarget = target;
        RespawnTarget.OnReachNewTarget.Invoke(newCheckpoint);
    }

    private void OnReachNewTarget(RespawnTarget target)
    {
        Physics.SyncTransforms();

        var snapshot = new RespawnTarget.RespawnSnapshot();
        RespawnTarget.OnBuildRespawnSnapshot.Invoke(snapshot);

        CurrentRespawnTarget.Snapshot = snapshot;
        CurrentRespawnTarget.NumCarriedItemsOnReach = TotalCarriedItems;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Command(requiresAuthority = false)]
#else
    [Command]
#endif
    public void CmdInvokeRespawnEvent(RespawnTarget target)
    {
        RpcInvokeRespawnEvent(target);
    }

    [ClientRpc]
    private void RpcInvokeRespawnEvent(RespawnTarget target)
    {
        if (target is Checkpoint checkpoint && !Checkpoints.Contains(checkpoint))
        {
            Debug.LogWarning($"Tried to respawn at unregistered checkpoint {target}!");
            return;
        }

        // Respawning at a different target, mainly from dev hotkeys
        if (CurrentRespawnTarget != target)
        {
            SetActiveRespawnTarget(target);
        }

        RespawnTarget.OnPreRespawn.Invoke(target);
        RespawnTarget.OnRespawn.Invoke(target);
        RespawnTarget.OnPostRespawn.Invoke(target);
    }

    [Server]
    public void RemoveAllTreasures()
    {
        //To prevent iterator invalidation from setting the state (which disables collider which runs OnTriggerExit which removes the treasure from Cart.CarriedTreasures)
        List<Treasure> treasuresToRemove = CarriedItems.OfType<Treasure>().ToList();
        foreach (Treasure treasure in treasuresToRemove)
        {
            //SyncVar hook hides the mesh, make it kinematic, and disable the collider
            treasure.StateData = new Item.InactiveStateData();
            RemoveCarriedItem(treasure);
        }
    }
}