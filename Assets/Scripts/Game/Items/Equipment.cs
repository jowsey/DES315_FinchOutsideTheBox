namespace Game.Items
{
    public class Equipment : Item
    {
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
                        Highlight.SetHighlightable("Treasure", false);
                        Highlight.SetHighlightable("Item", false);
                    }

                    break;
                }
            }

            base.OnStateChanged(oldState, newState);
        }
    }
}