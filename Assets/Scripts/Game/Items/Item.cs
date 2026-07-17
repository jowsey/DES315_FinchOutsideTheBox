using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using Util;
using Event = AK.Wwise.Event;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Game.Items
{
    [RequireComponent(typeof(Rigidbody), typeof(Highlight), typeof(Interactable))]
    public abstract class Item : NetworkBehaviour
    {
        public enum ItemState
        {
            Idle,
            Held,
            PuttingDown,
            Smashed,
            Inactive,
            Frozen
        }

        protected bool _hasInitialised;

        public Rigidbody Rb { get; protected set; }

        protected Transform _moveTarget;
        protected Collider[] _colliders;
        protected Renderer[] _renderers;
        protected Light[] _lights;

        [field: SerializeField] [field: Required] public ItemData Data { get; protected set; }

        [SerializeField] protected float _movementSpeed;

        [SyncVar(hook = nameof(OnHolderIdentityChanged))]
        protected NetworkIdentity _holderIdentity;

        protected PlayerController _holder;

        [SyncVar(hook = nameof(OnStateChanged))]
        [ReadOnly] public ItemState State;

        [SyncVar(hook = nameof(OnPickuppableChanged))]
        public bool Pickuppable = true;

        [SerializeField] private Event _pickupSfx;

        protected virtual void Awake()
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

        protected virtual void OnStateChanged(ItemState oldState, ItemState newState)
        {
            // Transition out
            switch (oldState)
            {
                case ItemState.Held:
                {
                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Item", true);
                    }

                    break;
                }
            }

            // Transition in
            switch (newState)
            {
                case ItemState.Idle:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = false;
                        Rb.linearVelocity = Vector3.zero;
                        Rb.angularVelocity = Vector3.zero;
                        _moveTarget = null;
                    }

                    foreach (Collider col in _colliders) col.enabled = true;
                    foreach (Renderer rend in _renderers) rend.enabled = true;
                    foreach (Light l in _lights) l.enabled = true;

                    if (_holder)
                    {
                        if (_holder.isLocalPlayer)
                        {
                            Highlight.SetHighlightable("Item", false);
                        }

                        _holder.HeldObject = null;
                        _holder = null;
                    }

                    break;
                }
                case ItemState.Held:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;
                        _moveTarget = _holder.HeldObjectPickupTarget;
                    }

                    foreach (Collider col in _colliders) col.enabled = false;

                    _holder.HeldObject = this;

                    if (_hasInitialised)
                    {
                        _pickupSfx.Post(gameObject);
                    }

                    break;
                }
                case ItemState.PuttingDown:
                {
                    if (isServer)
                    {
                        Rb.position = _holder.HeldObjectPickupTarget.position;
                        Rb.rotation = _holder.HeldObjectPickupTarget.rotation;
                    }

                    if (_hasInitialised)
                    {
                        _pickupSfx.Post(gameObject);
                    }

                    break;
                }
                case ItemState.Smashed:
                case ItemState.Inactive:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;
                    }

                    foreach (Collider col in _colliders) col.enabled = false;
                    foreach (Renderer rend in _renderers) rend.enabled = false;
                    foreach (Light l in _lights) l.enabled = false;

                    break;
                }
                case ItemState.Frozen:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;
                    }

                    foreach (Collider col in _colliders) col.enabled = false;

                    break;
                }
            }
        }

        private void OnPickuppableChanged(bool _, bool newState)
        {
            if (TryGetComponent<Highlight>(out var h)) h.enabled = newState;
            if (TryGetComponent<PlayerImmovable>(out var pi)) pi.enabled = !newState;
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
            if (State != ItemState.Idle) return;
            if (!Pickuppable)
            {
                return;
            }

            if (player.HeldObject) return;

            _holderIdentity = player.netIdentity;
            State = ItemState.Held;
        }

        [Command(requiresAuthority = false)]
        public void CmdTryPutdown(HeldObjectPutdownTarget target, NetworkConnectionToClient sender = null)
        {
            if (State != ItemState.Held) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != _holder) return;

            _moveTarget = target.transform;

            State = ItemState.PuttingDown;
        }

        [Command(requiresAuthority = false)]
        public void CmdTryDrop(NetworkConnectionToClient sender = null)
        {
            if (State != ItemState.Held) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != _holder) return;

            State = ItemState.Idle;
            _holderIdentity = null;
        }

        private void FixedUpdate()
        {
            if (!isServer) return;

            if (State == ItemState.Held) return;

            if (State == ItemState.PuttingDown)
            {
                Vector3 targetVec = _moveTarget.position - Rb.position;
                Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * _movementSpeed);
                Rb.MovePosition(Rb.position + delta);

                if (targetVec.sqrMagnitude < 0.025f)
                {
                    State = ItemState.Idle;
                    _holderIdentity = null;
                }
            }
        }

        private void LateUpdate()
        {
            if (State == ItemState.Held)
            {
                transform.position = _holder.HeldObjectPickupTarget.position;
                // todo this follows body which is updated in physics, not camera which is updated every frame - if on local player and first-person, it jitters on rotate specifically 
                transform.rotation = _holder.HeldObjectPickupTarget.rotation;
            }
        }
    }
}