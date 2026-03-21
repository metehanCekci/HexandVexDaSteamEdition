using UnityEngine;
using System.Collections;
using UnityEngine.Tilemaps;

public class SynapticAnchorPerk : BasePerk
{
    private bool hasAnchor = false;
    private Vector3Int anchorCell;
    private GameObject anchorVisual;

    void OnEnable()
    {
        maxLevel = 1;
    }

    public override void OnLevelStart()
    {
        hasAnchor = false;
        ClearAnchorVisual();
    }

    /// <summary>true ise bu skip'te teleport yapılacak — TurnManager bekleyecek.</summary>
    [System.NonSerialized] public bool teleportPending = false;

    public override void OnSkip()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        if (!hasAnchor)
        {
            // First skip: set anchor
            anchorCell = TurnManager.instance.player.GetCurrentCellPosition();
            hasAnchor = true;
            ShowAnchorVisual();
            TriggerVisualPop();
        }
        else
        {
            // Second skip: teleport'u hemen başlatma — HandleSkipPhase bekleyecek
            teleportPending = true;
            TriggerVisualPop();
        }
    }

    /// <summary>TurnManager tarafından çağrılır — teleport coroutine'ini çalıştırır.</summary>
    public IEnumerator ExecuteTeleport()
    {
        teleportPending = false;
        yield return TeleportToAnchor();
    }

    private IEnumerator TeleportToAnchor()
    {
        var player = TurnManager.instance.player;
        Vector3Int fromCell = player.GetCurrentCellPosition();

        // Anchor hucresinde dusman var mi? Varsa yer degistir
        EnemyMovement swapEnemy = null;
        if (TurnManager.instance != null)
        {
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e != null && e.health.currentHP > 0 && e.GetCurrentCellPosition() == anchorCell)
                { swapEnemy = e; break; }
            }
        }

        // Disappear VFX — player
        SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            float dur = 0.15f;
            float elapsed = 0f;
            Color startColor = sr.color;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                yield return null;
            }
        }

        // Disappear VFX — swap enemy (ayni anda fade)
        SpriteRenderer enemySR = null;
        Color enemyOrigColor = Color.white;
        if (swapEnemy != null)
        {
            enemySR = swapEnemy.GetComponentInChildren<SpriteRenderer>();
            if (enemySR != null)
            {
                enemyOrigColor = enemySR.color;
                float dur = 0.15f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / dur;
                    enemySR.color = new Color(enemyOrigColor.r, enemyOrigColor.g, enemyOrigColor.b, Mathf.Lerp(1f, 0f, t));
                    yield return null;
                }
            }
        }

        // Teleport
        if (ScaffoldManager.instance != null)
            ScaffoldManager.instance.OnEntityLeave(fromCell);

        player.ForceSetPosition(anchorCell);

        if (ScaffoldManager.instance != null)
            ScaffoldManager.instance.OnEntityEnter(anchorCell);

        // Swap enemy: oyuncunun eski yerine tasi
        if (swapEnemy != null)
        {
            swapEnemy.ForceSetPosition(fromCell);
        }

        // Reappear VFX — player
        if (sr != null)
        {
            float dur = 0.2f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float scale = 1f + (1f - t) * 0.3f;
                player.transform.localScale = Vector3.one * scale;
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            player.transform.localScale = Vector3.one;
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        // Reappear VFX — swap enemy
        if (swapEnemy != null && enemySR != null)
        {
            float dur = 0.2f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                enemySR.color = new Color(enemyOrigColor.r, enemyOrigColor.g, enemyOrigColor.b, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            enemySR.color = enemyOrigColor;
        }

        CameraController.ShakeLight();
        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();

        // Reset anchor — next skip will set new anchor
        hasAnchor = false;
        ClearAnchorVisual();

        player.UpdateHighlights();
    }

    private void ShowAnchorVisual()
    {
        ClearAnchorVisual();

        if (TurnManager.instance == null || TurnManager.instance.player == null) return;
        Tilemap groundMap = TurnManager.instance.player.groundMap;
        if (groundMap == null) return;

        Vector3 worldPos = groundMap.GetCellCenterWorld(anchorCell);
        worldPos.z = -0.1f;

        anchorVisual = new GameObject("SynapticAnchorMarker");
        anchorVisual.transform.position = worldPos;

        SpriteRenderer sr = anchorVisual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDiamondSprite();
        sr.color = new Color(0.3f, 0.8f, 1f, 0.6f); // Cyan anchor
        sr.sortingOrder = 5;
        anchorVisual.transform.localScale = Vector3.one * 0.5f;
    }

    private void ClearAnchorVisual()
    {
        if (anchorVisual != null)
        {
            Object.Destroy(anchorVisual);
            anchorVisual = null;
        }
    }

    void OnDestroy()
    {
        ClearAnchorVisual();
    }

    private static Sprite cachedDiamond;
    private static Sprite CreateDiamondSprite()
    {
        if (cachedDiamond != null) return cachedDiamond;

        int res = 32;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res / 2f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = Mathf.Abs(x - center) / center;
                float dy = Mathf.Abs(y - center) / center;
                float dist = dx + dy; // Diamond shape
                float alpha = dist < 0.8f ? 1f : Mathf.Clamp01(1f - (dist - 0.8f) / 0.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        cachedDiamond = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return cachedDiamond;
    }
}
