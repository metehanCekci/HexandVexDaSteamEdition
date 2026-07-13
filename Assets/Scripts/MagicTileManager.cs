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

    [Header("Animation")]
    [Tooltip("When more than this many enchanted tiles spawn in one sequence, " +
             "skip the camera pan entirely (per-tile VFX/SFX still play). " +
             "Prevents the camera animation from falling behind at high counts.")]
    public int cameraPanMaxTileCount = 3;

    [Header("Spacing")]
    [Tooltip("Minimum hex distance between two spawned magic tiles (any type). " +
             "Prevents clustering when many tiles spawn in the same turn.")]
    public int minTileSeparation = 2;

    // Runtime lookup — built from tileEntries
    private Dictionary<MagicTileType, MagicTileEntry> entryMap;

    // Active magic tiles on the map: cell -> type
    private Dictionary<Vector3Int, MagicTileType> activeTiles = new Dictionary<Vector3Int, MagicTileType>();

    // Saved originals for restore on consume/clear
    private Dictionary<Vector3Int, TileBase> savedGround = new Dictionary<Vector3Int, TileBase>();
    private Dictionary<Vector3Int, TileBase> savedColumn = new Dictionary<Vector3Int, TileBase>();
    // Cells where we hid a foreground decor/scaffold/hazard tile (alpha 0). On restore
    // we only need to flip alpha back — we never replaced these tiles, just dimmed them.
    private HashSet<Vector3Int> hiddenFgA = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> hiddenFgB = new HashSet<Vector3Int>();
    // (tilemap, originalTile) pairs we cleared on each cell. On restore we put them back.
    private Dictionary<Vector3Int, List<(Tilemap map, TileBase tile)>> clearedSceneTiles
        = new Dictionary<Vector3Int, List<(Tilemap, TileBase)>>();

    // Cells where ground sprite is missing — stores target tint color
    private Dictionary<Vector3Int, Color> tintedCells = new Dictionary<Vector3Int, Color>();

    // Foreground overlay tilemap — created at runtime, sorting order 200 (above characters)
    private Tilemap overlayMap;

    // Mid-layer overlay — created at runtime, sorting order 50 (above ground/foreground/warning decor,
    // below characters ~100+). Enchanted groundTile sprites render here so they aren't covered by
    // foreground decor tilemaps that sit at sorting 1–3.
    private Tilemap midOverlayMap;

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
        MagicTileTooltip.EnsureExists();
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
        MatchTilemapAnchor(overlayMap, LevelGenerator.instance.groundMap);
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = 200; // Above character sprites (~100)
        renderer.mode = TilemapRenderer.Mode.Individual;

        return overlayMap;
    }

    private Tilemap GetOrCreateMidOverlayMap()
    {
        if (midOverlayMap != null) return midOverlayMap;
        if (LevelGenerator.instance == null || LevelGenerator.instance.groundMap == null) return null;

        Grid grid = LevelGenerator.instance.groundMap.layoutGrid;
        if (grid == null) return null;

        Transform existing = grid.transform.Find("MagicMidOverlay");
        if (existing != null)
        {
            midOverlayMap = existing.GetComponent<Tilemap>();
            return midOverlayMap;
        }

        GameObject go = new GameObject("MagicMidOverlay");
        go.transform.SetParent(grid.transform, false);
        midOverlayMap = go.AddComponent<Tilemap>();
        MatchTilemapAnchor(midOverlayMap, LevelGenerator.instance.groundMap);
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Individual;

        // Match the scene's column tilemap sorting layer + order so enchanted sprites
        // participate in the same Y-sort pass as regular hex columns. They then nestle
        // between hexes correctly instead of floating in front of the whole grid.
        TilemapRenderer columnRenderer = LevelGenerator.instance.columnMap != null
            ? LevelGenerator.instance.columnMap.GetComponent<TilemapRenderer>()
            : null;
        if (columnRenderer != null)
        {
            renderer.sortingLayerID = columnRenderer.sortingLayerID;
            // Same order as columnMap so Unity's Transparency Sort Axis (Y) decides
            // front/back per-sprite. Using +1 pushes every enchanted sprite in front of
            // every column, which is what caused the "floating in front" look.
            renderer.sortingOrder = columnRenderer.sortingOrder;
        }
        else
        {
            renderer.sortingOrder = 0;
        }

        return midOverlayMap;
    }

    // Make sure the "Highlight" tilemap renders above the columnMap (where enchanted
    // sprites live). Without this, the move-highlight animation is hidden beneath
    // enchanted tiles whose column sprites sit in front of it.
    private static bool highlightOrderAdjusted = false;
    private void EnsureHighlightAboveEnchanted()
    {
        if (highlightOrderAdjusted) return;
        if (LevelGenerator.instance == null || LevelGenerator.instance.columnMap == null) return;

        var hlGo = GameObject.Find("Highlight");
        if (hlGo == null) return;
        var hlRenderer = hlGo.GetComponent<TilemapRenderer>();
        var colRenderer = LevelGenerator.instance.columnMap.GetComponent<TilemapRenderer>();
        if (hlRenderer == null || colRenderer == null) return;

        if (hlRenderer.sortingLayerID != colRenderer.sortingLayerID)
            hlRenderer.sortingLayerID = colRenderer.sortingLayerID;
        if (hlRenderer.sortingOrder <= colRenderer.sortingOrder)
            hlRenderer.sortingOrder = colRenderer.sortingOrder + 1;

        highlightOrderAdjusted = true;
    }

    // Runtime-created Tilemaps default to tileAnchor (0.5, 0.5, 0), but the scene's tilemaps
    // use (0, 0, 0). Mirror the source map's anchor/orientation so overlay sprites align perfectly.
    private static void MatchTilemapAnchor(Tilemap target, Tilemap source)
    {
        if (target == null || source == null) return;
        target.tileAnchor = source.tileAnchor;
        target.orientation = source.orientation;
        target.orientationMatrix = source.orientationMatrix;
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

        // Check which overlays are needed
        bool needsOverlay = false;
        bool needsMidOverlay = false;
        foreach (var type in tiles)
        {
            var entry = GetEntry(type);
            if (entry == null) continue;
            if (entry.foregroundTile != null) needsOverlay = true;
            if (entry.groundTile != null) needsMidOverlay = true;
        }
        if (needsOverlay) GetOrCreateOverlayMap();
        if (needsMidOverlay) GetOrCreateMidOverlayMap();
        EnsureHighlightAboveEnchanted();

        List<PendingSpawn> pending = new List<PendingSpawn>();
        List<Vector3Int> placedCells = new List<Vector3Int>();

        Debug.Log($"[MagicTile] SpawnMagicTiles: {tiles.Count} tile, {candidates.Count} aday hucre, validCells={LevelGenerator.instance.validCells.Count}");

        foreach (var type in tiles)
        {
            if (candidates.Count == 0) break;

            Vector3Int cell = PickSmartCell(type, candidates, placedCells);
            if (cell.y == -999) continue;
            placedCells.Add(cell);

            // Save originals before any replacement
            TileBase origGround = groundMap.GetTile(cell);
            savedGround[cell] = origGround;
            if (columnMap != null)
                savedColumn[cell] = columnMap.HasTile(cell) ? columnMap.GetTile(cell) : null;

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

    // Used by tooltip / hover UI to query what magic type sits on a cell.
    public bool TryGetTileAt(Vector3Int cell, out MagicTileType type)
    {
        return activeTiles.TryGetValue(cell, out type);
    }

    // ─────────────────────────────────────────
    //  CONSUME
    // ─────────────────────────────────────────

    public void ConsumeTile(Vector3Int cell)
    {
        if (!activeTiles.ContainsKey(cell)) return;
        MagicTileType type = activeTiles[cell];
        activeTiles.Remove(cell);
        StartCoroutine(FadeOutAndRestore(cell, type));
    }

    public void ConsumePlayerTile()
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;
        ConsumeTile(TurnManager.instance.player.GetCurrentCellPosition());
    }

    private IEnumerator FadeOutAndRestore(Vector3Int cell, MagicTileType type)
    {
        if (LevelGenerator.instance == null) { RestoreOriginal(cell); yield break; }

        Tilemap gm = LevelGenerator.instance.groundMap;
        Tilemap cm = LevelGenerator.instance.columnMap;
        Tilemap fm = overlayMap;
        Tilemap mm = midOverlayMap;

        bool groundIsTinted = tintedCells.TryGetValue(cell, out Color tintColor);

        bool hasMid = mm != null && mm.HasTile(cell);
        bool hasGround = gm != null && gm.HasTile(cell);
        bool hasColumn = cm != null && cm.HasTile(cell) && savedColumn.ContainsKey(cell);
        bool hasFg = fm != null && fm.HasTile(cell);

        if (hasMid)    mm.SetTileFlags(cell, TileFlags.None);
        if (hasGround) gm.SetTileFlags(cell, TileFlags.None);
        if (hasColumn) cm.SetTileFlags(cell, TileFlags.None);
        if (hasFg)     fm.SetTileFlags(cell, TileFlags.None);

        SpawnDissolveVFX(cell, type, gm);

        float dur = 0.4f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            // Flash peak at ~25%, then fade out with a soft glow tail
            float flash = t < 0.25f
                ? Mathf.Lerp(1f, 1.8f, t / 0.25f)
                : Mathf.Lerp(1.8f, 1f, (t - 0.25f) / 0.75f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            Color fadeCol = new Color(flash, flash, flash, alpha);

            if (hasMid)
            {
                // Mid-overlay fades out; underlying groundMap tile was never replaced.
                mm.SetColor(cell, fadeCol);
            }
            else if (hasGround)
            {
                if (groundIsTinted)
                {
                    Color blended = Color.Lerp(tintColor, Color.white, t);
                    blended.r *= flash;
                    blended.g *= flash;
                    blended.b *= flash;
                    blended.a = Mathf.Lerp(tintColor.a, 1f, t);
                    gm.SetColor(cell, blended);
                }
                else
                {
                    gm.SetColor(cell, fadeCol);
                }
            }

            if (hasColumn) cm.SetColor(cell, fadeCol);
            if (hasFg)     fm.SetColor(cell, fadeCol);

            yield return null;
        }

        RestoreOriginal(cell);
    }

    private void SpawnDissolveVFX(Vector3Int cell, MagicTileType type, Tilemap gm)
    {
        if (gm == null) return;

        Vector3 worldCenter = gm.GetCellCenterWorld(cell);
        Color tint = GetMagicTint(type);
        tint.a = 1f;

        GameObject vfx = new GameObject("MagicTileDissolveVFX");
        vfx.transform.position = worldCenter;

        ParticleSystem ps = vfx.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.55f;
        main.startSpeed = 1.4f;
        main.startSize = 0.18f;
        main.startColor = tint;
        main.gravityModifier = -0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 22)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 0.8f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(tint, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.3f, 1.2f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);

        ParticleSystemRenderer psr = vfx.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingOrder = 210;
            Shader particleShader = Shader.Find("Sprites/Default");
            if (particleShader != null)
                psr.material = new Material(particleShader);
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayHit();

        Destroy(vfx, 1.2f);
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
        hiddenFgA.Clear();
        hiddenFgB.Clear();
        clearedSceneTiles.Clear();

        // Clear the overlay tilemaps completely
        if (overlayMap != null)
            overlayMap.ClearAllTiles();
        if (midOverlayMap != null)
            midOverlayMap.ClearAllTiles();

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

        // Put back every scene tilemap tile we cleared at spawn.
        if (clearedSceneTiles.TryGetValue(cell, out var cleared))
        {
            foreach (var (tm, tile) in cleared)
            {
                if (tm == null) continue;
                tm.SetTile(cell, tile);
                tm.SetTileFlags(cell, TileFlags.None);
                tm.SetColor(cell, Color.white);
            }
            clearedSceneTiles.Remove(cell);
        }
        hiddenFgA.Remove(cell);
        hiddenFgB.Remove(cell);

        // Remove foreground overlay tile
        if (overlayMap != null && overlayMap.HasTile(cell))
            overlayMap.SetTile(cell, null);

        // Remove mid-overlay tile (enchanted ground sprite layer)
        if (midOverlayMap != null && midOverlayMap.HasTile(cell))
            midOverlayMap.SetTile(cell, null);

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

    private Vector3Int PickSmartCell(MagicTileType type, List<Vector3Int> candidates, List<Vector3Int> placedCells)
    {
        if (candidates.Count == 0) return new Vector3Int(0, -999, 0);

        Vector3Int playerCell = Vector3Int.zero;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            playerCell = TurnManager.instance.player.GetCurrentCellPosition();

        // Spread tiles apart: filter out cells too close to already-placed magic tiles.
        // Relax separation progressively if nothing qualifies, so we never fail to place.
        List<Vector3Int> pool = candidates;
        if (placedCells != null && placedCells.Count > 0 && minTileSeparation > 0)
        {
            for (int minSep = minTileSeparation; minSep >= 1; minSep--)
            {
                var filtered = new List<Vector3Int>(candidates.Count);
                foreach (var c in candidates)
                {
                    bool tooClose = false;
                    foreach (var p in placedCells)
                    {
                        if (HexGridUtils.DistanceCube(c, p) < minSep) { tooClose = true; break; }
                    }
                    if (!tooClose) filtered.Add(c);
                }
                if (filtered.Count > 0) { pool = filtered; break; }
            }
        }

        switch (type)
        {
            case MagicTileType.Red:    return PickNearEnemies(pool, playerCell);
            case MagicTileType.Blue:   return PickNearPlayer(pool, playerCell);
            case MagicTileType.Green:  return PickMediumDistance(pool, playerCell);
            case MagicTileType.Yellow: return PickAwayFromEnemies(pool, playerCell);
            case MagicTileType.Orange: return PickMediumDistance(pool, playerCell);
            default: return pool[Random.Range(0, pool.Count)];
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

        // ── Decide BEFORE animating whether to pan the camera at all ──
        // If we're about to animate more than the threshold, skip camera entirely
        // for the whole sequence (per-tile VFX/SFX inside FadeSingleTile are unaffected).
        bool useCameraPan = pending.Count <= cameraPanMaxTileCount;

        // Brief pause — original tiles are still fully visible
        // Only relevant when we're about to pan the camera; otherwise fire VFX immediately.
        if (useCameraPan)
            yield return new WaitForSeconds(0.3f);

        // Camera pan to tile center (skipped entirely when count > threshold)
        CameraController cam = useCameraPan ? Object.FindFirstObjectByType<CameraController>() : null;
        Vector3 camStart = cam != null ? cam.GetTargetPosition() : Vector3.zero;
        Vector3 camTarget = camStart;
        float panDur = 0.4f;

        if (cam != null)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in pending)
                center += gm.CellToWorld(p.cell);
            center /= pending.Count;

            camTarget = new Vector3(center.x, center.y, camStart.z + 3f);

            float elapsed = 0f;
            while (elapsed < panDur)
            {
                elapsed += Time.deltaTime;
                float ease = Mathf.SmoothStep(0, 1, elapsed / panDur);
                cam.SetTargetPosition(Vector3.Lerp(camStart, camTarget, ease));
                yield return null;
            }
        }

        // Staggered tile replacement + fade-in (original tiles visible until each one transforms)
        // These per-tile effects always run, regardless of camera pan.
        Tilemap mm = midOverlayMap;
        for (int i = 0; i < pending.Count; i++)
        {
            StartCoroutine(FadeSingleTile(pending[i], gm, cm, fm, mm));
            if (i < pending.Count - 1)
                yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.55f);

        // Camera return (only if we panned out in the first place)
        if (cam != null)
        {
            float elapsed = 0f;
            while (elapsed < panDur)
            {
                elapsed += Time.deltaTime;
                float ease = Mathf.SmoothStep(0, 1, elapsed / panDur);
                cam.SetTargetPosition(Vector3.Lerp(camTarget, camStart, ease));
                yield return null;
            }
            cam.SetTargetPosition(camStart);
        }
    }

    private IEnumerator FadeSingleTile(PendingSpawn spawn, Tilemap gm, Tilemap cm, Tilemap fm, Tilemap mm)
    {
        Vector3Int cell = spawn.cell;
        MagicTileEntry entry = spawn.entry;
        MagicTileType type = spawn.type;

        Color tintColor = Color.white;

        // ── Paint enchanted sprite directly onto the scene's columnMap ──
        // The scene's columnMap already sorts correctly with neighbours (it's how regular
        // hexes stack). Writing into it means the enchanted tile inherits that sort — no
        // overlay in front of / behind everything. The original column tile is replaced;
        // we'll restore it from savedColumn on consume.
        bool cellHasColumn = cm != null && savedColumn.ContainsKey(cell);
        TileBase chosenSprite = cellHasColumn && entry != null && entry.columnTile != null
            ? entry.columnTile
            : (entry != null ? entry.groundTile : null);

        bool paintedOnColumn = chosenSprite != null && cm != null;
        bool groundIsTinted = !paintedOnColumn;

        bool hasMidGround = false;
        bool hasColumn = false;

        if (paintedOnColumn)
        {
            cm.SetTile(cell, chosenSprite);
            cm.SetTileFlags(cell, TileFlags.None);
            cm.SetColor(cell, new Color(1, 1, 1, 0));
            hasColumn = true;

            // Hide the default groundMap tile visually so the enchanted sprite fully
            // represents the cell. Tile stays in place for pathfinding/HasTile.
            if (gm != null && gm.HasTile(cell))
            {
                gm.SetTileFlags(cell, TileFlags.None);
                gm.SetColor(cell, new Color(1, 1, 1, 0));
            }

            // Clear EVERY other scene tilemap's tile on this cell — the "default upper"
            // decor tile must fully disappear. Alpha-dimming is unreliable with some Tile
            // asset types (animated/scriptable) so we just remove the tile and remember
            // it so we can put it back on consume.
            // groundMap and columnMap are handled above (savedGround/savedColumn restore them).
            Grid grid = gm != null ? gm.layoutGrid : null;
            if (grid != null)
            {
                var cleared = new List<(Tilemap, TileBase)>();
                foreach (var tm in grid.GetComponentsInChildren<Tilemap>(true))
                {
                    if (tm == null) continue;
                    if (tm == gm || tm == cm) continue;
                    if (tm == midOverlayMap || tm == overlayMap) continue;
                    if (!tm.HasTile(cell)) continue;
                    TileBase orig = tm.GetTile(cell);
                    tm.SetTile(cell, null);
                    cleared.Add((tm, orig));
                }
                if (cleared.Count > 0)
                    clearedSceneTiles[cell] = cleared;
            }
        }
        else
        {
            tintColor = GetMagicTint(type);
            tintedCells[cell] = tintColor;
            if (gm != null) gm.SetTileFlags(cell, TileFlags.None);
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

            Color fadeCol = new Color(glow, glow, glow, alpha);
            if (hasMidGround)
            {
                // Fade in the mid-overlay sprite; leave the underlying groundMap tile untouched.
                mm.SetColor(cell, fadeCol);
            }
            else if (hasColumn)
            {
                // Enchanted sprite lives on columnMap. Fade it in. Do NOT touch groundMap —
                // its tile is still present and must stay alpha=0 so the default upper doesn't
                // re-appear under the enchanted sprite.
                cm.SetColor(cell, fadeCol);
            }
            else if (groundIsTinted)
            {
                // Blend from white to tint color with glow effect
                Color blended = Color.Lerp(Color.white, tintColor, t);
                blended.r *= glow;
                blended.g *= glow;
                blended.b *= glow;
                gm.SetColor(cell, blended);
            }

            if (hasFg) fm.SetColor(cell, fadeCol);

            yield return null;
        }

        // ── Final settled color ──

        if (hasMidGround)
            mm.SetColor(cell, Color.white);
        else if (hasColumn)
            cm.SetColor(cell, Color.white);
        else if (groundIsTinted)
            gm.SetColor(cell, tintColor);

        if (hasFg) fm.SetColor(cell, Color.white);
    }
}
