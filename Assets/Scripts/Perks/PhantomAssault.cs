using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class PhantomAssaultPerk : BasePerk
{
    private List<Vector3Int> ghostCells = new List<Vector3Int>();
    private List<GameObject> ghostVisuals = new List<GameObject>();

    private bool subscribed = false;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
    }

    public override void OnAcquire()
    {
        SubscribeScaffold();
        description = GetDescription();
    }

    public override void OnEquip()
    {
        SubscribeScaffold();
    }

    public override void OnUnequip()
    {
        UnsubscribeScaffold();
        ClearGhosts();
    }

    public override void OnLevelStart()
    {
        ClearGhosts();
        SubscribeScaffold();
        description = GetDescription();
    }

    public override void OnLevelClear()
    {
        ClearGhosts();
    }

    void OnDestroy()
    {
        UnsubscribeScaffold();
        ClearGhosts();
    }

    private void SubscribeScaffold()
    {
        if (subscribed) return;
        TrapTileEvents.OnTileDestroyed += OnScaffoldTileDestroyed;
        subscribed = true;
    }

    private void UnsubscribeScaffold()
    {
        if (!subscribed) return;
        TrapTileEvents.OnTileDestroyed -= OnScaffoldTileDestroyed;
        subscribed = false;
    }

    private void OnScaffoldTileDestroyed(Vector3Int cell)
    {
        // Scaffold düştüğünde o cell'deki ghost'u sil
        if (ghostCells.Contains(cell))
            DestroyGhostAtCell(cell);
    }

    /// <summary>
    /// Called by TurnManager after knockback resolves.
    /// Places a ghost at the cell the enemy vacated (its position before knockback).
    /// </summary>
    public void SpawnGhostAtCell(Vector3Int cell)
    {
        // Don't stack ghosts on the same cell
        if (ghostCells.Contains(cell)) return;

        ghostCells.Add(cell);
        SpawnGhostVisual(cell);
        TriggerVisualPop();
        description = GetDescription();
    }

    /// <summary>
    /// Returns all ghost cell positions (ordered: oldest first, newest last).
    /// Called by TurnManager during HandleSkipPhase.
    /// </summary>
    public List<Vector3Int> GetAllGhostCells()
    {
        return new List<Vector3Int>(ghostCells);
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

        foreach (var cell in ghostCells)
        {
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
        description = GetDescription();
    }

    public bool HasGhosts() => ghostCells.Count > 0;

    /// <summary>
    /// Called by TurnManager when an enemy walks into a ghost cell.
    /// Removes that ghost with a fade-out effect.
    /// </summary>
    public void DestroyGhostAtCell(Vector3Int cell)
    {
        int idx = ghostCells.IndexOf(cell);
        if (idx < 0) return;

        ghostCells.RemoveAt(idx);
        if (idx < ghostVisuals.Count)
        {
            GameObject ghost = ghostVisuals[idx];
            ghostVisuals.RemoveAt(idx);
            if (ghost != null)
                StartCoroutine(FadeOutAndDestroy(ghost));
        }
        description = GetDescription();
    }

    /// <summary>
    /// Check if the given cell has a ghost.
    /// </summary>
    public bool HasGhostAtCell(Vector3Int cell)
    {
        return ghostCells.Contains(cell);
    }

    /// <summary>
    /// Teleport the player to a ghost cell with fade-out/in animation.
    /// </summary>
    public IEnumerator TeleportPlayerToCell(Vector3Int targetCell)
    {
        var tm = TurnManager.instance;
        if (tm == null || tm.player == null) yield break;

        Vector3Int currentCell = tm.player.GetCurrentCellPosition();
        if (targetCell == currentCell) yield break;

        SpriteRenderer sr = tm.player.GetComponentInChildren<SpriteRenderer>();

        // Fade out
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
            ScaffoldManager.instance.OnEntityLeave(currentCell);
        tm.player.ForceSetPosition(targetCell);
        if (ScaffoldManager.instance != null)
            ScaffoldManager.instance.OnEntityEnter(targetCell);

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
    }

    private IEnumerator FadeOutAndDestroy(GameObject ghost)
    {
        if (ghost == null) yield break;
        SpriteRenderer sr = ghost.GetComponent<SpriteRenderer>();
        if (sr == null) { Object.Destroy(ghost); yield break; }

        float dur = 0.4f, elapsed = 0f;
        Color startColor = sr.color;
        Vector3 startScale = ghost.transform.localScale;

        while (elapsed < dur)
        {
            if (ghost == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
            ghost.transform.localScale = startScale * (1f + t * 0.3f); // slight expand as it fades
            yield return null;
        }

        if (ghost != null) Object.Destroy(ghost);
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

    private string GetDescription()
    {
        int count = ghostCells.Count;
        return $"Knockback leaves a ghost where the enemy stood. Skip to teleport through all ghosts, attacking at each.\nGhosts: {count}";
    }

}
