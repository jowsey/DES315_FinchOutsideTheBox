using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb { get; private set; }

    [SerializeField] private Checkpoint[] checkpoints;
    public int currentCheckpointIndex { get; private set; }

    [SerializeField] private InputActionReference respawnAction;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].index = i;
        }
    }

    private void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Checkpoint")
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
        if (isServer)
        {
            Transform newTransform = checkpoint.cartRespawnLocalTransform;
            transform.position = newTransform.position;
            transform.rotation = newTransform.rotation;
        }
    }

    private void Update()
    {
        if (respawnAction.action.WasPressedThisFrame())
        {
            CmdInvokeRespawnEvent();
        }
    }

    [Command]
    void CmdInvokeRespawnEvent()
    {
        RpcInvokeRespawnEvent();
    }

    void RpcInvokeRespawnEvent()
    {
        Checkpoint.respawnEvent.Invoke(checkpoints[currentCheckpointIndex]);
    }
}