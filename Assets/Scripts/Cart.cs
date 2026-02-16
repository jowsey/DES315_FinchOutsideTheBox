using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb { get; private set; }



    [SerializeField] private Checkpoint[] checkpoints;
    public int currentCheckpointIndex { get; private set; }

    [SerializeField] private InputActionReference respawnAction;
    private NetworkRigidbodyReliable networkRb;
    private Collider[] colliders;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        networkRb = GetComponent<NetworkRigidbodyReliable>();
        colliders = GetComponentsInChildren<Collider>();
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
            gameObject.SetActive(false);
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.position = newTransform.position;
            transform.rotation = newTransform.rotation;
            gameObject.SetActive(true);
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

    [ClientRpc]
    void RpcInvokeRespawnEvent()
    {
        Checkpoint.respawnEvent.Invoke(checkpoints[currentCheckpointIndex]);
    }

}