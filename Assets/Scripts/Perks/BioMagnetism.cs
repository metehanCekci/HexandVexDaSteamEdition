using UnityEngine;

public class BioMagnetismPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Pull all enemies toward you at the start of each level.";
        RebuildDescription();
    }

    public override void OnAcquire()
    {
        priority = 1; // Sıralaması önemli değil, savaş başlamadan özel olarak çağırıyoruz.
    }
}