using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class MagicTileManager : MonoBehaviour
{
    public static MagicTileManager instance;

    [System.Serializable]
    public class MagicTileEntry
    {
        public MagicTileType type;
        [Tooltip("Ground layer tile (50x25, pivot 0.5/0.5)")]
        public TileBase groundTile;
        [Tooltip("Column layer tile (50x41, pivot ~0.5/0.69)")]
        public TileBase columnTile;
    }

    [Header("Magic Tile Data")]
    public MagicTileEntry[] tileEntries = new MagicTileEntry[0];

    // Runtime lookup — built from tileEntries
    private Dictionary<MagicTileType, MagicTileEntry> entryMap;

    // Active magic tiles on the map: cell → type
    private Dictionary<Vector3Int, MagicTileType> activeTiles = new Dictionary<Vector3Int, MagicTileType>();

    // Saved originals for restore on consume/clear
    private Dictionary<Vector3Int, TileBase> savedGround = new Dictionary<Vector3Int, TileBase>();
    private Dictionary<Vector3Int, TileBase> savedColumn = new Dictionary<Vector3Int, TileBase>();

    // Blue tile: consume after 2-hex move away
    private Vector3Int blueTileCell = new Vector3Int(0, -999, 0);
    // Orange tile: consume when player leaves the cell
    private Vector3Int orangeTileCell = new Vector3Int(0, -999, 0);

    // ─────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────

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
            return;
        }
        RebuildLookup();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void RebuildLookup()
    {
        entryMap = new Dictionary<MagicTileType, MagicTileEntry>();
        if (tileEntries == null) return;
        foreach (var e in tileEntries)
            if (e != null) entryMap[e.type] = e;
    }

    // ─────────────────────────────────────────
    //  SPAWN
    // ─────────────────────────────────────────

    public void SpawnMagicTiles()
    {
        ClearActiveTiles();
        if (entryMap == null) RebuildLookup();

        if (RunManager.instance == null) return;
        List<MagicTileType> tiles = RunManager.instance.acquiredMagicTiles;
        if (tiles == null || tiles.Count == 0) return;
        if (LevelGenerator.instance == null) return;

        Tilemap groundMap = LevelGenerator.instance.groundMap;
        Tilemap columnMap = LevelGenerator.instance.columnMap;
        if (groundMap == null) return;

        // Only cells that already have a ground tile — never empty cells
        List<Vector3Int> candidates = GetOccupiedCells(groundMap);
        if (candidates.Count == 0) return;

        List<Vector3Int> spawned = new List<Vector3Int>();

        foreach (var type in tiles)
        {
            if (candidates.Count == 0) break;

            Vector3Int cell = PickSmartCell(type, candidates);
            if (cell.y == -999) continue;

            // Save originals before replacing
            savedGround[cell] = groundMap.GetTile(cell);
            if (columnMap != null && columnMap.HasTile(cell))
                savedColumn[cell] = columnMap.GetTile(cell);

            MagicTileEntry entry = GetEntry(type);

            // Replace ground tile (if magic ground sprite exists)
            if (entry != null && entry.groundTile != null)
            {
                groundMap.SetTile(cell, entry.groundTile);
            }
            else
            {
                Debug.LogWarning($"[MagicTile] {type} ground tile eksik — ground degistirilmedi. Editor'da Tools > Hex and Vex > Setup Magic Tile Assets calistir.");
            }

            // Replace column tile (only if original cell had a column)
            if (entry != null && entry.columnTile != null && columnMap != null && savedColumn.ContainsKey(cell))
            {
                columnMap.SetTile(cell, entry.columnTile);
            }
            else if (entry == null || entry.columnTile == null)
            {
                Debug.LogWarning($"[MagicTile] {type} column tile eksik — column degistirilmedi.");
            }

            activeTiles[cell] = type;
            spawned.Add(cell);
            candidates.Remove(cell);
        }

        if (spawned.Count > 0)
            StartCoroutine(FadeInAnimation(spawned));
    }

    // ─────────────────────────────────────────
    //  QUERY
    // ─────────────────────────────────────────

    public bool IsPlayerOnMagicTile(out MagicTileType tileType)
    {
        tileType = MagicTileType.Red;
        if (TurnManager.instance == null || TurnManager.instance.player == null) return false;
        Vector3Int cell = TurnManager.instance.player.GetCurrentCellPosition();
        return activeTiles.TryGetValue(cell, out tileType);
    }

    public bool IsMagicTileCell(Vector3Int cell)
    {
        return activeTiles.ContainsKey(cell);
    }

    // ─────────────────────────────────────────
    //  CONSUME
    // ─────────────────────────────────────────

    public void ConsumeTile(Vector3Int cell)
    {
        if (!activeTiles.ContainsKey(cell)) return;
        activeTiles.Remove(cell);
        RestoreOriginal(cell);
    }

    public void ConsumePlayerTile()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;
        ConsumeTile(TurnManager.instance.player.GetCurrentCellPosition());
    }

    // ── Blue tile (consume after 2-hex move) ──

    public void MarkBlueTileActive(Vector3Int cell) => blueTileCell = cell;

    public void CheckBlueTileConsumption(Vector3Int newCell)
    {
        if (blueTileCell.y == -999) return;
        if (HexGridUtils.DistanceCube(blueTileCell, newCell) >= 2f)
            ConsumeTile(blueTileCell);
        blueTileCell = new Vector3Int(0, -999, 0);
    }

    // ── Orange tile (consume when player leaves) ──

    public void MarkOrangeTileActive(Vector3Int cell) => orangeTileCell = cell;

    public void CheckOrangeTileConsumption(Vector3Int newCell)
    {
        if (orangeTileCell.y == -999) return;
        if (orangeTileCell != newCell)
            ConsumeTile(orangeTileCell);
        orangeTileCell = new Vector3Int(0, -999, 0);
    }

    // ─────────────────────────────────────────
    //  CLEAR / RESTORE
    // ─────────────────────────────────────────

    public void ClearActiveTiles()
    {
        foreach (var cell in new List<Vector3Int>(activeTiles.Keys))
            RestoreOriginal(cell);

        activeTiles.Clear();
        savedGround.Clear();
        savedColumn.Clear();
        blueTileCell = new Vector3Int(0, -999, 0);
        orangeTileCell = new Vector3Int(0, -999, 0);
    }

    private void RestoreOriginal(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) return;

        Tilemap gm = LevelGenerator.instance.groundMap;
        Tilemap cm = LevelGenerator.instance.columnMap;

        if (savedGround.TryGetValue(cell, out TileBase gt) && gm != null)
        {
            gm.SetTile(cell, gt);
            gm.SetTileFlags(cell, TileFlags.None);
            gm.SetTransformMatrix(cell, Matrix4x4.identity);
            gm.SetColor(cell, Color.white);
            savedGround.Remove(cell);
        }

        if (savedColumn.TryGetValue(cell, out TileBase ct) && cm != null)
        {
            cm.SetTile(cell, ct);
            cm.SetTileFlags(cell, TileFlags.None);
            cm.SetTransformMatrix(cell, Matrix4x4.identity);
            cm.SetColor(cell, Color.white);
            savedColumn.Remove(cell);
        }
    }

    // ─────────────────────────────────────────
    //  CELL SELECTION
    // ─────────────────────────────────────────

    private List<Vector3Int> GetOccupiedCells(Tilemap groundMap)
    {
        var result = new List<Vector3Int>();
        if (LevelGenerator.instance == null) return result;

        Vector3Int playerCell = Vector3Int.zero;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        var enemyCells = new HashSet<Vector3Int>();
        if (TurnManager.instance != null)
            foreach (var e in TurnManager.instance.enemies)
                if (e != null) enemyCells.Add(e.GetCurrentCellPosition());

        HashSet<Vector3Int> hazards = LevelGenerator.instance.hazardCells;
        HashSet<Vector3Int> scaffolds = LevelGenerator.instance.scaffoldCells;
        HashSet<Vector3Int> teleports = LevelGenerator.instance.teleportCells;

        BoundsInt bounds = groundMap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!groundMap.HasTile(cell)) continue;   // Only occupied cells
                if (cell == playerCell) continue;
                if (enemyCells.Contains(cell)) continue;
                if (hazards.Contains(cell)) continue;
                if (scaffolds.Contains(cell)) continue;
                if (teleports.Contains(cell)) continue;
                result.Add(cell);
            }
        }

        return result;
    }

    private Vector3Int PickSmartCell(MagicTileType type, List<Vector3Int> candidates)
    {
        if (candidates.Count == 0) return new Vector3Int(0, -999, 0);

        Vector3Int playerCell = Vector3Int.zero;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        switch (type)
        {
            case MagicTileType.Red:    return PickNearEnemies(candidates, playerCell);
            case MagicTileType.Blue:   return PickNearPlayer(candidates, playerCell);
            case MagicTileType.Green:  return PickMediumDistance(candidates, playerCell);
            case MagicTileType.Yellow: return PickAwayFromEnemies(candidates, playerCell);
            case MagicTileType.Orange: return PickMediumDistance(candidates, playerCell);
            default: return candidates[Random.Range(0, candidates.Count)];
        }
    }

    private Vector3Int PickNearEnemies(List<Vector3Int> cells, Vector3Int player)
    {
        if (TurnManager.instance == null || TurnManager.instance.enemies.Count == 0)
            return cells[Random.Range(0, cells.Count)];

        Vector3Int best = cells[0];
        float bestScore = float.MaxValue;
        foreach (var c in cells)
        {
            float minDist = float.MaxValue;
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e == null) continue;
                float d = HexGridUtils.DistanceCube(c, e.GetCurrentCellPosition());
                if (d < minDist) minDist = d;
            }
            float score = minDist - HexGridUtils.DistanceCube(c, player) * 0.3f;
            if (score < bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    private Vector3Int PickNearPlayer(List<Vector3Int> cells, Vector3Int player)
    {
        Vector3Int best = cells[0];
        float bestDist = float.MaxValue;
        foreach (var c in cells)
        {
            float d = HexGridUtils.DistanceCube(c, player);
            if (d > 0 && d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    private Vector3Int PickMediumDistance(List<Vector3Int> cells, Vector3Int player)
    {
        Vector3Int best = cells[0];
        float bestScore = float.MaxValue;
        foreach (var c in cells)
        {
            float score = Mathf.Abs(HexGridUtils.DistanceCube(c, player) - 2.5f);
            if (score < bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    private Vector3Int PickAwayFromEnemies(List<Vector3Int> cells, Vector3Int player)
    {
        if (TurnManager.instance == null || TurnManager.instance.enemies.Count == 0)
            return cells[Random.Range(0, cells.Count)];

        Vector3Int best = cells[0];
        float bestScore = float.MinValue;
        foreach (var c in cells)
        {
            float minDist = float.MaxValue;
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e == null) continue;
                float d = HexGridUtils.DistanceCube(c, e.GetCurrentCellPosition());
                if (d < minDist) minDist = d;
            }
            if (minDist > bestScore) { bestScore = minDist; best = c; }
        }
        return best;
    }

    // ─────────────────────────────────────────
    //  TILE LOOKUP
    // ─────────────────────────────────────────

    private MagicTileEntry GetEntry(MagicTileType type)
    {
        if (entryMap != null && entryMap.TryGetValue(type, out var entry))
            return entry;

        // Resources fallback — try to load at runtime
        TileBase gt = Resources.Load<TileBase>(type.ToString() + "MagicUpperTile");
        TileBase ct = Resources.Load<TileBase>(type.ToString() + "MagicTile");

        if (gt != null || ct != null)
        {
            var e = new MagicTileEntry { type = type, groundTile = gt, columnTile = ct };
            if (entryMap == null) entryMap = new Dictionary<MagicTileType, MagicTileEntry>();
            entryMap[type] = e;
            return e;
        }

        return null;
    }

    // ─────────────────────────────────────────
    //  ANIMATION (fade-in + glow)
    // ─────────────────────────────────────────

    private IEnumerator FadeInAnimation(List<Vector3Int> cells)
    {
        Tilemap gm = LevelGenerator.instance != null ? LevelGenerator.instance.groundMap : null;
        Tilemap cm = LevelGenerator.instance != null ? LevelGenerator.instance.columnMap : null;
        if (gm == null) yield break;

        // Start tiles invisible
        foreach (var cell in cells)
        {
            gm.SetTileFlags(cell, TileFlags.None);
            gm.SetColor(cell, new Color(1, 1, 1, 0));
            if (cm != null && cm.HasTile(cell))
            {
                cm.SetTileFlags(cell, TileFlags.None);
                cm.SetColor(cell, new Color(1, 1, 1, 0));
            }
        }

        yield return new WaitForSeconds(0.3f);

        // Camera pan to tile center
        CameraController cam = Object.FindFirstObjectByType<CameraController>();
        Vector3 center = Vector3.zero;
        foreach (var c in cells)
            center += gm.CellToWorld(c);
        center /= cells.Count;

        Vector3 camStart = cam != null ? cam.GetTargetPosition() : Vector3.zero;
        Vector3 camTarget = new Vector3(center.x, center.y, camStart.z + 3f);

        float panDur = 0.4f;
        float elapsed = 0f;
        while (cam != null && elapsed < panDur)
        {
            elapsed += Time.deltaTime;
            float ease = Mathf.SmoothStep(0, 1, elapsed / panDur);
            cam.SetTargetPosition(Vector3.Lerp(camStart, camTarget, ease));
            yield return null;
        }

        // Staggered fade-in per tile
        for (int i = 0; i < cells.Count; i++)
        {
            StartCoroutine(FadeSingleTile(cells[i], 0.35f, gm, cm));
            if (i < cells.Count - 1)
                yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.55f);

        // Camera return
        elapsed = 0f;
        while (cam != null && elapsed < panDur)
        {
            elapsed += Time.deltaTime;
            float ease = Mathf.SmoothStep(0, 1, elapsed / panDur);
            cam.SetTargetPosition(Vector3.Lerp(camTarget, camStart, ease));
            yield return null;
        }
        if (cam != null) cam.SetTargetPosition(camStart);
    }

    private IEnumerator FadeSingleTile(Vector3Int cell, float dur, Tilemap gm, Tilemap cm)
    {
        bool hasG = gm != null && gm.HasTile(cell);
        bool hasC = cm != null && cm.HasTile(cell);
        if (!hasG && !hasC) yield break;

        if (AudioManager.instance != null) AudioManager.instance.PlayHit();

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            // Fade in fast, then brief glow that settles to white
            float alpha = Mathf.Clamp01(t * 2.5f);
            float glow = t < 0.4f
                ? Mathf.Lerp(1f, 1.5f, t / 0.4f)
                : Mathf.Lerp(1.5f, 1f, (t - 0.4f) / 0.6f);
            Color col = new Color(glow, glow, glow, alpha);

            if (hasG) gm.SetColor(cell, col);
            if (hasC) cm.SetColor(cell, col);
            yield return null;
        }

        if (hasG) gm.SetColor(cell, Color.white);
        if (hasC) cm.SetColor(cell, Color.white);
    }
}
