using System;
using System.Linq;
using Mirror;
using UnityEngine;

namespace Game.Items
{
    public class TreasureSpawnLocation : NetworkBehaviour
    {
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        private Item _spawnedItem;

        private void OnDrawGizmos()
        {
            Gizmos.color = ItemData.RarityColors[_rarity];
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            name = $"TreasureSpawnLocation ({_rarity})";
        }

        [Server]
        private void SpawnNewItem()
        {
            var treasureData = Shop.ItemRegistry
                .Where(i => i.Type == ItemType.Treasure && i.Rarity == _rarity)
                .OrderBy(x => Guid.NewGuid())
                .FirstOrDefault();

            if (!treasureData)
            {
                Debug.LogWarning($"There are no treasures to spawn with rarity {_rarity}");
                return;
            }

            var rotation = transform.rotation * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            var itemInstance = Instantiate(treasureData.Prefab, transform.position, rotation);
            NetworkServer.Spawn(itemInstance.gameObject);
            _spawnedItem = itemInstance;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Checkpoint.RespawnEvent.AddListener(OnServerRespawn);

            SpawnNewItem();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Checkpoint.RespawnEvent.RemoveListener(OnServerRespawn);
        }

        [Server]
        private void OnServerRespawn(Checkpoint checkpoint)
        {
            // todo filter to current checkpoint only? or otherwise only picked-up ones?
            // _spawnedItem.transform.position = transform.position;
            // _spawnedItem.ServerSetIdle();

            if (_spawnedItem)
            {
                NetworkServer.Destroy(_spawnedItem.gameObject);
                _spawnedItem = null;
            }

            SpawnNewItem();
        }
    }
}