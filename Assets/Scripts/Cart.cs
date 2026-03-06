using System.Collections.Generic;
using System.Linq;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    [ValidateInput("@$value.Count > 0", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [SerializeField] private List<Checkpoint> _checkpoints;
    [field: SerializeField] public int CurrentCheckpointIndex { get; private set; }

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;
    
    [SerializeField] [Required] private InputActionReference _respawnAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointBackAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointForwardAction;

    [Tooltip("Whether to move the wheels using torque instead of flat forces. Should result in more consistent movement, but less tested.")]
    [field: SerializeField] [field: SyncVar] public bool UseNewTorqueSystem { get; private set; } = true;
    
    // Flask carrying
    [SerializeField] [Required] private Collider _flaskBounds;
    
    private Dictionary<GameObject, Vector3> _initialFlaskPositions = new();
    
    [field: SerializeField] [field: Sirenix.OdinInspector.ReadOnly] public int CarriedFlasks { get; private set; }
    
    [ValidateInput("@$value.Count > 0", "Cart doesn't have any flasks linked.", InfoMessageType.Warning)]
    [SerializeField] private List<GameObject> _trackedFlasks = new();
    
    public int MaxFlasks => _trackedFlasks.Count;
    
    // Ratio of flasks currently being carried
    public float FlasksRemainingRatio => (float)CarriedFlasks / _trackedFlasks.Count;

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
        
        foreach (var flask in _trackedFlasks)
        {
            _initialFlaskPositions[flask] = transform.InverseTransformPoint(flask.transform.position);
        }
    }
    
    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
    }
    
    private void Update()
    {
        if (_respawnAction.action.WasPressedThisFrame())
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex);
        }
        else if (_devCheckpointBackAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex - 1);
        }
        else if (_devCheckpointForwardAction.action.WasPressedThisFrame() && CurrentCheckpointIndex != _checkpoints.Count - 1)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex + 1);
        }

        // temp: debug, horrible, etc - swap between wheel force systems
        if (Keyboard.current.tKey.wasPressedThisFrame && isServer)
        {
            UseNewTorqueSystem = !UseNewTorqueSystem;
            Debug.Log($"UseNewTorqueSystem set to {UseNewTorqueSystem}");

            var wheels = GetComponentsInChildren<WheelSeat>();
            foreach (var wheel in wheels)
            {
                wheel.MoveForce *= !UseNewTorqueSystem ? 0.6f : 1 / 0.6f;
            }
        }
        
        // todo bad perf, should can probably just track in bounds trigger enter/exit
        CarriedFlasks = _trackedFlasks.Count(f => _flaskBounds.bounds.Contains(f.transform.position));
        if (isServer && CarriedFlasks == 0 && MaxFlasks > 0)
        {
            CmdInvokeRespawnEvent(CurrentCheckpointIndex);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            var newIndex = _checkpoints.IndexOf(checkpoint);
            
            if (newIndex > CurrentCheckpointIndex)
            {
                // New checkpoint reached
                CurrentCheckpointIndex = newIndex;
                Debug.Log($"Hit checkpoint {newIndex}: {checkpoint.AreaName}");

                var canvas = FindAnyObjectByType<Canvas>(); // todo we should probably have a global find object or similar for things like this. maybe tag it?
                var checkpointBanner = Instantiate(_checkpointBannerPrefab, canvas.transform);
                checkpointBanner.Checkpoint = checkpoint;
            }
        }
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        Transform newTransform = checkpoint.cartRespawnLocalTransform;
        gameObject.SetActive(false);
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            if (rb.isKinematic) continue;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = newTransform.position;
        transform.rotation = newTransform.rotation;
        gameObject.SetActive(true);
        
        ResetFlasks(true);
    }
    
    public void ResetFlasks(bool includeOutOfBounds = false)
    {
        foreach (var flask in _trackedFlasks)
        {
            if (includeOutOfBounds || _flaskBounds.bounds.Contains(flask.transform.position))
            {
                var rb = flask.GetComponent<Rigidbody>();
                rb.position = transform.TransformPoint(_initialFlaskPositions[flask]);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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
        CurrentCheckpointIndex = newCheckpointIndex;
        Checkpoint.respawnEvent.Invoke(_checkpoints[CurrentCheckpointIndex]);
    }
}