using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Treasure : NetworkBehaviour
{
    public enum TreasureState
    {
        Idle,
        Held,
        PuttingDown,
        Smashed,
        Inactive
    }

    private bool _hasInitialised;

    public Rigidbody Rb { get; private set; }

    private Transform _moveTarget;
    private Collider[] _colliders;
    private Renderer[] _renderers;
    private Light[] _lights;

    [SerializeField] private float _movementSpeed;

    [SyncVar(hook = nameof(OnHolderIdentityChanged))]
    private NetworkIdentity _holderIdentity;

    private PlayerController _holder;

    [SyncVar(hook = nameof(OnStateChanged))] [ReadOnly]
    public TreasureState State;

    public bool Smashable;

    [SerializeField] private GameObject _smashedTreasurePrefab;
    [SerializeField] private AK.Wwise.Event _treasureSmashFx;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _lights = GetComponentsInChildren<Light>();
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

    private void OnStateChanged(TreasureState _, TreasureState newState)
    {
        switch (newState)
        {
            case TreasureState.Idle:
            {
                if (isServer)
                {
                    Rb.isKinematic = false;
                    Rb.linearVelocity = Vector3.zero;
                    Rb.angularVelocity = Vector3.zero;
                    _moveTarget = null;
                }

                foreach (Collider col in _colliders)
                {
                    col.enabled = true;
                }

                foreach (Renderer rend in _renderers)
                {
                    rend.enabled = true;
                }

                foreach (Light l in _lights)
                {
                    l.enabled = true;
                }

                if (_holder)
                {
                    _holder.HeldTreasure = null;
                    _holder = null;
                }

                break;
            }
            case TreasureState.Held:
            {
                if (isServer)
                {
                    Rb.isKinematic = true;
                    _moveTarget = _holder.TreasurePickupTarget;
                }

                foreach (Collider col in _colliders)
                {
                    col.enabled = false;
                }

                _holder.HeldTreasure = this;

                if (_holder.isLocalPlayer)
                {
                    Highlight.SetHighlightable("Treasure", false);
                    Highlight.SetHighlightable("TreasureCarrier", true);
                }

                if (_hasInitialised)
                {
                    _holder.TreasurePickupFX.Post(gameObject);
                }

                break;
            }
            case TreasureState.PuttingDown:
            {
                if (isServer)
                {
                    Rb.position = _holder.TreasurePickupTarget.position;
                    Rb.rotation = _holder.TreasurePickupTarget.rotation;
                }

                if (_holder?.isLocalPlayer == true)
                {
                    Highlight.SetHighlightable("Treasure", true);
                    Highlight.SetHighlightable("TreasureCarrier", false);
                }

                break;
            }
            case TreasureState.Smashed:
            case TreasureState.Inactive:
            {
                if (isServer)
                {
                    Rb.isKinematic = true;
                }

                foreach (Collider col in _colliders)
                {
                    col.enabled = false;
                }

                foreach (Renderer rend in _renderers)
                {
                    rend.enabled = false;
                }

                foreach (Light l in _lights)
                {
                    l.enabled = false;
                }

                // Smashed same as Inactive except it also makes a smashed treasure
                if (State == TreasureState.Smashed)
                {
                    Instantiate(_smashedTreasurePrefab, transform.position, transform.rotation);
                    if (_hasInitialised)
                    {
                        _treasureSmashFx.Post(gameObject);
                    }
                }

                break;
            }
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPickup(NetworkConnectionToClient sender = null)
    {
        if (State != TreasureState.Idle) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player.HeldTreasure) return;

        _holderIdentity = sender.identity;
        State = TreasureState.Held;
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPutdown(TreasurePutdownTarget target, NetworkConnectionToClient sender = null)
    {
        if (State != TreasureState.Held) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player != _holder) return;

        _moveTarget = target.transform;
        Smashable = true;
        State = TreasureState.PuttingDown;
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        if (State == TreasureState.Held) return;

        if (State == TreasureState.PuttingDown)
        {
            Vector3 targetVec = _moveTarget.position - Rb.position;
            Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * _movementSpeed);
            Rb.MovePosition(Rb.position + delta);

            if (targetVec.sqrMagnitude < 0.025f)
            {
                State = TreasureState.Idle;
                _holderIdentity = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (State == TreasureState.Held)
        {
            transform.position = _holder.TreasurePickupTarget.position;
            // todo this follows body which is updated in physics, not camera which is updated every frame - if on local player and first-person, it jitters on rotate specifically 
            transform.rotation = _holder.TreasurePickupTarget.rotation;
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (!isServer) return;

        if (!col.collider.CompareTag("Treasure") &&
            !col.collider.CompareTag("TreasureCarrier") &&
            LayerMask.LayerToName(col.collider.gameObject.layer) != "Cart")
        {
            if (Smashable)
            {
                State = TreasureState.Smashed;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TreasureCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.AddCarriedTreasure(this);
            Smashable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("TreasureCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.RemoveCarriedTreasure(this);
        }
    }
}