using UnityEngine;
using System.Collections.Generic;

public class GravitonCorePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
    }

    public override void OnSkip()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        var tm = TurnManager.instance;
        Vector3Int playerCell = tm.player.GetCurrentCellPosition();

        List<EnemyMovement> pulled = new List<EnemyMovement>();

        foreach (var enemy in tm.enemies)
        {
            if (enemy == null || enemy.health.currentHP <= 0 || enemy.isAllied || enemy.IsBoss) continue;

            Vector3Int enemyCell = enemy.GetCurrentCellPosition();
            float dist = HexGridUtils.DistanceCube(enemyCell, playerCell);
            if (dist < 1.5f || dist > 3.5f) continue; // 2-3 hex range (skip adjacent, skip >3)

            // Find neighbor hex closest to player
            Vector3Int[] offsets = (enemyCell.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;
            Vector3Int bestCell = enemyCell;
            float bestDist = dist;

            foreach (var off in offsets)
            {
                Vector3Int candidate = enemyCell + off;
                if (!tm.HasWalkableTile(candidate)) continue;
                if (tm.IsEnemyAtCell(candidate)) continue;
                if (tm.player.GetCurrentCellPosition() == candidate) continue;

                float d = HexGridUtils.DistanceCube(candidate, playerCell);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCell = candidate;
                }
            }

            if (bestCell != enemyCell)
            {
                // %50 max HP hasar
                int damage = Mathf.Max(1, Mathf.CeilToInt(enemy.health.maxHP * 0.5f));
                enemy.health.TakeDamage(damage);

                if (enemy.health.currentHP > 0)
                    enemy.StartKnockbackMovement(bestCell);

                pulled.Add(enemy);
            }
        }

        if (pulled.Count > 0)
        {
            TriggerVisualPop();
            CameraController.ShakeLight();
            if (AudioManager.instance != null) AudioManager.instance.PlayHit();

            // Kill reward
            if (tm.coinService != null)
            {
                foreach (var e in pulled)
                {
                    if (e != null && e.health.currentHP <= 0)
                        tm.coinService.ProcessKillRewards(e);
                }
            }
        }
    }
}
