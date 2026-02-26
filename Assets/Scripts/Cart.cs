using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb { get; private set; }
    [SerializeField] private FlaskCarrier _flaskCarrier;

    [SerializeField] private Checkpoint[] checkpoints;
    public int currentCheckpointIndex { get; private set; }

    [SerializeField] private InputActionReference respawnAction;
    [SerializeField] private InputActionReference dev_checkpointBackAction;
    [SerializeField] private InputActionReference dev_checkpointForwardAction;


    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _flaskCarrier = GetComponentInChildren<FlaskCarrier>();
        
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].index = i;
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
            if (checkpoint.index > currentCheckpointIndex)
            {
                currentCheckpointIndex = checkpoint.index;
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
    }

}