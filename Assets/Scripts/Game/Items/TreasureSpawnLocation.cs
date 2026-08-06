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

        public override void OnStartServer()
        {
            base.OnStartServer();
            var treasureData = Shop.ItemRegistry.Values
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
    }
}