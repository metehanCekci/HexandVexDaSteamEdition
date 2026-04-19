using UnityEngine;

/// <summary>
/// Host Syndrome (Legendary)
/// Gain +1 extra die for every enemy currently adjacent to you.
/// Extra dice are added before rolling in TurnManager.
/// </summary>
public class HostSyndromePerk : BasePerk
{
    void OnEnable()
    {
        perkName     = "Host Syndrome";
        description  = "Roll +1 extra die for every enemy adjacent to you.";
        rarity       = PerkRarity.Legendary;
        maxLevel     = 1;
    }

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
