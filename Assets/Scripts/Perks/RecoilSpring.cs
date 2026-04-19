using UnityEngine;

public class RecoilSpringPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "After attacking, bounce back and attack again if an enemy is adjacent.";
        RebuildDescription();
    }
}
