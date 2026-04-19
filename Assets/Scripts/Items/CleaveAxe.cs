using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/CleaveAxe", fileName = "CleaveAxe")]
public class CleaveAxe : BaseItem
{
    void OnEnable()
    {
        itemName = "Cleave-Axe";
        if (string.IsNullOrEmpty(description))
            description = "Next attack deals {full} to all adjacent enemies without splitting.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "full", "full damage" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.cleaveNextCombat = true;
        return true;
    }
}
