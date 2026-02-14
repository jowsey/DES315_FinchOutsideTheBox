using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

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
        if (isServer)
        {
            Transform newTransform = checkpoint.cartRespawnLocalTransform;
            transform.position = newTransform.position;
            transform.rotation = newTransform.rotation;
        }
    }

    private void Update()
    {
        IsZDown();

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