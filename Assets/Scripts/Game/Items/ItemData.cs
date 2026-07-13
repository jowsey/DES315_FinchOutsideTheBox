using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Items
{
    public enum ItemType
    {
        Treasure,
        Equipment
    }

    public enum ItemRarity
    {
        Common,
        Rare,
        SuperRare
    }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Data")]
    public class ItemData : ScriptableObject
    {
        [field: SerializeField] public ItemType Type { get; private set; } = ItemType.Treasure;
        [field: SerializeField] public string ItemName { get; private set; } = "Unknown item";
        [field: SerializeField, Multiline] public string Description { get; private set; } = "An unknown item";
        [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;

        [field: SerializeField, SuffixLabel("coins")] public int BuyPrice { get; private set; } = 10;
        [field: SerializeField, SuffixLabel("coins")] public int SellPrice { get; private set; } = 5;

        [field: SerializeField] public Item Prefab { get; private set; }
    }
}