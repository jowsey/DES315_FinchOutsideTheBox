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

    // Treasure carrying
    [SerializeField] [Required] private Collider _carryBounds;

    // Populated on server, unnecessary on clients
    public readonly HashSet<Item> CarriedItems = new();

    [field: SyncVar(hook = nameof(OnTotalCarriedItemsChanged))] public int TotalCarriedItems { get; private set; }

    [field: SyncVar] public int TotalItemSellPrice { get; private set; }

    [field: SerializeField] public UpgradeSack SackPrefab { get; private set; }

    [field: SerializeField] public List<Transform> SackPositions { get; private set; } = new();

    public List<UpgradeSack> Sacks { get; private set; } = new();

    //Sound effects
    [SerializeField] [Required] private AK.Wwise.Event _carSound;
    [SerializeField] [Required] private AK.Wwise.Event _carOnSurface;
    [SerializeField] [Required] private AK.Wwise.Event _glassInVehicle;
    [SerializeField] [Required] private AK.Wwise.Event _collisionSfx;
    [SerializeField] [Required] private AK.Wwise.RTPC _cartSpeedRTPC;
    [SerializeField] [Required] private AK.Wwise.RTPC _numCarriedTreasuresRTPC;

    [SerializeField] private float _minimumCollisionMagnitudeForSfx = 2f;

    //Velocity doesn't exist on non-authed client, so we use this to calculate our own rough speed
    private Vector3 _positionLastFrame;

    public bool IsPuppet;

#if DEV_KEYS || UNITY_EDITOR
    private WheelSeat[] _wheelSeats;
    [SerializeField] private InputActionReference _alternateWheelMoveAction;
