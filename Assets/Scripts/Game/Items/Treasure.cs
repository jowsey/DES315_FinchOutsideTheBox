using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Game.Items
{
    public class Treasure : Item
    {
        public bool Smashable;

        [SerializeField] private GameObject _smashedTreasurePrefab;
        [SerializeField] private AK.Wwise.Event _breakSfx;

        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshCollider _meshCollider;
        [SerializeField] private List<Mesh> _randomMeshOptions = new();

        [SyncVar(hook = nameof(OnChangeRandomMeshIndex))] private int _randomMeshIndex = -1;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_randomMeshOptions.Count > 0)
            {
                _randomMeshIndex = Random.Range(0, _randomMeshOptions.Count);
            }
            
            RespawnTarget.OnRespawn.AddListener(OnRespawn);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            RespawnTarget.OnRespawn.RemoveListener(OnRespawn);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!_meshFilter) _meshFilter = GetComponentInChildren<MeshFilter>();
            if (!_meshCollider) _meshCollider = GetComponentInChildren<MeshCollider>();
        }

        private void OnChangeRandomMeshIndex(int oldValue, int newValue)
        {
            if (newValue < 0 || newValue >= _randomMeshOptions.Count) return;

            var newMesh = _randomMeshOptions[newValue];
            if (_meshFilter.sharedMesh != newMesh) _meshFilter.sharedMesh = newMesh;
            if (_meshCollider && _meshCollider.sharedMesh != newMesh) _meshCollider.sharedMesh = newMesh;
        }

        private void OnDestroy()
        {
            // fixes hang on scene unload in editor? shrug
            _meshFilter.sharedMesh = null;
            _meshCollider.sharedMesh = null;
        }

        private void OnRespawn(RespawnTarget target)
        {
            Smashable = false;
        }
        
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
                        Highlight.SetHighlightable("TreasureCarrier", false);
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
                        Highlight.SetHighlightable("TreasureCarrier", true);
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

            if (!col.collider.CompareTag("Item") &&
                !col.collider.CompareTag("TreasureCarrier") &&
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

            if (other.CompareTag("TreasureCarrier"))
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

            if (other.CompareTag("TreasureCarrier"))
            {
                Cart cart = other.GetComponentInParent<Cart>();
                cart.RemoveCarriedItem(this);
            }
        }
    }
}