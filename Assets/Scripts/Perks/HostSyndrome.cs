using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Host Syndrome (Legendary)
/// Gain +1 extra die for every enemy currently adjacent to you.
/// </summary>
public class HostSyndromePerk : BasePerk
{
    void OnEnable()
    {
        rarity       = PerkRarity.Legendary;
        maxLevel     = 1;
        if (string.IsNullOrEmpty(description))
            description = "Roll {bonus} for every enemy adjacent to you.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", "+1 die" }
    };

    /// <summary>Called by TurnManager before rolling dice. Returns extra dice count.</summary>
    public int GetExtraDice()
    {
        var tm = TurnManager.instance;
        if (tm == null || tm.player == null) return 0;

        int count = tm.GetAdjacentEnemies(tm.player.GetCurrentCellPosition()).Count;
        if (count > 0) TriggerVisualPop();
        return count;
    }
}
