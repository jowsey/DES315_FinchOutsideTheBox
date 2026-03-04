using Mirror;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb { get; private set; }
    [SerializeField] private FlaskCarrier _flaskCarrier;

    public Checkpoint[] checkpoints;
    [field: SerializeField] public int currentCheckpointIndex { get; private set; }

    [SerializeField] private InputActionReference respawnAction;
    [SerializeField] private InputActionReference dev_checkpointBackAction;
    [SerializeField] private InputActionReference dev_checkpointForwardAction;

    [Tooltip("Whether to move the wheels using torque instead of flat forces. Should result in more consistent movement, but less tested.")]
    [field: SerializeField] [field: SyncVar] public bool UseNewTorqueSystem { get; private set; } = true;

    [SerializeField] private CheckpointBanner _checkpointBannerPrefab;
    
    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _flaskCarrier = GetComponentInChildren<FlaskCarrier>();
        
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].index = i;
            if (string.IsNullOrWhiteSpace(checkpoints[i].AreaName))
            {
                checkpoints[i].AreaName = $"Unnamed Checkpoint {i}";
            }
        }
    }

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
    }
    
    private void OnDestroy()
    {
        Checkpoint.respawnEvent.RemoveListener(OnRespawn);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            Checkpoint checkpoint = other.GetComponent<Checkpoint>();
            Debug.Log($"Hit checkpoint {checkpoint.index}");
            if (checkpoint.index > currentCheckpointIndex)
            {
                // New checkpoint reached
                currentCheckpointIndex = checkpoint.index;

                var canvas = FindAnyObjectByType<Canvas>(); // todo we should probably have a global find object or similar for things like this. maybe tag it?
                var checkpointBanner = Instantiate(_checkpointBannerPrefab, canvas.transform);
                checkpointBanner.Checkpoint = checkpoint;
            }
        }
    }

    void OnRespawn(Checkpoint checkpoint)
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
        
        if (_flaskCarrier) _flaskCarrier.ResetFlasks(true);
    }

    private void Update()
    {
        if (respawnAction.action.WasPressedThisFrame())
        {
            CmdInvokeRespawnEvent(currentCheckpointIndex);
        }
        else if (dev_checkpointBackAction.action.WasPressedThisFrame() && currentCheckpointIndex != 0)
        {
            CmdInvokeRespawnEvent(currentCheckpointIndex - 1);
        }
        else if (dev_checkpointForwardAction.action.WasPressedThisFrame() && currentCheckpointIndex != checkpoints.Length - 1)
        {
            CmdInvokeRespawnEvent(currentCheckpointIndex + 1);
        }
    }

    [Command(requiresAuthority = false)]
    void CmdInvokeRespawnEvent(int newCheckpointIndex)
    {
        RpcInvokeRespawnEvent(newCheckpointIndex);
    }

    [ClientRpc]
    void RpcInvokeRespawnEvent(int newCheckpointIndex)
    {
        currentCheckpointIndex = newCheckpointIndex;
        Checkpoint.respawnEvent.Invoke(checkpoints[currentCheckpointIndex]);
        
        // Test that reaching a checkpoint works when we get there
        // Checkpoint.respawnEvent.Invoke(checkpoints[newCheckpointIndex]);
    }
}