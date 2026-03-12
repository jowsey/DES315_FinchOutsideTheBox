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
    private Rigidbody _rb;
    
    [ValidateInput("@$value.Count > 0", "Cart doesn't have any checkpoints linked.", InfoMessageType.Warning)]
    [SerializeField] private List<Checkpoint> _checkpoints;
    [field: SerializeField] public int CurrentCheckpointIndex { get; private set; }

    [SerializeField] [Required] private CheckpointBanner _checkpointBannerPrefab;
    
    [SerializeField] [Required] private InputActionReference _respawnAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointBackAction;
    [SerializeField] [Required] private InputActionReference _devCheckpointForwardAction;

    [Tooltip("Whether to move the wheels using torque instead of flat forces. Should result in more consistent movement, but less tested.")]
    [field: SerializeField] [field: SyncVar] public bool UseNewTorqueSystem { get; private set; } = true;

    [Tooltip("Base amount of tilt-correct to apply. Higher reduces overall amount of tilting.")]
    [SerializeField] private float _tiltCorrection = 1.1f;
    [Tooltip("Exponent for how much the amount of tilt-correction increases in response to tilting. 1 means consistent, higher makes it kick in far more when tilting more.")]
    [SerializeField] private float _tiltCorrectionScaling = 2f;
    
    // Flask carrying
    [SerializeField] [Required] private Collider _flaskBounds;
    private Dictionary<Flask, Vector3> _initialFlaskPositions = new();
    public List<Flask> CarriedFlasks = new();

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
    }
    
    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
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
    }

    private void FixedUpdate()
    {
        // Re-center rotation around local Z axis
        var rot = Mathf.DeltaAngle(transform.eulerAngles.z, 0);
        var rotExp = Mathf.Sign(rot) * Mathf.Pow(Mathf.Abs(rot), _tiltCorrectionScaling);
        _rb.AddTorque(_tiltCorrection * rotExp * transform.forward);
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
        
        ResetFlasks(false);
    }
    
    public void ResetFlasks(bool includeOutOfBounds = false)
    {
        foreach (var flask in CarriedFlasks)
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
        if (newCheckpointIndex < 0 || newCheckpointIndex >= _checkpoints.Count)
        {
            Debug.LogWarning($"Tried to respawn at invalid checkpoint index {newCheckpointIndex}");
            return;
        }

        CurrentCheckpointIndex = newCheckpointIndex;
        Checkpoint.respawnEvent.Invoke(_checkpoints[CurrentCheckpointIndex]);
    }
}