using UnityEngine;

public class BioMagnetismPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
    }

    public override void OnAcquire()
    {
        priority = 1; // Sıralaması önemli değil, savaş başlamadan özel olarak çağırıyoruz.
    }
}