using Mirror;

namespace Game.Items
{
    public class Equipment : Item
    {
        [Command(requiresAuthority = false)]
        public void CmdTryUse(NetworkConnectionToClient sender = null)
        {
            if (State != ItemState.Held) return;
            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != _holder) return;

            if (OnServerUse())
            {
                // unequip on successful use
                ServerSetIdle();
                State = ItemState.Inactive;
            }
        }

        protected virtual bool OnServerUse()
        {
            return true;
        }

        protected override void OnStateChanged(ItemState oldState, ItemState newState)
        {
            switch (oldState)
            {
                default:
                    break;
            }

            switch (newState)
            {
                case ItemState.Held:
                {
                    if (_holder && _holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Item", false);
                    }

                    break;
                }
            }

            base.OnStateChanged(oldState, newState);
        }
    }
}