using Mirror;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Game.Items
{
    public abstract class Equipment : Item
    {
        [SerializeField] private Event _useSfx;
        
        [SerializeField] protected bool _singleUse = true;
        
        public virtual void TryUse()
        {
        }

        protected virtual void OnServerUse()
        {
            var cachedClient = _holder.connectionToClient;

            if (_singleUse)
            {
                State = ItemState.Inactive;
            }

            ClientOnSuccessfulUse();
            TargetOnSuccessfulUse(cachedClient);
        }

        [ClientRpc]
        protected void ClientOnSuccessfulUse()
        {
            _useSfx.Post(gameObject);
        }

        [TargetRpc]
        protected void TargetOnSuccessfulUse(NetworkConnectionToClient target)
        {
            OnClientHolderSuccessfulUse();
        }

        protected virtual void OnClientHolderSuccessfulUse()
        {
        }
    }
}