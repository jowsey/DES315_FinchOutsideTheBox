using System;
using System.Linq;
using Game.Items.Equipments;
using Mirror;
using UnityEngine;
using Util;
using Event = AK.Wwise.Event;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;
using ShowInInspectorAttribute = Sirenix.OdinInspector.ShowInInspectorAttribute;

namespace Game.Items
{
    [RequireComponent(typeof(Rigidbody), typeof(Highlight), typeof(Interactable))]
    public abstract class Item : NetworkBehaviour
    {
        [Serializable]
        public class ItemStateData
        {
        }

        [Serializable]
        public class IdleStateData : ItemStateData
        {
        }

        [Serializable]
        public class HeldStateData : ItemStateData
        {
            public PlayerController Holder;
        }

        [Serializable]
        public class PuttingDownStateData : ItemStateData
        {
            public HeldObjectPutdownTarget Target;
        }

        [Serializable]
        public class SmashedStateData : ItemStateData
        {
        }

        [Serializable]
        public class InactiveStateData : ItemStateData
        {
        }

        [Serializable]
        public class FrozenStateData : ItemStateData
        {
        }

        [Serializable]
        public class SackCarriedStateData : ItemStateData
        {
            public UpgradeSack Sack;
        }

        protected bool _hasInitialised;

        public Rigidbody Rb { get; protected set; }

        protected Collider[] _colliders;
        protected Renderer[] _renderers;
        protected Light[] _lights;

        [field: SerializeField] public ItemData Data { get; protected set; }

        public const float PutdownSpeed = 16f;
        public const float MaxThrowForce = 100f;

        [SyncVar] [ReadOnly] public ItemStateData StateData = new IdleStateData();

        [ShowInInspector] private string StateName => StateData.GetType().Name;

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
            UpdateState(null, StateData);
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
            if (this is SandcastleEquipment && StateData is InactiveStateData) return; // sandcastles are persistent across respawns, don't store
            if (StateData is SackCarriedStateData) return; // handled by UpgradeSack

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
                    StateData = StateData switch
                    {
                        HeldStateData or PuttingDownStateData => new IdleStateData(),
                        SmashedStateData => new InactiveStateData(),
                        _ => StateData
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
                ServerSetState(new IdleStateData());

                Cart.Instance.AddCarriedItem(this);
            }
            else if (target.Snapshot.WorldItems.TryGetValue(this, out var worldItemSnapshot))
            {
                if (Cart.Instance.CarriedItems.Contains(this))
                {
                    Cart.Instance.RemoveCarriedItem(this);
                }

                ServerSetState(worldItemSnapshot.StateData);
                Rb.position = worldItemSnapshot.Position;
                Rb.rotation = worldItemSnapshot.Rotation;
            }
            else if (target.Snapshot.SackStoredItems.ContainsValue(this))
            {
                var (sack, item) = target.Snapshot.SackStoredItems.First(x => x.Value == this);
                ServerSetState(new SackCarriedStateData { Sack = sack });
            }
            else
            {
                // we didn't exist at the time of this snapshot
                NetworkServer.Destroy(gameObject); // 🫡
            }
        }

        [Server]
        public void ServerSetState(ItemStateData newState)
        {
            var oldState = StateData;
            UpdateState(oldState, newState);
            if (isServer) RpcUpdateState(oldState, newState);
            StateData = newState;
        }

        [ClientRpc]
        private void RpcUpdateState(ItemStateData oldState, ItemStateData newState)
        {
            if (isServer) return;
            UpdateState(oldState, newState);
        }

        protected virtual void UpdateState(ItemStateData oldData, ItemStateData newData)
        {
            // Transition out
            switch (oldData)
            {
                case HeldStateData heldData:
                {
                    if (heldData.Holder)
                    {
                        heldData.Holder.HeldObject = null;
                        if (heldData.Holder.isLocalPlayer)
                        {
                            Highlight.SetHighlightable("Item", true);
                        }
                    }

                    break;
                }
                case SackCarriedStateData sackData:
                {
                    if (isServer && sackData.Sack.StoredItem == this)
                    {
                        sackData.Sack.StoredItem = null;
                    }

                    foreach (Collider col in _colliders) col.enabled = true;

                    break;
                }
            }

            // Transition in
            switch (newData)
            {
                case IdleStateData:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = false;
                        Rb.linearVelocity = Vector3.zero;
                        Rb.angularVelocity = Vector3.zero;
                    }

                    foreach (Collider col in _colliders) col.enabled = true;
                    foreach (Renderer rend in _renderers) rend.enabled = true;
                    foreach (Light l in _lights) l.enabled = true;

                    break;
                }
                case HeldStateData heldData:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;

