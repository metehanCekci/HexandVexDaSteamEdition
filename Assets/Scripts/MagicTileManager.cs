using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class MagicTileManager : MonoBehaviour
{
    public static MagicTileManager instance;

    [Header("Magic Tile Assets")]
    public TileBase redTile;
    public TileBase blueTile;
    public TileBase greenTile;
    public TileBase yellowTile;
    public TileBase orangeTile;

    [Header("Tilemap")]
    public Tilemap magicTileMap;

    // Runtime: hangi cell'de hangi magic tile var
    private Dictionary<Vector3Int, MagicTileType> activeTiles = new Dictionary<Vector3Int, MagicTileType>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Level başında çağrılır. Oyuncunun sahip olduğu tüm magic tile'ları haritaya yerleştirir.
    /// </summary>
    public void SpawnMagicTiles()
    {
        ClearActiveTiles();

        if (RunManager.instance == null) return;
        List<MagicTileType> tiles = RunManager.instance.acquiredMagicTiles;
        if (tiles.Count == 0) return;

        EnsureTilemap();
        if (magicTileMap == null) return;

        List<Vector3Int> validCells = GetValidSpawnCells();
        if (validCells.Count == 0) return;

        List<Vector3Int> spawnedCells = new List<Vector3Int>();

        foreach (var tileType in tiles)
        {
            Vector3Int cell = PickSmartCell(tileType, validCells);
            if (cell.y == -999) continue;

            TileBase tile = GetTileAsset(tileType);
            if (tile == null) continue;

            magicTileMap.SetTile(cell, tile);
            activeTiles[cell] = tileType;
            spawnedCells.Add(cell);
            validCells.Remove(cell);
        }

        if (spawnedCells.Count > 0)
        {
            StartCoroutine(SpawnAnimation(spawnedCells));
        }
    }

    /// <summary>
    /// Oyuncu şu an bir magic tile üzerinde mi? Hangi tip?
    /// </summary>
    public bool IsPlayerOnMagicTile(out MagicTileType tileType)
    {
        tileType = MagicTileType.Red;
        if (TurnManager.instance == null || TurnManager.instance.player == null) return false;

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        if (activeTiles.TryGetValue(playerCell, out tileType))
            return true;

        return false;
    }

    /// <summary>
    /// Belirli bir cell magic tile mi?
    /// </summary>
    public bool IsMagicTileCell(Vector3Int cell)
    {
        return activeTiles.ContainsKey(cell);
    }

    /// <summary>
    /// Tek kullanım: tile'ı tüket ve haritadan kaldır.
    /// </summary>
    public void ConsumeTile(Vector3Int cell)
    {
        if (!activeTiles.ContainsKey(cell)) return;
        activeTiles.Remove(cell);
        if (magicTileMap != null)
            magicTileMap.SetTile(cell, null);
    }

    /// <summary>
    /// Oyuncunun üzerinde durduğu magic tile'ı tüket.
    /// </summary>
    public void ConsumePlayerTile()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;
        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        ConsumeTile(playerCell);
    }

    // ─── Blue tile tracking (consumed after 2-hex move) ───
    private Vector3Int blueTileActiveCell = new Vector3Int(0, -999, 0);

    public void MarkBlueTileActive(Vector3Int cell) { blueTileActiveCell = cell; }

    public void CheckBlueTileConsumption(Vector3Int newPlayerCell)
    {
        if (blueTileActiveCell.y == -999) return;
        float dist = HexGridUtils.DistanceCube(blueTileActiveCell, newPlayerCell);
        if (dist >= 2f)
            ConsumeTile(blueTileActiveCell);
        blueTileActiveCell = new Vector3Int(0, -999, 0);
    }

    // ─── Orange tile tracking (consumed when player leaves) ───
    private Vector3Int orangeTileActiveCell = new Vector3Int(0, -999, 0);

    public void MarkOrangeTileActive(Vector3Int cell) { orangeTileActiveCell = cell; }

    public void CheckOrangeTileConsumption(Vector3Int newPlayerCell)
    {
        if (orangeTileActiveCell.y == -999) return;
        if (orangeTileActiveCell != newPlayerCell)
            ConsumeTile(orangeTileActiveCell);
        orangeTileActiveCell = new Vector3Int(0, -999, 0);
    }

    /// <summary>
    /// Level sonunda veya reset'te temizle.
    /// </summary>
    public void ClearActiveTiles()
    {
        if (magicTileMap != null)
            magicTileMap.ClearAllTiles();
        activeTiles.Clear();
        blueTileActiveCell = new Vector3Int(0, -999, 0);
        orangeTileActiveCell = new Vector3Int(0, -999, 0);
    }

    // ─── Smart Positioning ───

    private Vector3Int PickSmartCell(MagicTileType type, List<Vector3Int> candidates)
    {
        if (candidates.Count == 0) return new Vector3Int(0, -999, 0);

        Vector3Int playerCell = Vector3Int.zero;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        // Red (damage): near enemies, away from player
        // Blue (move): near player
        // Green (dice): medium distance from player
        // Yellow (gold): away from enemies

        switch (type)
        {
            case MagicTileType.Red:
                return PickCellNearEnemies(candidates, playerCell);
            case MagicTileType.Blue:
                return PickCellNearPlayer(candidates, playerCell);
            case MagicTileType.Green:
                return PickCellMediumDistance(candidates, playerCell);
            case MagicTileType.Yellow:
                return PickCellAwayFromEnemies(candidates, playerCell);
            case MagicTileType.Orange:
                return PickCellMediumDistance(candidates, playerCell);
            default:
                return candidates[Random.Range(0, candidates.Count)];
        }
    }

    private Vector3Int PickCellNearEnemies(List<Vector3Int> candidates, Vector3Int playerCell)
    {
        if (TurnManager.instance == null || TurnManager.instance.enemies.Count == 0)
            return candidates[Random.Range(0, candidates.Count)];

        Vector3Int best = candidates[0];
        float bestScore = float.MaxValue;
        foreach (var cell in candidates)
        {
            float minEnemyDist = float.MaxValue;
            foreach (var enemy in TurnManager.instance.enemies)
            {
                if (enemy == null) continue;
                float d = HexGridUtils.DistanceCube(cell, enemy.GetCurrentCellPosition());
                if (d < minEnemyDist) minEnemyDist = d;
            }
            float playerDist = HexGridUtils.DistanceCube(cell, playerCell);
            float score = minEnemyDist - playerDist * 0.3f; // Closer to enemies, further from player preferred
            if (score < bestScore) { bestScore = score; best = cell; }
        }
        return best;
    }

    private Vector3Int PickCellNearPlayer(List<Vector3Int> candidates, Vector3Int playerCell)
    {
        Vector3Int best = candidates[0];
        float bestDist = float.MaxValue;
        foreach (var cell in candidates)
        {
            float d = HexGridUtils.DistanceCube(cell, playerCell);
            if (d > 0 && d < bestDist) { bestDist = d; best = cell; }
        }
        return best;
    }

    private Vector3Int PickCellMediumDistance(List<Vector3Int> candidates, Vector3Int playerCell)
    {
        // Aim for ~2-3 hex distance from player
        Vector3Int best = candidates[0];
        float bestScore = float.MaxValue;
        foreach (var cell in candidates)
        {
            float d = HexGridUtils.DistanceCube(cell, playerCell);
            float score = Mathf.Abs(d - 2.5f);
            if (score < bestScore) { bestScore = score; best = cell; }
        }
        return best;
    }

    private Vector3Int PickCellAwayFromEnemies(List<Vector3Int> candidates, Vector3Int playerCell)
    {
        if (TurnManager.instance == null || TurnManager.instance.enemies.Count == 0)
            return candidates[Random.Range(0, candidates.Count)];

        Vector3Int best = candidates[0];
        float bestScore = float.MinValue;
        foreach (var cell in candidates)
        {
            float minEnemyDist = float.MaxValue;
            foreach (var enemy in TurnManager.instance.enemies)
            {
                if (enemy == null) continue;
                float d = HexGridUtils.DistanceCube(cell, enemy.GetCurrentCellPosition());
                if (d < minEnemyDist) minEnemyDist = d;
            }
            if (minEnemyDist > bestScore) { bestScore = minEnemyDist; best = cell; }
        }
        return best;
    }

    // ─── Valid Spawn Cells ───

    private List<Vector3Int> GetValidSpawnCells()
    {
        List<Vector3Int> result = new List<Vector3Int>();
        if (LevelGenerator.instance == null) return result;

        Vector3Int playerCell = Vector3Int.zero;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        HashSet<Vector3Int> enemyCells = new HashSet<Vector3Int>();
        if (TurnManager.instance != null)
        {
            foreach (var enemy in TurnManager.instance.enemies)
            {
                if (enemy != null)
                    enemyCells.Add(enemy.GetCurrentCellPosition());
            }
        }

        Tilemap groundMap = LevelGenerator.instance.groundMap;
        HashSet<Vector3Int> hazardCells = LevelGenerator.instance.hazardCells;
        HashSet<Vector3Int> scaffoldCells = LevelGenerator.instance.scaffoldCells;

        // Iterate all ground tiles
        BoundsInt bounds = groundMap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!groundMap.HasTile(cell)) continue;
                if (cell == playerCell) continue;
                if (enemyCells.Contains(cell)) continue;
                if (hazardCells.Contains(cell)) continue;
                if (scaffoldCells.Contains(cell)) continue;
                result.Add(cell);
            }
        }

        return result;
    }

    // ─── Tilemap ───

    private void EnsureTilemap()
    {
        if (magicTileMap != null) return;

        GameObject existing = GameObject.Find("MagicTileMap");
        if (existing != null)
        {
            magicTileMap = existing.GetComponent<Tilemap>();
            if (magicTileMap != null)
            {
                magicTileMap.tileAnchor = Vector3.zero;
                return;
            }
        }

        // Find the Grid parent (same as other tilemaps)
        Grid grid = FindAnyObjectByType<Grid>();
        if (grid == null) return;

        GameObject go = new GameObject("MagicTileMap");
        go.transform.SetParent(grid.transform, false);
        magicTileMap = go.AddComponent<Tilemap>();
        magicTileMap.tileAnchor = Vector3.zero; // Match ground tilemap anchor
        TilemapRenderer tr = go.AddComponent<TilemapRenderer>();
        tr.sortingOrder = 1; // Above ground, below characters
    }

    private TileBase GetTileAsset(MagicTileType type)
    {
        TileBase tile = null;
        switch (type)
        {
            case MagicTileType.Red: tile = redTile; break;
            case MagicTileType.Blue: tile = blueTile; break;
            case MagicTileType.Green: tile = greenTile; break;
            case MagicTileType.Yellow: tile = yellowTile; break;
            case MagicTileType.Orange: tile = orangeTile; break;
        }

        // Fallback: if tile asset not assigned, try loading from Resources
        if (tile == null)
        {
            string name = type.ToString() + "MagicTile";
            tile = Resources.Load<TileBase>(name);
            if (tile != null)
            {
                switch (type)
                {
                    case MagicTileType.Red: redTile = tile; break;
                    case MagicTileType.Blue: blueTile = tile; break;
                    case MagicTileType.Green: greenTile = tile; break;
                    case MagicTileType.Yellow: yellowTile = tile; break;
                    case MagicTileType.Orange: orangeTile = tile; break;
                }
            }
        }

        // Last fallback: create a simple colored tile at runtime
        if (tile == null)
        {
            Debug.LogWarning($"[MagicTileManager] No tile asset found for {type}, using fallback. Assign in Inspector or run Tools > Hex and Vex > Setup Magic Tiles.");
            tile = CreateFallbackTile(type);
        }

        return tile;
    }

    private static Dictionary<MagicTileType, Tile> fallbackTiles = new Dictionary<MagicTileType, Tile>();

    private Tile CreateFallbackTile(MagicTileType type)
    {
        if (fallbackTiles.TryGetValue(type, out Tile cached)) return cached;

        Color color;
        switch (type)
        {
            case MagicTileType.Red: color = new Color(0.9f, 0.15f, 0.15f, 0.6f); break;
            case MagicTileType.Blue: color = new Color(0.15f, 0.4f, 0.9f, 0.6f); break;
            case MagicTileType.Green: color = new Color(0.15f, 0.75f, 0.25f, 0.6f); break;
            case MagicTileType.Yellow: color = new Color(0.9f, 0.8f, 0.15f, 0.6f); break;
            case MagicTileType.Orange: color = new Color(1f, 0.5f, 0.1f, 0.6f); break;
            default: color = Color.white; break;
        }

        // Create a simple colored hex sprite matching ground tile dimensions (50x25 @ PPU=100)
        int w = 50, h = 25;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float cx = w / 2f, cy = h / 2f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Diamond/hex shape approximation
                float dx = Mathf.Abs(x - cx) / cx;
                float dy = Mathf.Abs(y - cy) / cy;
                tex.SetPixel(x, y, (dx + dy <= 1.1f) ? color : Color.clear);
            }
        }
        tex.Apply();

        // PPU=100 + center pivot to match ground tile sprites
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);

        Tile newTile = ScriptableObject.CreateInstance<Tile>();
        newTile.sprite = sprite;
        newTile.color = Color.white;

        fallbackTiles[type] = newTile;
        return newTile;
    }

    // ─── Spawn Animation ───

    private IEnumerator SpawnAnimation(List<Vector3Int> cells)
    {
        if (magicTileMap == null) yield break;

        // Start all tiles invisible (scale 0)
        foreach (var cell in cells)
        {
            magicTileMap.SetTileFlags(cell, TileFlags.None);
            magicTileMap.SetTransformMatrix(cell, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero));
            magicTileMap.SetColor(cell, new Color(1f, 1f, 1f, 0f));
        }

        yield return new WaitForSeconds(0.3f);

        // Calculate center of spawned tiles in world space
        CameraController camCtrl = Object.FindFirstObjectByType<CameraController>();
        Vector3 tileCenter = Vector3.zero;
        foreach (var cell in cells)
            tileCenter += magicTileMap.CellToWorld(cell);
        tileCenter /= cells.Count;

        // Camera pan + zoom to tile center
        Vector3 startPos = camCtrl != null ? camCtrl.GetTargetPosition() : Vector3.zero;
        Vector3 zoomTarget = new Vector3(tileCenter.x, tileCenter.y, startPos.z + 3f);

        float zoomDur = 0.4f;
        float elapsed = 0f;
        while (camCtrl != null && elapsed < zoomDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDur;
            float ease = t * t * (3f - 2f * t);
            camCtrl.SetTargetPosition(Vector3.Lerp(startPos, zoomTarget, ease));
            yield return null;
        }

        // Animate each tile appearing one by one
        float perTileDur = 0.35f;
        float stagger = 0.15f;

        for (int i = 0; i < cells.Count; i++)
        {
            StartCoroutine(AnimateSingleTileSpawn(cells[i], perTileDur));
            if (i < cells.Count - 1)
                yield return new WaitForSeconds(stagger);
        }

        // Wait for last tile to finish
        yield return new WaitForSeconds(perTileDur);

        // Hold briefly
        yield return new WaitForSeconds(0.2f);

        // Camera pan + zoom back
        elapsed = 0f;
        while (camCtrl != null && elapsed < zoomDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / zoomDur;
            float ease = t * t * (3f - 2f * t);
            camCtrl.SetTargetPosition(Vector3.Lerp(zoomTarget, startPos, ease));
            yield return null;
        }
        if (camCtrl != null) camCtrl.SetTargetPosition(startPos);
    }

    private IEnumerator AnimateSingleTileSpawn(Vector3Int cell, float duration)
    {
        if (magicTileMap == null || !magicTileMap.HasTile(cell)) yield break;

        if (AudioManager.instance != null) AudioManager.instance.PlayHit();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Overshoot bounce: scale goes 0 → 1.15 → 1.0
            float scale;
            if (t < 0.6f)
            {
                float t1 = t / 0.6f;
                scale = Mathf.Lerp(0f, 1.15f, t1 * t1);
            }
            else
            {
                float t2 = (t - 0.6f) / 0.4f;
                scale = Mathf.Lerp(1.15f, 1f, t2);
            }

            float alpha = Mathf.Clamp01(t * 3f); // fade in fast in first third

            magicTileMap.SetTransformMatrix(cell, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale));
            magicTileMap.SetColor(cell, new Color(1f, 1f, 1f, alpha));
            yield return null;
        }

        magicTileMap.SetTransformMatrix(cell, Matrix4x4.identity);
        magicTileMap.SetColor(cell, Color.white);
    }
}
