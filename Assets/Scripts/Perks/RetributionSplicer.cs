using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Retribution Splicer (Common)
/// Each time you hit the same target, gain +2 damage against them (no stack limit).
/// At level 2: +3 per hit. At level 3: +4 per hit.
/// Stacks reset when the enemy dies.
/// </summary>
public class RetributionSplicerPerk : BasePerk
{
    // enemyInstanceID → hit count
    private readonly Dictionary<int, int> hitCounts = new Dictionary<int, int>();

    void OnEnable()
    {
        perkName    = "Retribution Splicer";
        description = "Each time you hit the same target, gain +2 flat damage against them. No stack limit. +1 more per level.";
        rarity      = PerkRarity.Common;
        maxLevel    = 3;
        priority    = 20; // Apply after other bonuses
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    /// <summary>Called by TurnManager after each hit to register the target.</summary>
    public void RegisterHit(EnemyMovement target)
    {
        if (target == null) return;
        int id = target.GetInstanceID();
        if (!hitCounts.ContainsKey(id)) hitCounts[id] = 0;
        hitCounts[id]++;
        description = GetDescription();
    }

    /// <summary>Returns the flat damage bonus against this target (based on previous hits).</summary>
    public long GetBonusFor(EnemyMovement target)
    {
        if (target == null) return 0;
        int id = target.GetInstanceID();
        if (!hitCounts.ContainsKey(id)) return 0;
        long bonusPerHit = 1 + currentLevel; // lv1=+2, lv2=+3, lv3=+4
        return hitCounts[id] * bonusPerHit;
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (enemy == null) return;
        hitCounts.Remove(enemy.GetInstanceID());
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        hitCounts.Clear();
        description = GetDescription();
    }

    private string GetDescription()
    {
        int bonusPerHit = 1 + currentLevel;
        int totalTargets = hitCounts.Count;
        int totalHits = 0;
        foreach (var kv in hitCounts) totalHits += kv.Value;
        return $"Each hit on the same target grants +{bonusPerHit} flat damage against them. No stack limit.\nTargets: {totalTargets} | Total hits: {totalHits}";
    }
}
