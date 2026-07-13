using UnityEngine;

/// <summary>
/// Walker/Melee düşman AI'ı. Oyuncuya doğru yürür, yanına gelince saldırır.
/// Movement: Follow (A* pathfinding).
/// Saldırı kararı TurnManager'da verilir (IsNeighbor check).
/// </summary>
public class MeleeEnemyAI : MonoBehaviour
{
    private EnemyMovement movement;
    private EnemyVisuals visuals;

    void Start()
    {
        movement = GetComponent<EnemyMovement>();
        visuals = GetComponent<EnemyVisuals>();
    }

    /// <summary>
    /// Intent hesapla: oyuncuya doğru bir adım.
    /// Oyuncunun yanındaysa hareket etme (saldırı TurnManager'da).
    /// </summary>
    public void LockNextMove(Vector3Int playerCell, bool isStunned)
    {
        if (isStunned || movement.health.currentHP <= 0)
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            return;
        }

        // Flip toward player
        Vector3 playerPos = movement.groundMap.GetCellCenterWorld(playerCell);
        float dxToPlayer = playerPos.x - transform.position.x;
        if (Mathf.Abs(dxToPlayer) > 0.01f && visuals != null && visuals.visualRenderer != null)
        {
            visuals.visualRenderer.flipX = (dxToPlayer < 0);
        }

        Vector3Int cell = movement.GetCurrentCellPosition();

        // Zaten yanındaysa hareket etme
        if (movement.IsNeighbor(cell, playerCell))
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            return;
        }

        movement.lockedTargetCell = movement.CalculateMove(playerCell);

        if (movement.lockedTargetCell != cell)
        {
            movement.hasLockedTarget = true;
            if (visuals != null)
            {
                visuals.PointArrowAt(cell, movement.lockedTargetCell);
                visuals.SetArrowVisibility(true);
            }
        }
        else
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
        }
    }

    /// <summary>
    /// Kilitlenmiş hareketi uygula.
    /// </summary>
    public void ExecuteLockedMove()
    {
        if (movement.IsCurrentlyMoving() || movement.health.currentHP <= 0 || movement.skipTurns > 0)
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            return;
        }

        if (movement.hasLockedTarget)
        {
            movement.TryMoveToLockedTarget();
        }

        movement.hasLockedTarget = false;
        if (visuals != null) visuals.SetArrowVisibility(false);
    }
}
