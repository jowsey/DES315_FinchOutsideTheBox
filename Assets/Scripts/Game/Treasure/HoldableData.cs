using UnityEngine;

namespace Game.Treasure
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
        Mythic
    }

    [CreateAssetMenu(fileName = "NewHoldable", menuName = "Game/Holdable Data")]
    public class HoldableData : ScriptableObject
    {
        [field: SerializeField] public ItemType Type { get; private set; } = ItemType.Treasure;
        [field: SerializeField] public string ItemName { get; private set; } = "Unknown item";
        [field: SerializeField] public string Description { get; private set; } = "An unknown item";
        [field: SerializeField] public ItemRarity Rarity { get; private set; } = ItemRarity.Common;
    }
}