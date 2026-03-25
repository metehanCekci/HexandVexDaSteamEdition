using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class PhantomAssaultPerk : BasePerk
{
    private List<Vector3Int> ghostCells = new List<Vector3Int>();
    private List<GameObject> ghostVisuals = new List<GameObject>();

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
    }

    public override void OnLevelStart()
    {
        ClearGhosts();
    }

    public override void OnLevelClear()
    {
        ClearGhosts();
    }

    void OnDestroy()
    {
        ClearGhosts();
    }

    /// <summary>
    /// Called by TurnManager after MultiAttack completes.
    /// Spawns ghost at player's current cell and teleports player to a random safe tile.
    /// </summary>
    public IEnumerator SpawnGhostAndTeleport()
    {
        var tm = TurnManager.instance;
        if (tm == null || tm.player == null) yield break;

        Vector3Int attackCell = tm.player.GetCurrentCellPosition();

        // Don't stack ghosts on the same cell
        if (!ghostCells.Contains(attackCell))
        {
            ghostCells.Add(attackCell);
            SpawnGhostVisual(attackCell);
        }

        // Find random safe tile to teleport to
        Vector3Int teleportTarget = FindRandomSafeTile(attackCell);
        if (teleportTarget == attackCell) yield break; // nowhere to go

        // Fade out
        SpriteRenderer sr = tm.player.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            float dur = 0.12f, elapsed = 0f;
            Color startColor = sr.color;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, elapsed / dur));
                yield return null;
            }
        }

        // Teleport
        if (ScaffoldManager.instance != null)
            ScaffoldManager.instance.OnEntityLeave(attackCell);
        tm.player.ForceSetPosition(teleportTarget);
        if (ScaffoldManager.instance != null)
            ScaffoldManager.instance.OnEntityEnter(teleportTarget);

        // Fade in
        if (sr != null)
        {
            float dur = 0.15f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                tm.player.transform.localScale = Vector3.one * (1f + (1f - t) * 0.2f);
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            tm.player.transform.localScale = Vector3.one;
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();
        tm.player.UpdateHighlights();
        TriggerVisualPop();
    }

    /// <summary>
    /// Returns all ghost cell positions that have adjacent enemies.
    /// Called by TurnManager during HandleSkipPhase to let ghosts attack.
    /// </summary>
    public List<Vector3Int> GetGhostCellsWithTargets()
    {
        var tm = TurnManager.instance;
        if (tm == null) return new List<Vector3Int>();

        List<Vector3Int> result = new List<Vector3Int>();
        Vector3Int playerCell = tm.player != null ? tm.player.GetCurrentCellPosition() : new Vector3Int(-999, -999, 0);

        foreach (var cell in ghostCells)
        {
            // Skip ghost at player's current position (player already attacks there)
            if (cell == playerCell) continue;

            var adj = tm.GetAdjacentEnemies(cell);
            if (adj.Count > 0) result.Add(cell);
        }
        return result;
    }

    /// <summary>
    /// Clear all ghosts after skip attack resolves.
    /// </summary>
    public void ConsumeGhosts()
    {
        ClearGhosts();
    }

    public bool HasGhosts() => ghostCells.Count > 0;

    private Vector3Int FindRandomSafeTile(Vector3Int excludeCell)
    {
        var tm = TurnManager.instance;
        if (tm == null) return excludeCell;

        var lg = LevelGenerator.instance;
        int radius = lg != null ? lg.baseMapRadius + (RunManager.instance.currentLevel / 6) + 2 : 10;
        Tilemap groundMap = tm.player.groundMap;

        List<Vector3Int> candidates = new List<Vector3Int>();
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int c = new Vector3Int(x, y, 0);
                if (c == excludeCell) continue;
                bool walkable = groundMap.HasTile(c) || (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(c));
                if (!walkable) continue;
                if (tm.IsEnemyAtCell(c)) continue;
                if (lg != null && lg.hazardCells != null && lg.hazardCells.Contains(c)) continue;
                // Don't teleport onto ghost cells
                if (ghostCells.Contains(c)) continue;
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0) return excludeCell;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private void SpawnGhostVisual(Vector3Int cell)
    {
        var tm = TurnManager.instance;
        if (tm == null || tm.player == null) return;

        Tilemap groundMap = tm.player.groundMap;
        Vector3 worldPos = groundMap.GetCellCenterWorld(cell);
        worldPos.z = -0.05f;

        GameObject ghost = new GameObject("PhantomGhost");
        ghost.transform.position = worldPos;

        SpriteRenderer ghostSR = ghost.AddComponent<SpriteRenderer>();
        SpriteRenderer playerSR = tm.player.GetComponentInChildren<SpriteRenderer>();
        if (playerSR != null)
        {
            ghostSR.sprite = playerSR.sprite;
            ghostSR.flipX = playerSR.flipX;
        }
        ghostSR.color = new Color(0.5f, 0.8f, 1f, 0.45f); // translucent blue ghost
        ghostSR.sortingOrder = 4;
        ghost.transform.localScale = Vector3.one * 0.9f;

        ghostVisuals.Add(ghost);
    }

    private void ClearGhosts()
    {
        foreach (var g in ghostVisuals)
            if (g != null) Object.Destroy(g);
        ghostVisuals.Clear();
        ghostCells.Clear();
    }
}
