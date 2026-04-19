using UnityEngine;
using System;
using System.Collections.Generic;

public class LootGlandPerk : BasePerk
{
    private bool bonusApplied = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        description = "Gain +2 bonus gold per kill for each level.";
    }

    // İlk alındığında çalışır (1. Seviye)
    public override void OnAcquire()
    {
        ApplyBonus();
        TriggerVisualPop();
    }

    // YENİ: Kart tekrar seçilirse çalışır (2. ve 3. Seviyeler)
    public override void Upgrade()
    {
        // Mevcut bonusu kaldır, seviye atla, yeni bonusu uygula
        RemoveBonus();
        base.Upgrade();
        ApplyBonus();
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        ApplyBonus();
    }

    public override void OnUnequip()
    {
        RemoveBonus();
    }

    private void ApplyBonus()
    {
        if (bonusApplied) return;
        bonusApplied = true;
        RunManager.instance.bonusGold += 2 * currentLevel;
    }

    private void RemoveBonus()
    {
        if (!bonusApplied) return;
        bonusApplied = false;
        RunManager.instance.bonusGold -= 2 * currentLevel;
    }
}
