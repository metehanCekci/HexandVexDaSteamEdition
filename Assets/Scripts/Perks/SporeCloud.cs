using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SporeCloudPerk : BasePerk
{
    private bool subscribed = false;

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "When damaged, stun enemies within {radius} hex radius.";
        RebuildDescription();
    }

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "radius", $"{currentLevel}" }
    };

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

    private void OnPlayerDamaged(long remainingHP)
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
                // 2 tur stun: bu turun sonunda DecreaseStunTurn 1 düşürecek,
                // böylece saldıran düşman da bir sonraki tur stunlanmış kalır.
                enemy.ApplyStun(2, true);
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

        Vector3 center = groundMap.GetCellCenterWorld(playerCell);

        // Merkez patlama — büyük ve parlak
        StartCoroutine(SpawnSmokeParticle(center, 2.0f + currentLevel * 0.6f, 0.85f, true));

        // Her etkilenen cell'de ayrı duman parçacığı
        float delay = 0f;
        foreach (var cell in cells)
        {
            Vector3 cellPos = groundMap.GetCellCenterWorld(cell);
            // Hafif rastgele offset
            cellPos += new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.15f, 0.15f), -0.1f);
            StartCoroutine(SpawnSmokeParticleDelayed(cellPos, 1.0f + currentLevel * 0.3f, 0.7f, delay));
            delay += 0.04f;
        }

        // Ekstra küçük parçacıklar — pop hissi
        for (int i = 0; i < 4 + currentLevel * 2; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(0.3f, 0.8f + currentLevel * 0.3f);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, -0.1f);
            StartCoroutine(SpawnSmokeParticleDelayed(pos, Random.Range(0.5f, 0.9f), Random.Range(0.5f, 0.8f), Random.Range(0f, 0.1f)));
        }

        yield return new WaitForSeconds(0.6f);
    }

    private IEnumerator SpawnSmokeParticleDelayed(Vector3 position, float size, float maxAlpha, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return StartCoroutine(SpawnSmokeParticle(position, size, maxAlpha, false));
    }

    private IEnumerator SpawnSmokeParticle(Vector3 position, float size, float maxAlpha, bool isBurst)
    {
        GameObject smoke = new GameObject("SporeSmoke");
        smoke.transform.position = position + Vector3.back * 0.1f;

        SpriteRenderer sr = smoke.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.sortingOrder = 100;

        // Burst: parlak yeşil-sarı, normal: yeşil
        Color baseColor = isBurst
            ? new Color(0.5f, 0.9f, 0.2f, maxAlpha)
            : new Color(0.3f + Random.Range(0f, 0.2f), 0.7f + Random.Range(0f, 0.2f), 0.1f, maxAlpha);
        sr.color = baseColor;

        float duration = isBurst ? 1.0f : 0.7f;
        float elapsed = 0f;
        Vector3 startScale = isBurst ? Vector3.one * size * 0.3f : Vector3.zero;
        Vector3 endScale = Vector3.one * size;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Pop efekti: hızlı büyüme sonra hafif geri çekilme
            float scaleT;
            if (isBurst && t < 0.2f)
            {
                scaleT = t / 0.2f;
                scaleT = 1f + 0.15f * Mathf.Sin(scaleT * Mathf.PI); // overshoot
                smoke.transform.localScale = Vector3.Lerp(startScale, endScale * 1.15f, scaleT);
            }
            else
            {
                scaleT = 1f - (1f - t) * (1f - t); // EaseOut
                smoke.transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);
            }

            // Alpha: hızlı yüksel, yavaş düş
            float alpha;
            if (t < 0.15f)
                alpha = Mathf.Lerp(0f, maxAlpha, t / 0.15f);
            else
                alpha = Mathf.Lerp(maxAlpha, 0f, (t - 0.15f) / 0.85f);

            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

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
