using UnityEngine;

[CreateAssetMenu(menuName = "Items/LuckyClover", fileName = "LuckyClover")]
public class LuckyClover : BaseItem
{
    void OnEnable()
    {
        itemName = "Lucky Clover";
        itemType = ItemType.Consumable;
    }

    public override bool Use()
    {
        return false;
    }
}
