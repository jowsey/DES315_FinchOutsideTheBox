using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Flask : Mirror.NetworkBehaviour
{
    public enum State
    {
        None,
        PickingUp,
        Held,
        PuttingDown,
    }

    private Transform _target;
    private Collider[] _colliders;
    private Rigidbody _rb;
    [field: Mirror.SyncVar] public State state { get; private set; }
    [SerializeField] public bool Smashable;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        state = State.None;
    }

    public void Start()
    {
        Checkpoint.respawnEvent.AddListener(OnRespawn);
    }

    private void OnRespawn(Checkpoint checkpoint)
    {
        gameObject.SetActive(true);
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdPickup(Transform pickupTarget)
    {
        //Pickup involves disabling colliders and setting rigidbody to kinematic, then moving flask towards the target
        //Syncing of movement of flask towards pickup target will be handled by NetworkRigidbody
        //Disabling colliders and setting rigidbody to kinematic needs to be done per-client though, so use an RPC
        _target = pickupTarget;
        RpcPickup();
        state = State.PickingUp;
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdPutdown(Transform putdownTarget)
    {
        _target = putdownTarget;
        state = State.PuttingDown;
        Smashable = true;
    }

    [Mirror.Command(requiresAuthority = false)]
    public void CmdDrop()
    {
        _target = null;
        state = State.None;
        RpcEndPutdown();
    }

    [Mirror.Command(requiresAuthority = false)]
    private void CmdEndPutdown()
    {
        RpcEndPutdown();
        Smashable = true;
    }

    [Mirror.ClientRpc]
    private void RpcPickup()
    {
        foreach (Collider collider in _colliders) { collider.enabled = false; }
        _rb.isKinematic = true;
    }

    //[Mirror.ClientRpc]
    //private void RpcEndPickup()
    //{
    //}

    [Mirror.ClientRpc]
    private void RpcEndPutdown()
    {
        foreach (Collider collider in _colliders) { collider.enabled = true; }
        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (state == State.Held)
        {
            if (_target) { _rb.MovePosition(_target.position); }
            return;
        }
        if (state == State.None)
        {
            return;
        }

        float movementSpeed = 10.0f;
        Vector3 newPos = _rb.position + (_target.position - _rb.position).normalized * Time.fixedDeltaTime * movementSpeed;
        _rb.MovePosition(newPos);

        if ((_rb.position - _target.position).sqrMagnitude < 1e-2)
        {
            if (state == State.PickingUp)
            {
                /*CmdEndPickup();*/
                state = State.Held;
            }
            else
            {
                CmdEndPutdown();
                _target = null;
                state = State.None;
            }
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!col.collider.transform.CompareTag("Flask") && !col.collider.transform.CompareTag("FlaskCarrier") && LayerMask.LayerToName(col.collider.gameObject.layer) != "Cart")
        {
            if (Smashable) { Smash(); }
        }
    }

    void Smash()
    {
        //todo: implement visually
        gameObject.SetActive(false);
    }
}
