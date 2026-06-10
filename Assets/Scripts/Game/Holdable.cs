using Mirror;
using UnityEngine;
using UnityEngine.UI;
using Util;

public abstract class Holdable : NetworkBehaviour
{
    public enum HoldableState
    {
        Idle,
        Held,
        PuttingDown,
        Smashed,
        Inactive
    }

    protected bool _hasInitialised;

    public Rigidbody Rb { get; protected set; }

    protected Transform _moveTarget;
    protected Collider[] _colliders;
    protected Renderer[] _renderers;
    protected Light[] _lights;

    [SerializeField] protected float _movementSpeed;

    [SyncVar(hook = nameof(OnHolderIdentityChanged))]
    protected NetworkIdentity _holderIdentity;

    protected PlayerController _holder;

    [SyncVar(hook = nameof(OnStateChanged))]
    [ReadOnly] public HoldableState State;
    [SyncVar(hook = nameof(OnPickuppableChanged))]
    public bool Pickuppable;

    protected virtual void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _lights = GetComponentsInChildren<Light>();
        Pickuppable = true;
    }

    public override void OnStartClient()
    {
        _hasInitialised = true;
    }

    private void OnHolderIdentityChanged(NetworkIdentity _, NetworkIdentity newHolder)
    {
        // Only assign new holders, we handle removing the old holder ourselves
        if (newHolder)
        {
            _holder = newHolder.GetComponent<PlayerController>();
        }
    }

    protected virtual void OnStateChanged(HoldableState _, HoldableState newState)
    {
        switch (newState)
        {
            case HoldableState.Idle:
            {
                if (isServer)
                {
                    Rb.isKinematic = false;
                    Rb.linearVelocity = Vector3.zero;
                    Rb.angularVelocity = Vector3.zero;
                    _moveTarget = null;
                }

                foreach (Collider col in _colliders) { col.enabled = true; }
                foreach (Renderer rend in _renderers) { rend.enabled = true; }
                foreach (Light l in _lights) { l.enabled = true; }

                if (_holder)
                {
                    _holder.HeldObject = null;
                    _holder = null;
                }

                break;
            }
            case HoldableState.Held:
            {
                if (isServer)
                {
                    Rb.isKinematic = true;
                    _moveTarget = _holder.HeldObjectPickupTarget;
                }

                foreach (Collider col in _colliders) { col.enabled = false; }

                _holder.HeldObject = this;

                break;
            }
            case HoldableState.PuttingDown:
            {
                if (isServer)
                {
                    Rb.position = _holder.HeldObjectPickupTarget.position;
                    Rb.rotation = _holder.HeldObjectPickupTarget.rotation;
                }

                break;
            }
            case HoldableState.Smashed:
            case HoldableState.Inactive:
            {
                if (isServer) { Rb.isKinematic = true; }
                foreach (Collider col in _colliders) { col.enabled = false; }
                foreach (Renderer rend in _renderers) { rend.enabled = false; }
                foreach (Light l in _lights) { l.enabled = false; }

                break;
            }
        }
    }

    private void OnPickuppableChanged(bool _, bool newState)
    {
        GetComponent<Highlight>().enabled = newState;
        GetComponent<PlayerImmovable>().enabled = !newState;
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPickup(NetworkConnectionToClient sender = null)
    {
        PlayerController player = sender!.identity.GetComponent<PlayerController>();
        ServerTryPickup(player);
    }

    //because we might be picking up from Shop.CmdTryBuy() and calling a command from a command messes with the sender data or something ?
    [Server]
    public void ServerTryPickup(PlayerController player)
    {
        if (State != HoldableState.Idle) return;
        if (!Pickuppable) { return; }
        if (player.HeldObject) return;

        _holderIdentity = player.netIdentity;
        State = HoldableState.Held;
    }


    [Command(requiresAuthority = false)]
    public void CmdTryPutdown(HeldObjectPutdownTarget target, NetworkConnectionToClient sender = null)
    {
        if (State != HoldableState.Held) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player != _holder) return;

        _moveTarget = target.transform;
        
        OnBaseClassPutdown();

        State = HoldableState.PuttingDown;
    }
    protected virtual void OnBaseClassPutdown() {}

    private void FixedUpdate()
    {
        if (!isServer) return;

        if (State == HoldableState.Held) return;

        if (State == HoldableState.PuttingDown)
        {
            Vector3 targetVec = _moveTarget.position - Rb.position;
            Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * _movementSpeed);
            Rb.MovePosition(Rb.position + delta);

            if (targetVec.sqrMagnitude < 0.025f)
            {
                State = HoldableState.Idle;
                _holderIdentity = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (State == HoldableState.Held)
        {
            transform.position = _holder.HeldObjectPickupTarget.position;
            // todo this follows body which is updated in physics, not camera which is updated every frame - if on local player and first-person, it jitters on rotate specifically 
            transform.rotation = _holder.HeldObjectPickupTarget.rotation;
        }
    }
}
