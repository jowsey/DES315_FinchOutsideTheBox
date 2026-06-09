using UnityEngine;

public class Item : Holdable
{
    public ItemType Type;

    //todo: do we want like a virtual Use() in here or something

    protected override void OnStateChanged(HoldableState _, HoldableState newState)
    {
        //chat yo shit base class
        base.OnStateChanged(_, newState);

        switch (newState)
        {
            case HoldableState.Held:
            {
                if (_holder != null && _holder.isLocalPlayer)
                {
                    Highlight.SetHighlightable("Item", false);
                }
                break;
            }
            case HoldableState.PuttingDown:
            {
                if (_holder?.isLocalPlayer == true)
                {
                    Highlight.SetHighlightable("Item", true);
                }
                break;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer) return;

        if (other.CompareTag("TreasureCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.AddCarriedItem(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isServer) return;

        if (other.CompareTag("TreasureCarrier"))
        {
            Cart cart = other.GetComponentInParent<Cart>();
            cart.RemoveCarriedItem(this);
        }
    }
}
