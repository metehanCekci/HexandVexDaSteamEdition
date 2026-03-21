using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class SlipperySecretionPerk : BasePerk
{
    [HideInInspector] public HashSet<Vector3Int> mucusCells = new HashSet<Vector3Int>();
    private List<Vector3Int> trailHistory = new List<Vector3Int>();
    private Vector3Int lastTrackedCell;
    private bool initialized = false;

    void OnEnable()
    {
        maxLevel = 3;
    }

    public override void OnAcquire()
    {
        InitTracking();
    }

    public override void OnEquip()
    {
        InitTracking();
    }

    public override void OnLevelStart()
    {
        mucusCells.Clear();
        trailHistory.Clear();
        initialized = false;
        InitTracking();
    }

    private void InitTracking()
    {
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            lastTrackedCell = TurnManager.instance.player.GetCurrentCellPosition();
            initialized = true;
        }
    }

    void Update()
    {
        if (!initialized || TurnManager.instance == null || TurnManager.instance.player == null) return;

        Vector3Int currentCell = TurnManager.instance.player.GetCurrentCellPosition();
        if (currentCell != lastTrackedCell)
        {
            // Player moved — add old cell to trail
            AddToTrail(lastTrackedCell);
            lastTrackedCell = currentCell;
        }
    }

    private void AddToTrail(Vector3Int cell)
    {
        int maxTrail = currentLevel; // Lv1=1, Lv2=2, Lv3=3

        trailHistory.Add(cell);

        // Remove old cells beyond trail length
        while (trailHistory.Count > maxTrail)
        {
            Vector3Int oldCell = trailHistory[0];
            trailHistory.RemoveAt(0);
            mucusCells.Remove(oldCell);
            ClearMucusVisual(oldCell);
        }

        mucusCells.Add(cell);
        ShowMucusVisual(cell);
    }

    /// <summary>
    /// Checks if a cell has mucus and returns the slide target cell.
    /// Called by EnemyMovement after entering a cell.
    /// </summary>
    public Vector3Int GetSlideTarget(Vector3Int enteredCell, Vector3Int cameFromCell)
    {
        if (!mucusCells.Contains(enteredCell)) return enteredCell;

        // Calculate direction of movement
        Vector3Int[] offsets = (cameFromCell.y % 2 != 0)
            ? EnemyMovement.evenOffsets
            : EnemyMovement.oddOffsets;

        // Find which direction index goes from cameFrom to entered
        int dirIndex = -1;
        for (int i = 0; i < 6; i++)
        {
            if (cameFromCell + offsets[i] == enteredCell)
            {
                dirIndex = i;
                break;
            }
        }

        if (dirIndex < 0) return enteredCell;

        // Continue in the same direction for 1 extra hex
        Vector3Int[] slideOffsets = (enteredCell.y % 2 != 0)
            ? EnemyMovement.evenOffsets
            : EnemyMovement.oddOffsets;
        Vector3Int slideTarget = enteredCell + slideOffsets[dirIndex];

        // Check if slide target is valid
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            Tilemap groundMap = TurnManager.instance.player.groundMap;
            bool isScaffold = ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(slideTarget);
            bool isWalkable = groundMap.HasTile(slideTarget) || isScaffold;
            bool hasEnemy = TurnManager.instance.IsEnemyAtCell(slideTarget);
            bool hasPlayer = TurnManager.instance.player.GetCurrentCellPosition() == slideTarget;

            if (isWalkable && !hasEnemy && !hasPlayer)
                return slideTarget;
        }

        return enteredCell; // Can't slide, stay put
    }

    public bool IsMucusCell(Vector3Int cell)
    {
        return mucusCells.Contains(cell);
    }

    // ─── Visuals ───

    private void ShowMucusVisual(Vector3Int cell)
    {
        if (TurnManager.instance == null || TurnManager.instance.player == null) return;
        Tilemap warningMap = null;

        // Try to get warningMap from any enemy for tile overlay
        if (TurnManager.instance.enemies.Count > 0)
        {
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e != null && e.warningMap != null) { warningMap = e.warningMap; break; }
            }
        }

        if (warningMap != null)
        {
            warningMap.SetTile(cell, warningMap.GetTile(warningMap.origin)); // reuse existing tile
            warningMap.SetColor(cell, new Color(0.3f, 0.9f, 0.3f, 0.35f)); // Green-ish mucus
        }
    }

    private void ClearMucusVisual(Vector3Int cell)
    {
        if (TurnManager.instance == null) return;
        Tilemap warningMap = null;
        if (TurnManager.instance.enemies.Count > 0)
        {
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e != null && e.warningMap != null) { warningMap = e.warningMap; break; }
            }
        }

        if (warningMap != null && warningMap.HasTile(cell))
        {
            // Only clear if not targeted by a bruiser
            bool isWarning = false;
            foreach (var e in TurnManager.instance.enemies)
            {
                if (e == null) continue;
                var bruiser = e.GetComponent<BruiserEnemyAI>();
                if (bruiser != null && bruiser.warningCells.Contains(cell)) { isWarning = true; break; }
            }
            if (!isWarning) warningMap.SetTile(cell, null);
        }
    }
}
