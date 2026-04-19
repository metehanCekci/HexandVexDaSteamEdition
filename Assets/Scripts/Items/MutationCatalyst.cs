using UnityEngine;

[CreateAssetMenu(menuName = "Items/MutationCatalyst", fileName = "MutationCatalyst")]
public class MutationCatalyst : BaseItem
{
    void OnEnable()
    {
        itemName = "Mutation Catalyst";
        description = $"Sets the next shop reroll cost to <color=#{UIColors.Gold}>0</color>.";
        price = 18;
        itemType = ItemType.Consumable;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.hasPerkReroll = true;
        RunManager.instance.pendingRerollReset = true;
        return true;
    }
}