                        if (Cart.Instance.CarriedItems.Contains(this))
                        {
                            // workaround physics not noticing the trigger exit
                            Cart.Instance.RemoveCarriedItem(this);
                        }
                    }

                    foreach (Collider col in _colliders) col.enabled = false;

                    heldData.Holder.HeldObject = this;
                    if (heldData.Holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Item", false);
                    }

                    if (_hasInitialised)
                    {
                        _pickupSfx?.Post(gameObject);
                    }

                    break;
                }
                case PuttingDownStateData:
                {
                    if (_hasInitialised)
                    {
                        _pickupSfx?.Post(gameObject);
                    }

                    break;
                }
                case SmashedStateData:
                case InactiveStateData:
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
                case FrozenStateData:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;
                    }

                    break;
                }
                case SackCarriedStateData sackData:
                {
                    if (isServer)
                    {
                        Rb.isKinematic = true;
                        sackData.Sack.StoredItem = this;
                    }

                    foreach (Collider col in _colliders) col.enabled = false;
                    foreach (Renderer rend in _renderers) rend.enabled = true;
                    foreach (Light l in _lights) l.enabled = true;

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

        [Server]
        public void ServerTryPickup(PlayerController player)
        {
            if (!Pickuppable) return;
            if (player.HeldObject) return;
            if (StateData is not IdleStateData and not SackCarriedStateData) return;

            ServerSetState(new HeldStateData { Holder = player });
        }

        [Command(requiresAuthority = false)]
        public void CmdTryPutdown(HeldObjectPutdownTarget target, NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != heldData.Holder) return;

            ServerSetState(new PuttingDownStateData { Target = target });
        }

        [Command(requiresAuthority = false)]
        public void CmdTryStore(UpgradeSack sack, NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;
            if (sack.StoredItem) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != heldData.Holder) return;

            ServerSetState(new SackCarriedStateData { Sack = sack });
        }

        [Command(requiresAuthority = false)]
        public void CmdTryDrop(NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != heldData.Holder) return;
            ServerSetState(new IdleStateData());
        }

        [Command(requiresAuthority = false)]
        public void CmdTryThrow(float forceRatio, Vector3 worldThrowDir, NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;

            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != heldData.Holder) return;

            ServerSetState(new IdleStateData());

            var dir = worldThrowDir.normalized;
            var impulseForce = MaxThrowForce * Mathf.Clamp01(forceRatio);

            Rb.AddForce(dir * impulseForce, ForceMode.Impulse);
            Rb.AddTorque(dir * (impulseForce * 0.01f), ForceMode.Impulse);
        }

        protected virtual void FixedUpdate()
        {
            if (!isServer) return;

            if (StateData is PuttingDownStateData puttingDownData)
            {
                Vector3 targetVec = puttingDownData.Target.transform.position - Rb.position;
                Vector3 delta = targetVec.normalized * (Time.fixedDeltaTime * PutdownSpeed);
                Rb.MovePosition(Rb.position + delta);

                if (targetVec.sqrMagnitude < 0.025f)
                {
                    ServerSetState(new IdleStateData());
                }
            }
        }

        protected virtual void LateUpdate()
        {
            if (StateData is HeldStateData heldData && ForceMoveOnHeld)
            {
                // todo this follows body which is updated in physics, not camera which is updated every frame - if on local player and first-person, it jitters on rotate specifically 
                transform.SetPositionAndRotation(heldData.Holder.HeldObjectPickupTarget.position, heldData.Holder.HeldObjectPickupTarget.rotation);
            }
            else if (StateData is SackCarriedStateData sackCarriedData)
            {
                transform.SetPositionAndRotation(sackCarriedData.Sack.StorePosition.position, sackCarriedData.Sack.StorePosition.rotation);
            }
        }
    }
}