#endif

    private void Awake()
    {
        IsPuppet = false;
        Instance = this;

        foreach (var pos in SackPositions) pos.gameObject.SetActive(false);

#if DEV_KEYS || UNITY_EDITOR
        _wheelSeats = transform.parent.GetComponentsInChildren<WheelSeat>();
#endif
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RespawnTarget.OnReachNewTarget.AddListener(OnReachNewTarget);
        RespawnTarget.OnRespawn.AddListener(OnRespawn);

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
#if DEV_KEYS || UNITY_EDITOR
            var startIndex = Mathf.Clamp((SettingsManager.ActiveSettings?.Debug.StartingCheckpoint ?? 1) - 1, 0, Checkpoints.Count - 1);
#else
            var startIndex = 0;
#endif
            yield return new WaitUntil(() => Checkpoints[startIndex].isServer);

            Debug.Log($"Force-hit checkpoint {startIndex}: {Checkpoints[startIndex].AreaName}");
            SetActiveRespawnTarget(Checkpoints[startIndex]);
#if DEV_KEYS || UNITY_EDITOR
            ServerTeleportTo(Checkpoints[startIndex].CartSpawnPoint);
#endif
        }
    }

    public override void OnStartClient()
    {
        if (!isServer) RespawnTarget.OnReachNewTarget.AddListener(OnReachNewTarget);

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

    public override void OnStopClient()
    {
        base.OnStopClient();
        RespawnTarget.OnReachNewTarget.RemoveListener(OnReachNewTarget);
    }

    private bool GetPreviousRespawnTarget(RespawnTarget current, out RespawnTarget previousTarget)
    {
        if (current is Sandcastle sandcastle)
        {
            var siblingIndex = sandcastle.Parent.Sandcastles.IndexOf(sandcastle);
            previousTarget = siblingIndex > 0 ? sandcastle.Parent.Sandcastles[siblingIndex - 1] : sandcastle.Parent;
            return true;
        }

        if (current is Checkpoint checkpoint)
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

        Debug.LogWarning($"Current respawn target {current} is not a checkpoint or sandcastle, can't get previous");
        previousTarget = null;
        return false;
    }

    private bool GetNextRespawnTarget(RespawnTarget current, out RespawnTarget nextTarget)
    {
        if (current is Sandcastle sandcastle)
        {
            var siblingIndex = sandcastle.Parent.Sandcastles.IndexOf(sandcastle);
            if (siblingIndex < sandcastle.Parent.Sandcastles.Count - 1)
            {
                nextTarget = sandcastle.Parent.Sandcastles[siblingIndex + 1];
                return true;
            }

            var parentIndex = Checkpoints.IndexOf(sandcastle.Parent);
            nextTarget = parentIndex < Checkpoints.Count - 1 ? Checkpoints[parentIndex + 1] : null;
            return nextTarget;
        }

        if (current is Checkpoint checkpoint)
        {
            if (checkpoint.Sandcastles.Count > 0)
            {
                nextTarget = checkpoint.Sandcastles[0];
                return true;
            }

            var index = Checkpoints.IndexOf(checkpoint);
            nextTarget = index < Checkpoints.Count - 1 ? Checkpoints[index + 1] : null;
            return nextTarget;
        }

        Debug.LogWarning($"Current respawn target {current} is not a checkpoint or sandcastle, can't get next");
        nextTarget = null;
        return false;
    }

    private void Update()
    {
#if DEV_KEYS || UNITY_EDITOR
        if (SettingsManager.ActiveSettings?.Debug?.EnableDebugKeys == true)
        {
            if (_devCheckpointBackAction.action.WasPressedThisFrame() && GetPreviousRespawnTarget(CurrentRespawnTarget, out var prevTarget))
            {
                CmdInvokeRespawnEvent(prevTarget);
            }
            else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && GetNextRespawnTarget(CurrentRespawnTarget, out var nextTarget))
            {
                CmdInvokeRespawnEvent(nextTarget);
            }
        }
#endif

        // manually calculate velocity since we don't have the luxury of knowing it on all clients
        var linearVelocity = (transform.position - _positionLastFrame) / Time.fixedDeltaTime;
        _positionLastFrame = transform.position;

        _cartSpeedRTPC.SetGlobalValue(linearVelocity.magnitude * 20);

#if DEV_KEYS || UNITY_EDITOR
        if (isServer && SettingsManager.ActiveSettings?.Debug?.EnableDebugKeys == true && PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Move))
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
        var correctionMultiplier = Mathf.Max(0, localWorldUp.y);
        // correctionMultiplier = Mathf.SmoothStep(0f, 1f, uprightAmount * 2f); 
        var rollError = -Mathf.Atan2(localWorldUp.x, localWorldUp.y) * Mathf.Rad2Deg;
        var rotExp = Mathf.Sign(rollError) * Mathf.Pow(Mathf.Abs(rollError), _tiltCorrectionScaling);
        Rb.AddTorque(_tiltCorrection * rotExp * correctionMultiplier * transform.forward);
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (!isServer) return;

        var layerName = LayerMask.LayerToName(collision.gameObject.layer);
        if (layerName is "Player" or "Item") return;

        var normalVelocity = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.contacts[0].normal));
        if (normalVelocity < _minimumCollisionMagnitudeForSfx) return;

        // Debug.Log($"Collided with {collision.gameObject.name} at {normalVelocity} m/s");

        RpcPlayCollisionSfx();
    }

    [ClientRpc]
    private void RpcPlayCollisionSfx()
    {
        _collisionSfx?.Post(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;

        if (!other.CompareTag("Checkpoint")) return;

        var checkpoint = other.GetComponent<Checkpoint>();
        var newIndex = Checkpoints.IndexOf(checkpoint);

        var currentIndex = CurrentRespawnTarget switch
        {
            Checkpoint currentCheckpoint => Checkpoints.IndexOf(currentCheckpoint),
            Sandcastle currentSandcastle => Checkpoints.IndexOf(currentSandcastle.Parent),
            _ => -1
        };

        if (newIndex <= currentIndex) return;
        Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");

        // New checkpoint reached!
        SetActiveRespawnTarget(checkpoint);
    }

    private void OnRespawn(RespawnTarget target)
    {
        if (!isServer) return;

        ServerTeleportTo(target.CartSpawnPoint);
    }

    [Server]
    public void ServerTeleportTo(Transform target)
    {
        var chassis = transform;
        var parent = chassis.parent;

        var rbs = parent.GetComponentsInChildren<Rigidbody>();
        var parentRelativePositions = rbs.Select(rb => chassis.InverseTransformPoint(rb.transform.position)).ToList();

        transform.position = target.position;
        transform.rotation = target.rotation;

        for (var i = 0; i < parentRelativePositions.Count; i++)
        {
            var relativePosition = parentRelativePositions[i];
            var rb = rbs[i];

            if (!rb) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.transform.position = chassis.TransformPoint(relativePosition);
        }

        Physics.SyncTransforms();
    }

    [Server]
    public void AddCarriedItem(Item item)
    {
        CarriedItems.Add(item);
        TotalCarriedItems = CarriedItems.Count;

        // we sync this since we don't sync individual items
        ReevaluateTotalItemSellPrice();
    }

    [Server]
    public void RemoveCarriedItem(Item item)
    {
        CarriedItems.Remove(item);
        TotalCarriedItems = CarriedItems.Count;

        ReevaluateTotalItemSellPrice();
    }

    [Server]
    public void ReevaluateTotalItemSellPrice()
    {
        var allTreasures = CarriedItems.OfType<Treasure>().Concat(Sacks.Select(sack => sack.StoredItem).OfType<Treasure>());
        TotalItemSellPrice = allTreasures.Sum(treasure => treasure.Data.SellPrice);
    }

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
        RespawnTarget.OnReachNewTarget.Invoke(target); // ensure we immediately run on server
        RpcReachTarget(target);
    }

    [ClientRpc]
    private void RpcReachTarget(RespawnTarget target)
    {
        if (isServer) return; // mitigate host mode double proc
        RespawnTarget.OnReachNewTarget.Invoke(target);
    }

    private void OnReachNewTarget(RespawnTarget target)
    {
        if (target is Checkpoint checkpoint && Checkpoints.IndexOf(checkpoint) > 0)
        {
            var checkpointBanner = Instantiate(_checkpointBannerPrefab, UIGlobals.MainCanvas.transform);
            checkpointBanner.Checkpoint = checkpoint;

            checkpoint.ActivateVFX();

            if (!HintPrompt.HasShown.ReachCheckpoint)
            {
                HintPrompt.HasShown.ReachCheckpoint = true;
                HintPrompt.RequestNew(new HintPrompt.HintPromptData
                {
                    Title = "A place to rest?",
                    Description = "It's important to rest and take stock!\n\n" +
                                  "Checkpoints save your progress: the items and upgrades you had are always returned to you upon respawning."
                });
            }
        }

        if (!isServer) return;
        Physics.SyncTransforms();

        var snapshot = new RespawnTarget.RespawnSnapshot();
        RespawnTarget.OnBuildRespawnSnapshot.Invoke(snapshot);

        CurrentRespawnTarget.Snapshot = snapshot;
        CurrentRespawnTarget.NumCarriedItemsOnReach = TotalCarriedItems;
    }

#if DEV_KEYS || UNITY_EDITOR
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
        if (isServer && CurrentRespawnTarget != target)
        {
            SetActiveRespawnTarget(target);
        }

        RespawnTarget.OnPreRespawn.Invoke(target);
        RespawnTarget.OnRespawn.Invoke(target);
        RespawnTarget.OnPostRespawn.Invoke(target);
    }
}