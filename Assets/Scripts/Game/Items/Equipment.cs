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
            if (StateData is HeldStateData heldData)
            {
                var cachedClient = heldData.Holder.connectionToClient;

                if (_singleUse)
                {
                    StateData = new InactiveStateData();
                }

                ClientOnSuccessfulUse();
                TargetOnSuccessfulUse(cachedClient);
            }
        }

        [ClientRpc]
        protected void ClientOnSuccessfulUse()
        {
            _useSfx?.Post(gameObject);
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