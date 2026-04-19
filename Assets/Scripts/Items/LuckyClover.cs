using UnityEngine;

[CreateAssetMenu(menuName = "Items/LuckyClover", fileName = "LuckyClover")]
public class LuckyClover : BaseItem
{
    void OnEnable()
    {
        itemName = "Lucky Clover";
        if (string.IsNullOrEmpty(description))
            description = "(Disabled) This item has been removed from the game.";
        itemType = ItemType.Consumable;
        RebuildDescription();
    }

    public override bool Use()
    {
        return false;
    }
}
