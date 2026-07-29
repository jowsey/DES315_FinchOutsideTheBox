using Mirror;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Game.Items
{
    public abstract class Equipment : Item
    {
        [SerializeField] private Event _useSfx;

        public virtual void TryUse()
        {
        }

        protected virtual void OnServerUse()
        {
            var cachedClient = _holder.connectionToClient;

            ServerSetIdle();
            State = ItemState.Inactive;

            TargetOnSuccessfulUse(cachedClient);
        }

        [TargetRpc]
        protected void TargetOnSuccessfulUse(NetworkConnectionToClient target)
        {
            _useSfx.Post(gameObject);
            OnClientSuccessfulUse();
        }

        protected virtual void OnClientSuccessfulUse()
        {
        }
    }
}