using UnityEngine;

namespace Game.Items
{
    public class Equipment : Item
    {
        public virtual void Use()
        {
            if (!_holder || !isServer)
            {
                Debug.LogWarning($"Tried using {name} but holder {_holder}, isServer {isServer}");
                return;
            }
            
            // todo rotate holder to face camera dir i think?
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