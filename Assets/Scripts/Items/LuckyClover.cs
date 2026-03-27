using UnityEngine;

[CreateAssetMenu(menuName = "Items/LuckyClover", fileName = "LuckyClover")]
public class LuckyClover : BaseItem
{
    void OnEnable()
    {
        itemName = "Lucky Clover";
        description = "Equalizes all perk rarity chances on the next perk selection screen. Use during perk selection to re-roll with equal odds.";
        price = 8;
        itemType = ItemType.Consumable;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.hasLuckyClover = true;

        // If used during perk selection screen, trigger re-roll with equal chances
        if (LevelUpManager.instance != null
            && LevelUpManager.instance.levelUpPanel != null
            && LevelUpManager.instance.levelUpPanel.activeSelf)
        {
            LevelUpManager.instance.LuckyCloverReroll();
        }

        return true;
    }
}
