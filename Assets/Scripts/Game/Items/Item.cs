using Game.Items.Equipments;
using Mirror;
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

        [field: SerializeField] public ItemData Data { get; protected set; }

        public const float PutdownSpeed = 16f;

        [SyncVar(hook = nameof(OnHolderIdentityChanged))]
        protected NetworkIdentity _holderIdentity;

        protected PlayerController _holder;

        [SyncVar(hook = nameof(OnStateChanged))]
        [ReadOnly] public ItemState State;

        [SyncVar(hook = nameof(OnPickuppableChanged))]
        public bool Pickuppable = true;

        [SerializeField] private Event _pickupSfx;

        [field: SerializeField] public bool ShowInfoCard { get; protected set; } = true;
        [field: SerializeField] public bool ForceMoveOnHeld { get; protected set; } = true;

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

        public override void OnStartServer()
        {
            base.OnStartServer();
            RespawnTarget.OnBuildRespawnSnapshot.AddListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnPostRespawn.AddListener(OnPostRespawn);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RespawnTarget.OnBuildRespawnSnapshot.RemoveListener(OnBuildRespawnSnapshot);
            RespawnTarget.OnPostRespawn.RemoveListener(OnPostRespawn);
        }

        private void OnBuildRespawnSnapshot(RespawnTarget.RespawnSnapshot snapshot)
        {
            if (this is SandcastleEquipment && State == ItemState.Inactive) return; // sandcastles are persistent across respawns, don't store
            
            if (Cart.Instance.CarriedItems.Contains(this))
            {
                snapshot.CarriedItems[this] = new RespawnTarget.RespawnSnapshot.CarriedItemSnapshot
                {
                    LocalPosition = Cart.Instance.transform.InverseTransformPoint(transform.position),
                    Rotation = transform.rotation
                };
            }
            else
            {
                snapshot.WorldItems[this] = new RespawnTarget.RespawnSnapshot.WorldItemSnapshot
                {
                    Position = transform.position,
                    Rotation = transform.rotation,
                    State = State switch
                    {
                        ItemState.Held => ItemState.Idle,
                        ItemState.PuttingDown => ItemState.Idle,
                        ItemState.Smashed => ItemState.Inactive,
                        _ => State
                    }
                };
            }
        }

        private void OnPostRespawn(RespawnTarget target)
        {
            if (target.Snapshot.CarriedItems.ContainsKey(this))
            {
                var snapshot = target.Snapshot.CarriedItems[this];
                transform.position = Cart.Instance.transform.TransformPoint(snapshot.LocalPosition);
                transform.rotation = snapshot.Rotation;
                Physics.SyncTransforms();
                State = ItemState.Idle;

                Cart.Instance.AddCarriedItem(this);
            }
            else if (target.Snapshot.WorldItems.TryGetValue(this, out var worldItemSnapshot))
            {
                if (Cart.Instance.CarriedItems.Contains(this))
                {
                    Cart.Instance.RemoveCarriedItem(this);
                }

                transform.position = worldItemSnapshot.Position;
                transform.rotation = worldItemSnapshot.Rotation;
                State = worldItemSnapshot.State;
            }
            else
            {
                // we didn't exist at the time of this snapshot
                NetworkServer.Destroy(gameObject); // 🫡
            }
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

                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Item", false);
                    }

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

                    break;
                }
            }

            // Late transition out
            switch (oldState)
            {
                case ItemState.Held:
                {
                    if (_holder)
                    {
                        _holder.HeldObject = null;
                        _holder = null;
                        _holderIdentity = null;
                    }

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
            if (!Pickuppable) return;
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
        }

        private void FixedUpdate()
        {
            if (!isServer) return;

            if (State == ItemState.Held) return;

            if (State == ItemState.PuttingDown)
            {
                Vector3 targetVec = _moveTarget.position - Rb.position;
                Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * PutdownSpeed);
                Rb.MovePosition(Rb.position + delta);

                if (targetVec.sqrMagnitude < 0.025f)
                {
                    State = ItemState.Idle;
                }
            }
        }

        protected virtual void LateUpdate()
        {
            if (State == ItemState.Held && ForceMoveOnHeld)
            {
                // todo this follows body which is updated in physics, not camera which is updated every frame - if on local player and first-person, it jitters on rotate specifically 
                transform.SetPositionAndRotation(_holder.HeldObjectPickupTarget.position, _holder.HeldObjectPickupTarget.rotation);
            }
        }
    }
}