using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/OverClok", fileName = "OverClok")]
public class OverClok : BaseItem
{
    void OnEnable()
    {
        itemName = "Over-Clock";
        if (string.IsNullOrEmpty(description))
            description = "Deal {mult} damage on your next dice roll.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X2" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleDamageNextCombat = true;
        return true;
    }
}
