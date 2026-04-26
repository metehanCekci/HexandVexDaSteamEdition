using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Seismic Step (Legendary)
/// Skip yaptÄ±ÄŸÄ±nda, skip yapÄ±lan tile titremeye baÅŸlar.
/// Oyuncu o tile'dan Ã§Ä±ktÄ±ÄŸÄ±nda tile Ã§Ã¶ker (scaffold mantÄ±ÄŸÄ±).
/// DÃ¼ÅŸman Ã§Ã¶ken tile Ã¼zerindeyse dÃ¼ÅŸer ve hasar alÄ±r.
/// Seviye baÅŸÄ±na ek etki: Lv2 Ã§Ã¶kme hasarÄ± 2x, Lv3 Ã§Ã¶kme hasarÄ± 3x + komÅŸu tile'lar da titrer.
/// </summary>
public class SeismicStepPerk : BasePerk
{
    // Aktif titreyen tile'lar (skip yapÄ±lmÄ±ÅŸ ama oyuncu henÃ¼z Ã§Ä±kmamÄ±ÅŸ)
    private HashSet<Vector3Int> shakingCells = new HashSet<Vector3Int>();
    private Dictionary<Vector3Int, Coroutine> shakeCoroutines = new Dictionary<Vector3Int, Coroutine>();

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "skip",     GameKeywords.Action("Skip") },
        { "collapse", GameKeywords.Action("collapse") },
        { "dmg",      GameKeywords.Plus(1, "damage") }
    };

    /// <summary>
    /// Skip yapÄ±ldÄ±ÄŸÄ±nda Ã§aÄŸrÄ±lÄ±r. Oyuncunun bulunduÄŸu tile'Ä± titretmeye baÅŸla.
    /// </summary>
    public override void OnSkip()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        // Zaten titriyor veya scaffold ise dokunma
        if (shakingCells.Contains(playerCell)) return;
        if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(playerCell)) return;

        // Ground tile olmayan hÃ¼crelere de dokunma
        if (LevelGenerator.instance == null || !LevelGenerator.instance.groundMap.HasTile(playerCell)) return;

        // Hazard tile'Ä±na da dokunma â€” diken Ã§Ã¶kerse mantÄ±k bozulur
        if (LevelGenerator.instance.hazardCells.Contains(playerCell)) return;

        // Magic tile'lar yÄ±kÄ±lmaz
        if (MagicTileManager.instance != null && MagicTileManager.instance.IsMagicTileCell(playerCell)) return;

        shakingCells.Add(playerCell);
        Coroutine shake = TurnManager.instance.StartCoroutine(ShakeCoroutine(playerCell));
        shakeCoroutines[playerCell] = shake;

        TriggerVisualPop();
    }

    /// <summary>
    /// TurnManager tarafÄ±ndan oyuncu her hareket ettiÄŸinde Ã§aÄŸrÄ±lÄ±r.
    /// Oyuncu eski tile'dan ayrÄ±ldÄ±ysa ve o tile titriyorsa Ã§Ã¶ker.
    /// </summary>
    public void OnPlayerLeftCell(Vector3Int oldCell)
    {
        if (!shakingCells.Contains(oldCell)) return;

        // Pre-collapse safety: don't collapse if it would disconnect player from all enemies
        if (TurnManager.instance != null && !TurnManager.instance.WouldRemainConnectedToEnemies(oldCell))
        {
            // Tile stabilizes â€” cancel shake, don't collapse
            StopShake(oldCell);
            shakingCells.Remove(oldCell);
            return;
        }

        // TitreÅŸimi durdur
        StopShake(oldCell);
        shakingCells.Remove(oldCell);

        // Ã‡Ã¶kme baÅŸlat
        TurnManager.instance.StartCoroutine(CollapseCoroutine(oldCell));
    }

    /// <summary>
    /// Level geÃ§iÅŸinde temizle.
    /// </summary>
    public override void OnLevelStart()
    {
        CleanupAll();
    }

    void OnDestroy()
    {
        CleanupAll();
    }

    private void CleanupAll()
    {
        foreach (var kvp in shakeCoroutines)
        {
            if (kvp.Value != null && TurnManager.instance != null)
                TurnManager.instance.StopCoroutine(kvp.Value);
        }
        shakeCoroutines.Clear();

        // Titreyen tile'larÄ±n transform'larÄ±nÄ± resetle
        if (LevelGenerator.instance != null)
        {
            Tilemap groundMap = LevelGenerator.instance.groundMap;
            Tilemap bgMap = LevelGenerator.instance.columnMap;
            foreach (var cell in shakingCells)
            {
                if (groundMap != null && groundMap.HasTile(cell))
                    groundMap.SetTransformMatrix(cell, Matrix4x4.identity);
                if (bgMap != null && bgMap.HasTile(cell))
                    bgMap.SetTransformMatrix(cell, Matrix4x4.identity);
            }
        }
        shakingCells.Clear();
    }

    public bool IsCellShaking(Vector3Int cell)
    {
        return shakingCells.Contains(cell);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€ TitreÅŸim (Scaffold ile aynÄ± kalitede) â”€â”€â”€â”€â”€â”€â”€â”€

    private IEnumerator ShakeCoroutine(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) yield break;
        Tilemap groundMap = LevelGenerator.instance.groundMap;
        Tilemap bgMap = LevelGenerator.instance.columnMap;
        if (groundMap == null) yield break;

        float elapsed = 0f;
        float intensity = 0.005f;
        float speed = 45f;

        while (shakingCells.Contains(cell))
        {
            elapsed += Time.deltaTime;

            float ox = Mathf.Sin(elapsed * speed) * intensity;
            float oy = Mathf.Cos(elapsed * speed * 1.3f) * intensity * 0.5f;

            Matrix4x4 shakeMatrix = Matrix4x4.TRS(
                new Vector3(ox, oy, 0f), Quaternion.identity, Vector3.one);

            if (groundMap.HasTile(cell))
                groundMap.SetTransformMatrix(cell, shakeMatrix);

            if (bgMap != null && bgMap.HasTile(cell))
                bgMap.SetTransformMatrix(cell, shakeMatrix);

            yield return null;
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€ Ã‡Ã¶kme (Scaffold ile aynÄ± polish seviyesinde) â”€â”€â”€â”€â”€â”€â”€â”€

    private IEnumerator CollapseCoroutine(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) yield break;
        Tilemap groundMap = LevelGenerator.instance.groundMap;
        Tilemap bgMap = LevelGenerator.instance.columnMap;

        if (AudioManager.instance != null) AudioManager.instance.PlayWall();

        // Ã‡Ã¶kme sÄ±rasÄ±nda dÃ¼ÅŸman varsa hasar ver
        int collapseDamage = 1;
        if (TurnManager.instance != null)
        {
            EnemyMovement victim = TurnManager.instance.GetEnemyAtCell(cell);
            if (victim != null && victim.health.currentHP > 0)
            {
                victim.health.TakeDamage(collapseDamage);

                // DÃ¼ÅŸmanÄ± gÃ¼venli komÅŸu hÃ¼creye it
                if (victim.health.currentHP > 0)
                {
                    Vector3Int safeCell = TurnManager.instance.GetSafeNeighborForEnemy(cell);
                    if (safeCell != cell)
                        victim.StartKnockbackMovement(safeCell);
                }
            }
        }

        // Ã‡Ã¶kme animasyonu â€” scaffold ile birebir aynÄ± kalite
        float collapseDuration = 0.35f;
        float elapsed = 0f;

        while (elapsed < collapseDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / collapseDuration;
            float scale = Mathf.Lerp(1f, 0f, t);
            float yOff = Mathf.Lerp(0f, -0.5f, t * t);
            Color fadeColor = new Color(1f, 1f, 1f, 1f - t);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(0f, yOff, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            if (groundMap != null && groundMap.HasTile(cell))
            {
                groundMap.SetTransformMatrix(cell, matrix);
                groundMap.SetTileFlags(cell, TileFlags.None);
                groundMap.SetColor(cell, fadeColor);
            }
            if (bgMap != null && bgMap.HasTile(cell))
            {
                bgMap.SetTransformMatrix(cell, matrix);
                bgMap.SetTileFlags(cell, TileFlags.None);
                bgMap.SetColor(cell, fadeColor);
            }

            yield return null;
        }

        // Tile'Ä± kaldÄ±r
        RemoveTile(groundMap, cell);
        RemoveTile(bgMap, cell);

        // validCells'den kaldÄ±r â€” artÄ±k yÃ¼rÃ¼nebilir deÄŸil
        if (LevelGenerator.instance != null)
            LevelGenerator.instance.validCells.Remove(cell);

        // VoidHunger gibi dinleyiciler iÃ§in event yayÄ±nla
        TrapTileEvents.FireTileDestroyed(cell);
    }

    private void StopShake(Vector3Int cell)
    {
        if (shakeCoroutines.ContainsKey(cell))
        {
            if (shakeCoroutines[cell] != null && TurnManager.instance != null)
                TurnManager.instance.StopCoroutine(shakeCoroutines[cell]);
            shakeCoroutines.Remove(cell);
        }

        // Transform'u resetle
        if (LevelGenerator.instance != null)
        {
            Tilemap groundMap = LevelGenerator.instance.groundMap;
            Tilemap bgMap = LevelGenerator.instance.columnMap;

            if (groundMap != null && groundMap.HasTile(cell))
                groundMap.SetTransformMatrix(cell, Matrix4x4.identity);
            if (bgMap != null && bgMap.HasTile(cell))
                bgMap.SetTransformMatrix(cell, Matrix4x4.identity);
        }
    }

    private void RemoveTile(Tilemap map, Vector3Int cell)
    {
        if (map != null && map.HasTile(cell))
        {
            map.SetTransformMatrix(cell, Matrix4x4.identity);
            map.SetColor(cell, Color.white);
            map.SetTile(cell, null);
        }
    }
}
