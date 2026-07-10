using UnityEngine;

namespace Game.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public class Treasure : Item
    {
        public bool Smashable;

        [SerializeField] private GameObject _smashedTreasurePrefab;
        [SerializeField] private AK.Wwise.Event _breakSfx;

        protected override void OnStateChanged(ItemState oldState, ItemState newState)
        {
            // Transition out
            switch (oldState)
            {
                // No longer holding
                case ItemState.Held:
                {
                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("ObjectCarrier", false);
                    }

                    break;
                }
            }

            // Transition in
            switch (newState)
            {
                case ItemState.Held:
                {
                    if (isServer)
                    {
                        Smashable = false;
                    }

                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("ObjectCarrier", true);
                    }

                    break;
                }
                case ItemState.PuttingDown:
                {
                    if (isServer)
                    {
                        Smashable = true;
                    }

                    break;
                }
                case ItemState.Smashed:
                {
                    Instantiate(_smashedTreasurePrefab, transform.position, transform.rotation);
                    if (_hasInitialised)
                    {
                        _breakSfx.Post(gameObject);
                    }

                    break;
                }
            }

            // base clears references, cleans up etc, so call last
            base.OnStateChanged(oldState, newState);
        }

        private void OnCollisionEnter(Collision col)
        {
            if (!isServer) return;

            if (!col.collider.CompareTag("Treasure") &&
                !col.collider.CompareTag("Item") &&
                !col.collider.CompareTag("ObjectCarrier") &&
                LayerMask.LayerToName(col.collider.gameObject.layer) != "Cart")
            {
                if (Smashable)
                {
                    State = ItemState.Smashed;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer)
            {
                return;
            }

            if (other.CompareTag("ObjectCarrier"))
            {
                Cart cart = other.GetComponentInParent<Cart>();
                cart.AddCarriedItem(this);
                Smashable = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!isServer)
            {
                return;
            }

            if (other.CompareTag("ObjectCarrier"))
            {
                Cart cart = other.GetComponentInParent<Cart>();
                cart.RemoveCarriedItem(this);
            }
        }
    }
}