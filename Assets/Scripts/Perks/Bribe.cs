using UnityEngine;

public class BribePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Shop reroll is free.";
        RebuildDescription();
    }
}
