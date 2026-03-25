using UnityEngine;
using System.Collections.Generic;

public class GravitonCorePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
    }

    /// <summary>
    /// Called by TurnManager after an enemy is knocked back.
    /// Pulls adjacent enemies 1 hex toward the knockback origin cell.
    /// Returns the list of pulled enemies so TurnManager can check spike/scaffold.
    /// </summary>
    public List<EnemyMovement> PullAdjacentEnemies(Vector3Int originCell, EnemyMovement knockedEnemy)
    {
        List<EnemyMovement> pulled = new List<EnemyMovement>();
        if (TurnManager.instance == null) return pulled;

        var tm = TurnManager.instance;
        Vector3Int[] offsets = (originCell.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;

        foreach (var off in offsets)
        {
            Vector3Int neighborCell = originCell + off;
            EnemyMovement neighbor = tm.GetEnemyAtCell(neighborCell);
            if (neighbor == null || neighbor == knockedEnemy || neighbor.health.currentHP <= 0) continue;
            if (neighbor.IsBoss) continue;

            // Pull toward origin: find the neighbor hex of this enemy that is closest to origin
            Vector3 originWorld = tm.groundMap.GetCellCenterWorld(originCell);
            Vector3Int bestCell = neighborCell;
            float bestDist = float.MaxValue;

            Vector3Int[] nOffsets = (neighborCell.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;
            foreach (var nOff in nOffsets)
            {
                Vector3Int candidate = neighborCell + nOff;
                if (!tm.HasWalkableTile(candidate)) continue;
                if (tm.IsEnemyAtCell(candidate)) continue;
                if (tm.player != null && tm.player.GetCurrentCellPosition() == candidate) continue;

                float dist = Vector3.Distance(tm.groundMap.GetCellCenterWorld(candidate), originWorld);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = candidate;
                }
            }

            if (bestCell != neighborCell)
            {
                neighbor.StartKnockbackMovement(bestCell);
                pulled.Add(neighbor);
            }
        }

        if (pulled.Count > 0) TriggerVisualPop();
        return pulled;
    }
}
