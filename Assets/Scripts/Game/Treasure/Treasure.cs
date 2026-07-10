using UnityEngine;

namespace Game.Treasure
{
    [RequireComponent(typeof(Rigidbody))]
    public class Treasure : Holdable
    {
        public TreasureType Type;
        public bool Smashable;

        [SerializeField] private GameObject _smashedTreasurePrefab;
        [SerializeField] private AK.Wwise.Event _breakSfx;

        protected override void OnStateChanged(HoldableState oldState, HoldableState newState)
        {
            // Transition out
            switch (oldState)
            {
                // No longer being held
                case HoldableState.Held:
                {
                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Treasure", true);
                        Highlight.SetHighlightable("ObjectCarrier", false);
                    }

                    break;
                }
            }

            // Transition in
            switch (newState)
            {
                case HoldableState.Held:
                {
                    if (isServer)
                    {
                        Smashable = false;
                    }

                    if (_holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("Treasure", false);
                        Highlight.SetHighlightable("ObjectCarrier", true);
                    }

                    break;
                }
                case HoldableState.PuttingDown:
                {
                    if (isServer)
                    {
                        Smashable = true;
                    }

                    break;
                }
                case HoldableState.Smashed:
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
                    State = HoldableState.Smashed;
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
                cart.AddCarriedTreasure(this);
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
                cart.RemoveCarriedTreasure(this);
            }
        }
    }
}