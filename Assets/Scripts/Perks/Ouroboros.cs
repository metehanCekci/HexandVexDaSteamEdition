using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ouroboros (Secret)
/// Ã–ldÃ¼ÄŸÃ¼nde canÄ±n full dolar ve tÃ¼m perklerinin seviyesi 1 dÃ¼ÅŸer.
/// Lv1'deki perkler yok olur. Perk kalmayÄ±nca gerÃ§ekten Ã¶lÃ¼rsÃ¼n.
/// maxLevel = 1, kendisi hiÃ§ yok olmaz (level dÃ¼ÅŸÃ¼rmeye dahil deÄŸil).
/// </summary>
public class OuroborosPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "death", GameKeywords.Action("death") },
        { "full",  GameKeywords.HealthText("full HP") },
        { "loss",  GameKeywords.Status("1 level") }
    };

    /// <summary>
    /// CanÄ± yeterli mi kontrol et â€” en az 1 tane Lv1+ baÅŸka perk olmalÄ±.
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
    /// DiriliÅŸ: canÄ± full yap, rastgele 1 perkin seviyesini 1 dÃ¼ÅŸÃ¼r, Lv1 ise yok et.
    /// </summary>
    public void Revive()
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        // CanÄ± full yap
        var playerHealth = TurnManager.instance.player.health;
        playerHealth.Heal(playerHealth.maxHP);

        // TÃ¼m perklerden (active + inventory) Ouroboros hariÃ§ birini seÃ§
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

        // UI'Ä± gÃ¼ncelle
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.RefreshUI();
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.RefreshBar();

        TriggerVisualPop();
    }
}
