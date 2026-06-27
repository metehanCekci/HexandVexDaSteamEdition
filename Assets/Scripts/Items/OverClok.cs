using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/OverClok", fileName = "OverClok")]
public class OverClok : BaseItem
{
    void OnEnable()
    {
        itemName = "Over-Clock";
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X2" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleDamageNextCombatStacks++;
        return true;
    }
}
