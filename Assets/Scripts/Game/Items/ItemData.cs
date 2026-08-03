using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Event = AK.Wwise.Event;

namespace Game.Items
{
    public enum ItemType
    {
        Treasure,
        Equipment
    }

    public enum ItemRarity
    {
        Common = -1,
        Uncommon = 0,
        Rare = 1,
        SuperRare = 2
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Data")]
    public class ItemData : ScriptableObject
    {
        public static readonly Dictionary<ItemRarity, Color> RarityColors = new()
        {
            { ItemRarity.Common, Color.gray5 },
            { ItemRarity.Uncommon, Color.forestGreen },
            { ItemRarity.Rare, Color.cornflowerBlue },
            { ItemRarity.SuperRare, Color.mediumPurple }
        };

        [field: SerializeField] public ItemType Type { get; private set; } = ItemType.Treasure;
        [field: SerializeField] public string ItemName { get; private set; } = "Unknown item";
        [field: SerializeField, Multiline] public string Description { get; private set; } = "An unknown item";
        [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;

        [field: SerializeField, SuffixLabel("coins")] public int BuyPrice { get; private set; } = 10;
        [field: SerializeField, SuffixLabel("coins")] public int SellPrice { get; private set; } = 5;

        [field: SerializeField] public Item Prefab { get; private set; }

        [field: SerializeField] public Event BuySfx { get; private set; }

        private void OnEnable()
        {
#if UNITY_EDITOR
            // Check if addressable
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;

            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);

            var entry = settings.FindAssetEntry(guid);

            if (settings && entry != null) return;
            Debug.LogWarning($"Item {ItemName} ({name}) isn't registered with Addressables, remember to do so!");
#endif
        }
    }
}