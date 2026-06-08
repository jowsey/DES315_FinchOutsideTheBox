using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

public enum TreasureType
{
    Flask,
    Gem,
}

public enum ItemType
{
    RedBox,
    GreenBox,
    BlueBox,
}

[CreateAssetMenu(fileName = "EconomySettings", menuName = "Settings/Economy")]
public class EconomySettings : SerializedScriptableObject
{
    public Dictionary<TreasureType, int> TreasureSellPrices;
    public Dictionary<ItemType, int> ItemBuyPrices;
}