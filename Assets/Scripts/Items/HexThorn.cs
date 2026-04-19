using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/HexThorn", fileName = "HexThorn")]
public class HexThorn : BaseItem
{
    void OnEnable()
    {
        itemName = "Hex-Thorn";
        if (string.IsNullOrEmpty(description))
            description = "Place a spike trap on any empty hex tile. Breaks after {turns} turns (blinks on turn {warn}).";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "turns", 3 },
        { "warn", 2 }
    };

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;
        TurnManager.instance.StartThornPlacement();
        return true;
    }
}
