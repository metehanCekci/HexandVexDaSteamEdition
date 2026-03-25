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
    /// Diriliş: canı full yap, tüm perklerin seviyesini 1 düşür, Lv1 olanları yok et.
    /// </summary>
    public void Revive()
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        // Canı full yap
        var playerHealth = TurnManager.instance.player.health;
        playerHealth.Heal(playerHealth.maxHP);

        // Tüm perklerin seviyesini düşür
        List<BasePerk> toRemove = new List<BasePerk>();

        foreach (var p in RunManager.instance.activePerks)
        {
            if (p == this) continue; // Ouroboros kendini düşürmez

            if (p.currentLevel <= 1)
                toRemove.Add(p);
            else
                p.currentLevel--;
        }

        // Lv1 perkleri yok et
        foreach (var p in toRemove)
        {
            RunManager.instance.activePerks.Remove(p);
            Object.Destroy(p.gameObject);
        }

        TriggerVisualPop();
    }
}
