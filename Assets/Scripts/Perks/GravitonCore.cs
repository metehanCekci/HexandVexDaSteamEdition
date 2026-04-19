using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GravitonCorePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "On skip, pull enemies 2-3 hexes away closer to you.";
        RebuildDescription();
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
            if (dist < 1.5f || dist > 3.5f) continue; // 2-3 hex range

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
                // Sadece çek, hasar yok
                enemy.StartKnockbackMovement(bestCell);
                pulled.Add(enemy);
            }
        }

        if (pulled.Count > 0)
        {
            // Vacuum VFX + ses (BioMagnetism ile aynı)
            if (tm.vacuumVfxPrefab != null)
                tm.StartCoroutine(VacuumVFX(tm.player.transform.position, tm.vacuumVfxPrefab));
            if (AudioManager.instance != null) AudioManager.instance.PlayVacuum();

            TriggerVisualPop();
            CameraController.ShakeLight();
        }
    }

    private IEnumerator VacuumVFX(Vector3 pos, GameObject prefab)
    {
        GameObject vfx = Instantiate(prefab, pos, Quaternion.identity);
        SpriteRenderer[] renderers = vfx.GetComponentsInChildren<SpriteRenderer>();
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 12f;
        Vector3 endScale = Vector3.one * 1.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float alpha = t < 0.2f ? Mathf.Lerp(0f, 0.4f, t / 0.2f) : Mathf.Lerp(0.4f, 0f, (t - 0.2f) / 0.8f);
            vfx.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            foreach (var sr in renderers) { if (sr != null) { Color c = sr.color; c.a = alpha; sr.color = c; } }
            yield return null;
        }
        Destroy(vfx);
    }
}
