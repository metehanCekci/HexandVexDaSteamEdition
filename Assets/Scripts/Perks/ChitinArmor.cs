using UnityEngine;

public class ChitinArmorPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
    }

    // İlk alındığında çalışır (1. Seviye)
    public override void OnAcquire()
    {
        RunManager.instance.armorChance += 0.15f; 
        TriggerVisualPop();
    }

    // YENİ: Kart tekrar seçilirse çalışır (Seviye Atlama)
    public override void Upgrade()
    {
        base.Upgrade();

        RunManager.instance.armorChance += 0.15f;
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.multiplier *= 999f;
    }
}