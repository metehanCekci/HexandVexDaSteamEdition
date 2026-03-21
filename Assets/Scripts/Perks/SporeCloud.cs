using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SporeCloudPerk : BasePerk
{
    private bool subscribed = false;

    void OnEnable()
    {
        maxLevel = 3;
    }

    public override void OnAcquire()
    {
        Subscribe();
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
    }

    public override void OnLevelStart()
    {
        Subscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.health.OnDamaged += OnPlayerDamaged;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null
            && TurnManager.instance.player.health != null)
        {
            TurnManager.instance.player.health.OnDamaged -= OnPlayerDamaged;
        }
        subscribed = false;
    }

    private void OnPlayerDamaged(int remainingHP)
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        TriggerVisualPop();

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        int radius = currentLevel; // Lv1=1, Lv2=2, Lv3=3

        HashSet<Vector3Int> affectedCells = GetCellsInRadius(playerCell, radius);

        // Stun enemies in range
        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy == null || enemy.health.currentHP <= 0) continue;
            Vector3Int enemyCell = enemy.GetCurrentCellPosition();
            if (affectedCells.Contains(enemyCell))
            {
                enemy.ApplyStun(1, true);
            }
        }

        // Spawn smoke VFX
        if (gameObject.activeInHierarchy)
            StartCoroutine(SpawnSmokeVFX(playerCell, affectedCells));
    }

    private HashSet<Vector3Int> GetCellsInRadius(Vector3Int center, int radius)
    {
        HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
        HashSet<Vector3Int> current = new HashSet<Vector3Int> { center };

        for (int r = 0; r < radius; r++)
        {
            HashSet<Vector3Int> next = new HashSet<Vector3Int>();
            foreach (var cell in current)
            {
                Vector3Int[] offsets = (cell.y % 2 != 0)
                    ? EnemyMovement.evenOffsets
                    : EnemyMovement.oddOffsets;
                foreach (var off in offsets)
                {
                    Vector3Int neighbor = cell + off;
                    if (!cells.Contains(neighbor) && neighbor != center)
                    {
                        cells.Add(neighbor);
                        next.Add(neighbor);
                    }
                }
            }
            current = next;
        }
        return cells;
    }

    private IEnumerator SpawnSmokeVFX(Vector3Int playerCell, HashSet<Vector3Int> cells)
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) yield break;

        var groundMap = TurnManager.instance.player.groundMap;
        if (groundMap == null) yield break;

        // Create smoke particles at player position expanding outward
        Vector3 center = groundMap.GetCellCenterWorld(playerCell);

        // Main burst at center
        yield return StartCoroutine(SpawnSmokeParticle(center, 1.5f + currentLevel * 0.5f));
    }

    private IEnumerator SpawnSmokeParticle(Vector3 position, float size)
    {
        // Create a simple sprite-based smoke puff
        GameObject smoke = new GameObject("SporeSmoke");
        smoke.transform.position = position + Vector3.back * 0.1f;

        SpriteRenderer sr = smoke.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.4f, 0.7f, 0.2f, 0.6f); // Green-ish spore color
        sr.sortingOrder = 100;

        float duration = 0.8f;
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * size;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Expand fast, fade slow
            float scaleT = 1f - (1f - t) * (1f - t); // EaseOut
            smoke.transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);

            float alpha = t < 0.3f ? 0.6f : Mathf.Lerp(0.6f, 0f, (t - 0.3f) / 0.7f);
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);

            yield return null;
        }

        Object.Destroy(smoke);
    }

    private static Sprite cachedCircle;
    private static Sprite CreateCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;

        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res / 2f;
        float radius = center - 1;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = alpha * alpha; // Soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        cachedCircle = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return cachedCircle;
    }
}
