using UnityEngine;

[CreateAssetMenu(menuName = "Items/LuckyClover", fileName = "LuckyClover")]
public class LuckyClover : BaseItem
{
    void OnEnable()
    {
        itemName = "Lucky Clover";
        description = "(Disabled) This item has been removed from the game.";
        price = 0;
        itemType = ItemType.Consumable;
    }

    public override bool Use()
    {
        return false;
    }
}
