using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Flask : NetworkBehaviour
{
    public enum HeldState
    {
        None,
        PickingUp,
        Held,
        PuttingDown,
    }

    private Transform _moveTarget;
    private Collider[] _colliders;
    private Rigidbody _rb;

    [SerializeField] private float _movementSpeed;

    [field: SyncVar] public HeldState State { get; private set; }
    [SerializeField] public bool Smashable;

    private PlayerController _holder;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPickup(NetworkConnectionToClient sender = null)
    {
        if (State != HeldState.None) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player.HeldFlask) return;

        _holder = player;

        _moveTarget = _holder.FlaskPickupTarget;
        State = HeldState.PickingUp;

        RpcPickup(sender.identity);
    }

    [ClientRpc]
    private void RpcPickup(NetworkIdentity holderIdentity)
    {
        _holder = holderIdentity.GetComponent<PlayerController>();

        foreach (Collider col in _colliders)
        {
            col.enabled = false;
        }

        if (authority)
        {
            _rb.isKinematic = true;
        }

        _holder.HeldFlask = this;
        if (_holder.isLocalPlayer)
        {
            _holder.FlaskPickup.Post(gameObject);
            Highlight.SetHighlightable("Flask", false);
            Highlight.SetHighlightable("FlaskCarrier", true);
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPutdown(FlaskPutdownTarget target, NetworkConnectionToClient sender = null)
    {
        if (State != HeldState.Held) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (_holder != player) return;

        _moveTarget = target.transform;
        State = HeldState.PuttingDown;
        Smashable = true;
    }

    [ClientRpc]
    private void RpcEndPutdown()
    {
        foreach (Collider col in _colliders)
        {
            col.enabled = true;
        }

        if (authority)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _holder.HeldFlask = null;
        if (_holder.isLocalPlayer)
        {
            Highlight.SetHighlightable("Flask", true);
            Highlight.SetHighlightable("FlaskCarrier", false);
        }

        _holder = null;
    }

    private void FixedUpdate()
    {
        if (!authority || State == HeldState.None) return;

        if (State == HeldState.Held)
        {
            _rb.MovePosition(_holder.FlaskPickupTarget.position);
            return;
        }

        Vector3 delta = (_moveTarget.position - _rb.position).normalized * (Time.fixedDeltaTime * _movementSpeed);
        _rb.MovePosition(_rb.position + delta);

        if ((_rb.position - _moveTarget.position).sqrMagnitude < 1e-2)
        {
            if (State == HeldState.PickingUp)
            {
                State = HeldState.Held;
            }
            else if (State == HeldState.PuttingDown)
            {
                State = HeldState.None;
                _moveTarget = null;

                RpcEndPutdown();
            }
        }
    }

    private void LateUpdate()
    {
        // Clients simulate separately to mask latency
        if (!authority && State == HeldState.Held)
        {
            transform.position = _holder.FlaskPickupTarget.position;
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!col.collider.transform.CompareTag("Flask") &&
            !col.collider.transform.CompareTag("FlaskCarrier") &&
            LayerMask.LayerToName(col.collider.gameObject.layer) != "Cart")
        {
            if (Smashable)
            {
                Smash();
            }
        }
    }

    private void Smash()
    {
        //todo: implement visually
        gameObject.SetActive(false);
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