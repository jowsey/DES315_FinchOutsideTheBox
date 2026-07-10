using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum TreasureType
{
    Flask = 0,
    Gem = 1,
}

public enum ItemType
{
    RedBox = 0,
    GreenBox = 1,
    BlueBox = 2,
}

//Lets us easily have different settings for different difficulty modes
[CreateAssetMenu(fileName = "EconomySettings", menuName = "Settings/Economy")]
public class EconomySettings : SerializedScriptableObject
{
    public Dictionary<TreasureType, int> TreasureSellPrices;
    public Dictionary<ItemType, int> ItemBuyPrices;
}