using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/MutationCatalyst", fileName = "MutationCatalyst")]
public class MutationCatalyst : BaseItem
{
    void OnEnable()
    {
        itemName = "Mutation Catalyst";
        if (string.IsNullOrEmpty(description))
            description = "Sets the next shop reroll cost to {cost} gold.";
        itemType = ItemType.Consumable;
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "cost", 0 }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.pendingRerollReset = true;
        if (MergedShopManager.instance != null)
            MergedShopManager.instance.ApplyFreeRerollFromCatalyst();
        return true;
    }
}
