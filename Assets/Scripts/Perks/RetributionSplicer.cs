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
    private readonly Dictionary<int, int> hitCounts = new Dictionary<int, int>();

    public override Dictionary<string, object> GetDescValues()
    {
        int bonusPerHit = 1 + currentLevel;
        int totalHits = 0;
        foreach (var kv in hitCounts) totalHits += kv.Value;
        return new Dictionary<string, object>
        {
            { "bonus",   GameKeywords.Plus(bonusPerHit, "damage") },
            { "targets", GameKeywords.Counter(hitCounts.Count.ToString()) },
            { "hits",    GameKeywords.Counter(totalHits.ToString()) }
        };
    }

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    /// <summary>Called by TurnManager after each hit to register the target.</summary>
    public void RegisterHit(EnemyMovement target)
    {
        if (target == null) return;
        int id = target.GetInstanceID();
        if (!hitCounts.ContainsKey(id)) hitCounts[id] = 0;
        hitCounts[id]++;
        RebuildDescription();
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
        RebuildDescription();
    }

    public override void OnLevelStart()
    {
        hitCounts.Clear();
        RebuildDescription();
    }
}
