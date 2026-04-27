using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ouroboros (Secret)
/// Oldugunde canin full dolar ve N adet rastgele perk YOK olur.
/// N her revive'da artar: ilk revive 1 perk, ikinci 2, ucuncu 3...
/// Yetecek kadar perk yoksa (kalan perk sayisi < N) revive olmaz, gercekten olursun.
/// Ouroboros kendisi kurban listesine dahil degil.
/// </summary>
public class OuroborosPerk : BasePerk
{
    // Bir sonraki revive'in maliyeti. Her basarili revive'dan sonra +1.
    [System.NonSerialized] public int nextCost = 1;

    public override Dictionary<string, object> GetDescValues()
    {
        string lossText = nextCost == 1 ? "1 random implant" : nextCost + " random implants";
        return new Dictionary<string, object>
        {
            { "death", GameKeywords.Action("death") },
            { "full",  GameKeywords.HealthText("full HP") },
            { "loss",  GameKeywords.Status(lossText) }
        };
    }

    /// <summary>
    /// Yeterli kurban perk var mi? (kendisi haric, en az nextCost adet)
    /// </summary>
    public bool CanRevive()
    {
        if (RunManager.instance == null) return false;
        return CountSacrificeCandidates() >= nextCost;
    }

    private int CountSacrificeCandidates()
    {
        int count = 0;
        foreach (var p in RunManager.instance.activePerks)
            if (p != null && p != this) count++;
        foreach (var p in RunManager.instance.inventoryPerks)
            if (p != null && p != this) count++;
        return count;
    }

    /// <summary>
    /// Dirilis: cani full yap, nextCost kadar rastgele perki yok et, sonra maliyeti +1 arttir.
    /// </summary>
    public void Revive()
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        var playerHealth = TurnManager.instance.player.health;
        playerHealth.Heal(playerHealth.maxHP);

        List<BasePerk> candidates = new List<BasePerk>();
        foreach (var p in RunManager.instance.activePerks)
            if (p != null && p != this) candidates.Add(p);
        foreach (var p in RunManager.instance.inventoryPerks)
            if (p != null && p != this) candidates.Add(p);

        int toDestroy = Mathf.Min(nextCost, candidates.Count);
        for (int i = 0; i < toDestroy; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            BasePerk target = candidates[idx];
            candidates.RemoveAt(idx);

            RunManager.instance.activePerks.Remove(target);
            RunManager.instance.inventoryPerks.Remove(target);
            target.OnUnequip();
            Object.Destroy(target.gameObject);
        }

        nextCost++;
        RebuildDescription();

        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.RefreshUI();
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.RefreshBar();

        TriggerVisualPop();
    }
}
