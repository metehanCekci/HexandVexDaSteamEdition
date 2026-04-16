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
        [Tooltip("Ground layer — arka kisim, karakter bunun onunde (groundMap, sorting 0)")]
        public TileBase groundTile;
        [Tooltip("Column layer — hex sutunu (columnMap, sorting -1)")]
        public TileBase columnTile;
        [Tooltip("On katman — karakter bunun arkasinda kalir (MagicOverlay, sorting 200)")]
        public TileBase foregroundTile;
    }

    [Header("Magic Tile Data")]
    public MagicTileEntry[] tileEntries = new MagicTileEntry[0];

    // Runtime lookup — built from tileEntries
    private Dictionary<MagicTileType, MagicTileEntry> entryMap;

    // Active magic tiles on the map: cell -> type
    private Dictionary<Vector3Int, MagicTileType> activeTiles = new Dictionary<Vector3Int, MagicTileType>();

    // Saved originals for restore on consume/clear
    private Dictionary<Vector3Int, TileBase> savedGround = new Dictionary<Vector3Int, TileBase>();
    private Dictionary<Vector3Int, TileBase> savedColumn = new Dictionary<Vector3Int, TileBase>();

    // Cells where ground sprite is missing — stores target tint color
    private Dictionary<Vector3Int, Color> tintedCells = new Dictionary<Vector3Int, Color>();

    // Foreground overlay tilemap — created at runtime, sorting order 200 (above characters)
    private Tilemap overlayMap;

    // Blue tile: consume after 2-hex move away
    private Vector3Int blueTileCell = new Vector3Int(0, -999, 0);
    // Orange tile: consume when player leaves the cell
    private Vector3Int orangeTileCell = new Vector3Int(0, -999, 0);

    // Deferred spawn data — tiles are replaced during animation, not immediately
    private struct PendingSpawn
    {
        public Vector3Int cell;
        public MagicTileType type;
        public MagicTileEntry entry;
    }

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
    //  OVERLAY TILEMAP (auto-created)
    // ─────────────────────────────────────────

    private Tilemap GetOrCreateOverlayMap()
    {
        if (overlayMap != null) return overlayMap;
        if (LevelGenerator.instance == null || LevelGenerator.instance.groundMap == null) return null;

        Grid grid = LevelGenerator.instance.groundMap.layoutGrid;
        if (grid == null) return null;

        // Check if already exists in the scene
        Transform existing = grid.transform.Find("MagicOverlay");
        if (existing != null)
        {
            overlayMap = existing.GetComponent<Tilemap>();
            return overlayMap;
        }

        // Create a new tilemap child under the Grid
        GameObject go = new GameObject("MagicOverlay");
        go.transform.SetParent(grid.transform, false);
        overlayMap = go.AddComponent<Tilemap>();
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 200; // Above character sprites (~100)
        renderer.mode = TilemapRenderer.Mode.Individual;

        return overlayMap;
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

        // Check if any entry has a foreground tile — only create overlay if needed
        bool needsOverlay = false;
        foreach (var type in tiles)
        {
            var entry = GetEntry(type);
            if (entry != null && entry.foregroundTile != null)
            {
                needsOverlay = true;
                break;
            }
        }
        if (needsOverlay) GetOrCreateOverlayMap();

        List<PendingSpawn> pending = new List<PendingSpawn>();

        Debug.Log($"[MagicTile] SpawnMagicTiles: {tiles.Count} tile, {candidates.Count} aday hucre, validCells={LevelGenerator.instance.validCells.Count}");

        foreach (var type in tiles)
        {
            if (candidates.Count == 0) break;

            Vector3Int cell = PickSmartCell(type, candidates);
            if (cell.y == -999) continue;

            // Save originals before any replacement
            TileBase origGround = groundMap.GetTile(cell);
            savedGround[cell] = origGround;
            if (columnMap != null && columnMap.HasTile(cell))
                savedColumn[cell] = columnMap.GetTile(cell);

            MagicTileEntry entry = GetEntry(type);

            Debug.Log($"[MagicTile] {type} -> cell {cell} | origGround={origGround?.name ?? "NULL"} | entry ground={entry?.groundTile?.name ?? "NULL"} column={entry?.columnTile?.name ?? "NULL"} fg={entry?.foregroundTile?.name ?? "NULL"}");

            // Register as active — but DON'T replace tiles yet (deferred to animation)
            activeTiles[cell] = type;
            pending.Add(new PendingSpawn { cell = cell, type = type, entry = entry });
            candidates.Remove(cell);
        }

        if (pending.Count > 0)
            StartCoroutine(FadeInAnimation(pending));
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
        tintedCells.Clear();

        // Clear the overlay tilemap completely
        if (overlayMap != null)
            overlayMap.ClearAllTiles();

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

        // Remove foreground overlay tile
        if (overlayMap != null && overlayMap.HasTile(cell))
            overlayMap.SetTile(cell, null);

        tintedCells.Remove(cell);
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

        Tilemap gm = LevelGenerator.instance.groundMap;

        // Use validCells AND verify the cell actually has a ground tile
        foreach (var cell in LevelGenerator.instance.validCells)
        {
            if (!gm.HasTile(cell)) continue;              // Double-check: must have ground tile
            if (cell == playerCell) continue;
            if (enemyCells.Contains(cell)) continue;
            if (hazards.Contains(cell)) continue;
            if (scaffolds.Contains(cell)) continue;
            if (teleports.Contains(cell)) continue;
            result.Add(cell);
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
        TileBase fg = Resources.Load<TileBase>("Katman" + type.ToString() + "Tile");

        if (gt != null || ct != null || fg != null)
        {
            var e = new MagicTileEntry { type = type, groundTile = gt, columnTile = ct, foregroundTile = fg };
            if (entryMap == null) entryMap = new Dictionary<MagicTileType, MagicTileEntry>();
            entryMap[type] = e;
            return e;
        }

        return null;
    }

    // ─────────────────────────────────────────
    //  COLOR TINT (fallback when ground sprite is missing)
    // ─────────────────────────────────────────

    private Color GetMagicTint(MagicTileType type)
    {
        switch (type)
        {
            case MagicTileType.Red:    return new Color(1.0f, 0.55f, 0.55f);
            case MagicTileType.Blue:   return new Color(0.55f, 0.65f, 1.0f);
            case MagicTileType.Green:  return new Color(0.55f, 1.0f, 0.6f);
            case MagicTileType.Yellow: return new Color(1.0f, 0.95f, 0.5f);
            case MagicTileType.Orange: return new Color(1.0f, 0.7f, 0.4f);
            default:                   return Color.white;
        }
    }

    // ─────────────────────────────────────────
    //  ANIMATION (fade-in + glow)
    //  Tile replacement is DEFERRED to here so
    //  original tiles stay visible during camera pan.
    // ─────────────────────────────────────────

    private IEnumerator FadeInAnimation(List<PendingSpawn> pending)
    {
        Tilemap gm = LevelGenerator.instance != null ? LevelGenerator.instance.groundMap : null;
        Tilemap cm = LevelGenerator.instance != null ? LevelGenerator.instance.columnMap : null;
        Tilemap fm = overlayMap;
        if (gm == null) yield break;

        // Brief pause — original tiles are still fully visible
        yield return new WaitForSeconds(0.3f);

        // Camera pan to tile center
        CameraController cam = Object.FindFirstObjectByType<CameraController>();
        Vector3 center = Vector3.zero;
        foreach (var p in pending)
            center += gm.CellToWorld(p.cell);
        center /= pending.Count;

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

        // Staggered tile replacement + fade-in (original tiles visible until each one transforms)
        for (int i = 0; i < pending.Count; i++)
        {
            StartCoroutine(FadeSingleTile(pending[i], gm, cm, fm));
            if (i < pending.Count - 1)
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

    private IEnumerator FadeSingleTile(PendingSpawn spawn, Tilemap gm, Tilemap cm, Tilemap fm)
    {
        Vector3Int cell = spawn.cell;
        MagicTileEntry entry = spawn.entry;
        MagicTileType type = spawn.type;

        bool groundIsTinted = (entry == null || entry.groundTile == null);
        Color tintColor = Color.white;

        // ── Apply tile replacement right now (original was visible until this moment) ──

        if (!groundIsTinted)
        {
            // Replace ground with magic sprite — start invisible, will fade in
            gm.SetTile(cell, entry.groundTile);
            gm.SetTileFlags(cell, TileFlags.None);
            gm.SetColor(cell, new Color(1, 1, 1, 0));
        }
        else
        {
            // No magic ground sprite — keep original tile, blend to tint color
            tintColor = GetMagicTint(type);
            tintedCells[cell] = tintColor;
            gm.SetTileFlags(cell, TileFlags.None);
            // Ground stays visible at white — will blend to tint during animation
        }

        // Replace column tile
        bool hasColumn = false;
        if (entry != null && entry.columnTile != null && cm != null && savedColumn.ContainsKey(cell))
        {
            cm.SetTile(cell, entry.columnTile);
            cm.SetTileFlags(cell, TileFlags.None);
            cm.SetColor(cell, new Color(1, 1, 1, 0));
            hasColumn = true;
        }

        // Place foreground overlay tile
        bool hasFg = false;
        if (entry != null && entry.foregroundTile != null && fm != null)
        {
            fm.SetTile(cell, entry.foregroundTile);
            fm.SetTileFlags(cell, TileFlags.None);
            fm.SetColor(cell, new Color(1, 1, 1, 0));
            hasFg = true;
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayHit();

        // ── Fade in with glow ──

        float dur = 0.35f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            float alpha = Mathf.Clamp01(t * 2.5f);
            float glow = t < 0.4f
                ? Mathf.Lerp(1f, 1.5f, t / 0.4f)
                : Mathf.Lerp(1.5f, 1f, (t - 0.4f) / 0.6f);

            if (!groundIsTinted)
            {
                // Normal fade-in for magic ground sprite
                gm.SetColor(cell, new Color(glow, glow, glow, alpha));
            }
            else
            {
                // Blend from white to tint color with glow effect
                Color blended = Color.Lerp(Color.white, tintColor, t);
                blended.r *= glow;
                blended.g *= glow;
                blended.b *= glow;
                gm.SetColor(cell, blended);
            }

            Color fadeCol = new Color(glow, glow, glow, alpha);
            if (hasColumn) cm.SetColor(cell, fadeCol);
            if (hasFg) fm.SetColor(cell, fadeCol);

            yield return null;
        }

        // ── Final settled color ──

        if (!groundIsTinted)
            gm.SetColor(cell, Color.white);
        else
            gm.SetColor(cell, tintColor);

        if (hasColumn) cm.SetColor(cell, Color.white);
        if (hasFg) fm.SetColor(cell, Color.white);
    }
}
