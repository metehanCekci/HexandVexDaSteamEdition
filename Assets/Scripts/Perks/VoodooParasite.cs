using UnityEngine;

public class VoodooParasitePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 4;
        rarity = PerkRarity.Legendary;
        if (string.IsNullOrEmpty(description))
            description = "Damage dealt to enemies is also dealt to nearby enemies (voodoo curse).";
        RebuildDescription();
    }
}
