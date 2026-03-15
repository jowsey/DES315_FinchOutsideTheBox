using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Flask : NetworkBehaviour
{
    public enum FlaskState
    {
        Idle,
        PickingUp,
        Held,
        PuttingDown,
        Smashed
    }

    public Rigidbody Rb { get; private set; }

    private Transform _moveTarget;
    private Collider[] _colliders;
    private Renderer[] _renderers;

    [SerializeField] private float _movementSpeed;

    [field: SyncVar] public FlaskState State { get; private set; }
    [SerializeField] public bool Smashable;

    private PlayerController _holder;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPickup(NetworkConnectionToClient sender = null)
    {
        if (State != FlaskState.Idle) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player.HeldFlask) return;

        _holder = player;
        _moveTarget = _holder.FlaskPickupTarget;

        Rb.isKinematic = true;

        State = FlaskState.PickingUp;
        RpcPickup(sender.identity);
    }

    [ClientRpc]
    private void RpcPickup(NetworkIdentity holderIdentity)
    {
        foreach (Collider col in _colliders)
        {
            col.enabled = false;
        }

        _holder = holderIdentity.GetComponent<PlayerController>();
        _holder.HeldFlask = this;

        if (_holder.isLocalPlayer)
        {
            Highlight.SetHighlightable("Flask", false);
            Highlight.SetHighlightable("FlaskCarrier", true);
            _holder.FlaskPickupFX.Post(gameObject);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPutdown(FlaskPutdownTarget target, NetworkConnectionToClient sender = null)
    {
        if (State != FlaskState.Held) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (_holder != player) return;

        _moveTarget = target.transform;
        State = FlaskState.PuttingDown;
        Smashable = true;
    }

    [ClientRpc]
    private void RpcEndPutdown()
    {
        foreach (Collider col in _colliders)
        {
            col.enabled = true;
        }

        if (_holder.isLocalPlayer)
        {
            Highlight.SetHighlightable("Flask", true);
            Highlight.SetHighlightable("FlaskCarrier", false);
        }

        _holder.HeldFlask = null;
        _holder = null;
    }

    [ClientRpc]
    public void RpcSmash()
    {
        foreach (Collider col in _colliders)
        {
            col.enabled = false;
        }

        foreach (Renderer rend in _renderers)
        {
            rend.enabled = false;
        }

        if (isServer)
        {
            Rb.isKinematic = true;
            State = FlaskState.Smashed;
        }
    }

    [ClientRpc]
    public void RpcUnsmash()
    {
        foreach (Collider col in _colliders)
        {
            col.enabled = true;
        }

        foreach (Renderer rend in _renderers)
        {
            rend.enabled = true;
        }

        if (isServer)
        {
            Rb.isKinematic = false;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;

            State = FlaskState.Idle;
        }
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        if (State == FlaskState.Held)
        {
            Rb.MovePosition(_holder.FlaskPickupTarget.position);
            return;
        }

        if (State == FlaskState.PickingUp || State == FlaskState.PuttingDown)
        {
            Vector3 targetVec = _moveTarget.position - transform.position;
            Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * _movementSpeed);
            Rb.MovePosition(Rb.position + delta);

            if (targetVec.sqrMagnitude < 0.01f)
            {
                if (State == FlaskState.PickingUp)
                {
                    State = FlaskState.Held;
                }
                else if (State == FlaskState.PuttingDown)
                {
                    Rb.isKinematic = false;
                    Rb.linearVelocity = Vector3.zero;
                    Rb.angularVelocity = Vector3.zero;

                    _moveTarget = null;
                    State = FlaskState.Idle;

                    RpcEndPutdown();
                }
            }
        }
    }

    private void LateUpdate()
    {
        // Clients simulate separately to mask latency
        if (!isServer && State == FlaskState.Held)
        {
            transform.position = _holder.FlaskPickupTarget.position;
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!isServer) return;

        if (!col.collider.CompareTag("Flask") &&
            !col.collider.CompareTag("FlaskCarrier") &&
            LayerMask.LayerToName(col.collider.gameObject.layer) != "Cart")
        {
            if (Smashable)
            {
                RpcSmash();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FlaskCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.CarriedFlasks.Add(this);
            Smashable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("FlaskCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.CarriedFlasks.Remove(this);
        }
    }
}