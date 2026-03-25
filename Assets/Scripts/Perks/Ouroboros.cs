using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ouroboros (Secret)
/// Öldüğünde canın full dolar ve tüm perklerinin seviyesi 1 düşer.
/// Lv1'deki perkler yok olur. Perk kalmayınca gerçekten ölürsün.
/// maxLevel = 1, kendisi hiç yok olmaz (level düşürmeye dahil değil).
/// </summary>
public class OuroborosPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Secret;
        maxLevel = 1;
    }

    /// <summary>
    /// Canı yeterli mi kontrol et — en az 1 tane Lv1+ başka perk olmalı.
    /// </summary>
    public bool CanRevive()
    {
        if (RunManager.instance == null) return false;

        foreach (var p in RunManager.instance.activePerks)
        {
            if (p == this) continue;
            if (p.currentLevel >= 1) return true;
        }
        return false;
    }

    /// <summary>
    /// Diriliş: canı full yap, rastgele 1 perkin seviyesini 1 düşür, Lv1 ise yok et.
    /// </summary>
    public void Revive()
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        // Canı full yap
        var playerHealth = TurnManager.instance.player.health;
        playerHealth.Heal(playerHealth.maxHP);

        // Tüm perklerden (active + inventory) Ouroboros hariç birini seç
        List<BasePerk> candidates = new List<BasePerk>();

        foreach (var p in RunManager.instance.activePerks)
        {
            if (p != this && p.currentLevel >= 1) candidates.Add(p);
        }
        foreach (var p in RunManager.instance.inventoryPerks)
        {
            if (p != this && p.currentLevel >= 1) candidates.Add(p);
        }

        if (candidates.Count == 0) return;

        BasePerk target = candidates[Random.Range(0, candidates.Count)];

        if (target.currentLevel <= 1)
        {
            RunManager.instance.activePerks.Remove(target);
            RunManager.instance.inventoryPerks.Remove(target);
            Object.Destroy(target.gameObject);
        }
        else
        {
            target.currentLevel--;
        }

        // UI'ı güncelle
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.RefreshUI();
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.RefreshBar();

        TriggerVisualPop();
    }
}
