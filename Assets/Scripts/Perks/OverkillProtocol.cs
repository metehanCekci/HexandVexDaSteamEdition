using UnityEngine;
using System.Collections.Generic;

public class OverkillProtocolPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Overkill damage carries over to a random living enemy.";
        RebuildDescription();
    }

    /// <summary>
    /// Called by TurnManager after an enemy dies from combat damage.
    /// Returns the overkill amount to transfer to a random living enemy.
    /// </summary>
    public void TransferOverkill(EnemyMovement deadEnemy, long overkillAmount)
    {
        if (overkillAmount <= 0 || TurnManager.instance == null) return;

        // Find a random living enemy
        List<EnemyMovement> alive = new List<EnemyMovement>();
        foreach (var e in TurnManager.instance.enemies)
        {
            if (e != null && e != deadEnemy && e.health.currentHP > 0)
                alive.Add(e);
        }

        if (alive.Count == 0) return;

        EnemyMovement target = alive[Random.Range(0, alive.Count)];
        target.health.TakeDamage(overkillAmount, true);
        TriggerVisualPop();
    }
}
