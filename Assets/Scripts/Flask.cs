using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Flask : NetworkBehaviour
{
    public enum FlaskState
    {
        Idle,
        Held,
        PuttingDown,
        Smashed
    }

    private bool _hasInitialised;

    public Rigidbody Rb { get; private set; }

    private Transform _moveTarget;
    private Collider[] _colliders;
    private Renderer[] _renderers;

    [SerializeField] private float _movementSpeed;

    [SyncVar(hook = nameof(OnHolderIdentityChanged))]
    private NetworkIdentity _holderIdentity;

    private PlayerController _holder;

    [SyncVar(hook = nameof(OnStateChanged))] [ReadOnly]
    public FlaskState State;

    public bool Smashable;

    [SerializeField] private GameObject _smashedFlaskPrefab;
    [SerializeField] private AK.Wwise.Event _flaskSmashFx;

    private void Awake()
    {
        Rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
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

    private void OnStateChanged(FlaskState _, FlaskState newState)
    {
        switch (newState)
        {
            case FlaskState.Idle:
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

                if (_holder)
                {
                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Flask", true);
                        Highlight.SetHighlightable("FlaskCarrier", false);
                    }

                    _holder.HeldFlask = null;
                    _holder = null;
                }

                break;
            }
            case FlaskState.Held:
            {
                if (isServer)
                {
                    Rb.isKinematic = true;
                    _moveTarget = _holder.FlaskPickupTarget;
                }

                foreach (Collider col in _colliders)
                {
                    col.enabled = false;
                }

                _holder.HeldFlask = this;

                if (_holder.isLocalPlayer)
                {
                    Highlight.SetHighlightable("Flask", false);
                    Highlight.SetHighlightable("FlaskCarrier", true);
                    _holder.FlaskPickupFX.Post(gameObject);
                }

                break;
            }
            case FlaskState.PuttingDown:
                break;
            case FlaskState.Smashed:
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

                Instantiate(_smashedFlaskPrefab, transform.position, transform.rotation);
                if (_hasInitialised)
                {
                    _flaskSmashFx.Post(gameObject);
                }

                break;
            }
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPickup(NetworkConnectionToClient sender = null)
    {
        if (State != FlaskState.Idle) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (player.HeldFlask) return;

        _holderIdentity = sender.identity;
        State = FlaskState.Held;
    }

    [Command(requiresAuthority = false)]
    public void CmdTryPutdown(FlaskPutdownTarget target, NetworkConnectionToClient sender = null)
    {
        if (State != FlaskState.Held) return;

        var player = sender!.identity.GetComponent<PlayerController>();
        if (_holder != player) return;

        _moveTarget = target.transform;
        Smashable = true;
        State = FlaskState.PuttingDown;
    }

    private void FixedUpdate()
    {
        if (!isServer) return;

        if (State == FlaskState.Held) return;

        if (State == FlaskState.PuttingDown)
        {
            Vector3 targetVec = _moveTarget.position - transform.position;
            Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * _movementSpeed);
            Rb.MovePosition(Rb.position + delta);

            if (targetVec.sqrMagnitude < 0.01f)
            {
                State = FlaskState.Idle;
                _holderIdentity = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (State == FlaskState.Held)
        {
            transform.position = _holder.FlaskPickupTarget.position;
            transform.rotation = _holder.FlaskPickupTarget.rotation;
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
                State = FlaskState.Smashed;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FlaskCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.AddCarriedFlask(this);
            Smashable = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("FlaskCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.RemoveCarriedFlask(this);
        }
    }
}