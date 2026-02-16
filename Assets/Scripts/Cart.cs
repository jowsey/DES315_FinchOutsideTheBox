using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Cart : NetworkBehaviour
{
    public Rigidbody Rb { get; private set; }

    public AK.Wwise.Event carSound = new AK.Wwise.Event();
    public AK.Wwise.RTPC RTPCSpeed;
    public float RTPCpeed = 0f;

    [SerializeField] private Checkpoint[] checkpoints;
    public int currentCheckpointIndex { get; private set; }

    [SerializeField] private InputActionReference respawnAction;
    [SerializeField] private InputActionReference dev_checkpointBackAction;
    [SerializeField] private InputActionReference dev_checkpointForwardAction;

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

        carSound.Post(gameObject);
        float RTPCpeed = 0;
        RTPCSpeed.SetGlobalValue(RTPCpeed);
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

    private void Update()
    {
        IsZDown();

        if (respawnAction.action.WasPressedThisFrame())
        {
            CmdInvokeRespawnEvent();
        }
        else if (dev_checkpointBackAction.action.WasPressedThisFrame() && currentCheckpointIndex != 0)
        {
            --currentCheckpointIndex;
            CmdInvokeRespawnEvent();
        }
        else if (dev_checkpointForwardAction.action.WasPressedThisFrame() && currentCheckpointIndex != checkpoints.Length - 1)
        {
            ++currentCheckpointIndex;
            CmdInvokeRespawnEvent();
        }
    }

    [Command(requiresAuthority = false)]
    void CmdInvokeRespawnEvent()
    {
        RpcInvokeRespawnEvent();
    }

    [ClientRpc]
    void RpcInvokeRespawnEvent()
    {
        Checkpoint.respawnEvent.Invoke(checkpoints[currentCheckpointIndex]);
    }

    public void IsZDown()
    {
        var zKeyPressed = Keyboard.current.zKey.isPressed;
        if (zKeyPressed == true)
        {
            RTPCpeed += 1f;
        }
        else
        {
            RTPCpeed -= 3f;
        }

        if (RTPCpeed < 0)
        {
            RTPCpeed = 0;
        }
        if (RTPCpeed > 100)
        {
            RTPCpeed = 100;
        }
        Debug.Log(RTPCSpeed);
        RTPCSpeed.SetGlobalValue(RTPCpeed);

    }
}