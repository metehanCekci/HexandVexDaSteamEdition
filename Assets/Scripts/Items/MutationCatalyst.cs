using UnityEngine;

[CreateAssetMenu(menuName = "Items/MutationCatalyst", fileName = "MutationCatalyst")]
public class MutationCatalyst : BaseItem
{
    void OnEnable()
    {
        itemName = "Mutation Catalyst";
        description = "Unlocks a free item reroll in every shop for the rest of the run.";
        price = 18;
        itemType = ItemType.Instant;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        if (RunManager.instance.hasMutationCatalyst) return false; // Zaten sahip
        RunManager.instance.hasMutationCatalyst = true;
        return true;
    }
}
