using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Items
{
    public class Treasure : Item
    {
        public bool Smashable;

        [Serializable]
        public class TreasureMeshPair
        {
            public Mesh Mesh;
            public GameObject SmashedGroup;
        }

        [SerializeField] private AK.Wwise.Event _breakSfx;

        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private MeshCollider _meshCollider;

        [SerializeField] private List<TreasureMeshPair> _randomMeshOptions = new();
        [SerializeField, ShowIf("@_randomMeshOptions.Count == 0")] private GameObject _smashedPrefab;

        [SyncVar(hook = nameof(OnChangeRandomMeshIndex))] private int _randomMeshIndex = -1;

        private const float SmashSpeed = 5.5f;

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
            if (!_meshRenderer) _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (!_meshCollider) _meshCollider = GetComponentInChildren<MeshCollider>();

            if (_randomMeshOptions.Count > 0)
            {
                _smashedPrefab = null;
            }
        }

        private void OnChangeRandomMeshIndex(int oldValue, int newValue)
        {
            if (newValue < 0 || newValue >= _randomMeshOptions.Count) return;

            var newPair = _randomMeshOptions[newValue];
            if (_meshFilter.sharedMesh != newPair.Mesh) _meshFilter.sharedMesh = newPair.Mesh;
            if (_meshCollider && _meshCollider.sharedMesh != newPair.Mesh) _meshCollider.sharedMesh = newPair.Mesh;
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

        protected override void UpdateState(ItemStateData oldState, ItemStateData newState)
        {
            // Transition out
            switch (oldState)
            {
                // No longer holding
                case HeldStateData heldData:
                {
                    if (isServer)
                    {
                        Smashable = true;
                    }

                    if (heldData.Holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("TreasureCarrier", false);
                    }

                    break;
                }
            }

            // Transition in
            switch (newState)
            {
                case HeldStateData heldData:
                {
                    if (isServer)
                    {
                        Smashable = false;
                    }

                    if (heldData.Holder.isLocalPlayer)
                    {
                        Highlight.SetHighlightable("TreasureCarrier", true);

                        if (!HintPrompt.HasShown.PickupTreasure)
                        {
                            HintPrompt.HasShown.PickupTreasure = true;
                            HintPrompt.RequestNew(new HintPrompt.HintPromptData
                            {
                                Title = "What's this?",
                                Description = "If it shines, it might just be worth something!\n\nTreasures like this can be stored for safekeeping in your caravan."
                            });
                        }
                    }

                    break;
                }
                case SmashedStateData:
                {
                    var prefab = _randomMeshIndex >= 0 && _randomMeshOptions.Count >= _randomMeshIndex - 1
                        ? _randomMeshOptions[_randomMeshIndex].SmashedGroup
                        : _smashedPrefab;

                    if (!prefab) break;

                    if (_hasInitialised)
                    {
                        _breakSfx?.Post(gameObject);

                        var smashInstance = Instantiate(prefab, transform.position, transform.rotation);
                        smashInstance.transform.localScale = _meshFilter.transform.lossyScale;

                        // if we're using a randomised group, we're responsible for "building" it
                        if (prefab != _smashedPrefab)
                        {
                            foreach (var meshCol in smashInstance.GetComponentsInChildren<MeshCollider>())
                            {
                                meshCol.convex = true;
                                meshCol.gameObject.AddComponent<Rigidbody>();

                                // some of our smashed objects have both an inside and an outside material (presumably an oversight)
                                var meshRen = meshCol.GetComponent<MeshRenderer>();
                                meshRen.SetSharedMaterials(Enumerable.Repeat(_meshRenderer.sharedMaterial, meshRen.sharedMaterials.Length).ToList());
                            }
                        }

                        // Destroy(smashInstance.gameObject, 30f); // todo animate away

                        // if (!HintPrompt.HasShown.TreasureSmash)
                        // {
                        //     HintPrompt.HasShown.TreasureSmash = true;
                        //     HintPrompt.RequestNew(new HintPrompt.HintPromptData());
                        // }
                    }

                    break;
                }
            }

            // base clears references, cleans up etc, so call last
            base.UpdateState(oldState, newState);
        }

        private void OnCollisionEnter(Collision col)
        {
            if (!isServer) return;
            if (!Smashable) return;

            if (col.relativeVelocity.magnitude < SmashSpeed) return;

            var otherLayerName = LayerMask.LayerToName(col.collider.gameObject.layer);
            if (otherLayerName is "Cart" or "Item" or "Player") return;

            ServerSetState(new SmashedStateData());
        }
    }
}