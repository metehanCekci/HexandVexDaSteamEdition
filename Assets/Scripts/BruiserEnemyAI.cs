using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Bruiser (TelegraphAoE) düşman AI'ı. Menzile girince 1 tur hazırlanır (warning tile),
/// sonraki tur çizgisel AoE saldırı yapar.
/// Movement: Follow (A* pathfinding).
/// </summary>
public class BruiserEnemyAI : MonoBehaviour
{
    [Header("AoE Settings")]
    public int aoeAttackRange = 3;
    public int aoeCooldown = 2;

    [Header("VFX")]
    public GameObject hammerImpactVFXPrefab;
    public float vfxDelayBetweenCells = 0.07f;
    public float vfxYOffset = 0.2f;
    public float vfxXOffset = 0f;

    [HideInInspector] public bool isChargingAttack = false;
    [HideInInspector] public List<Vector3Int> warningCells = new List<Vector3Int>();

    private int currentCooldown = 0;
    private bool isFirstTurn = true;

    private EnemyMovement movement;
    private EnemyVisuals visuals;

    void Start()
    {
        movement = GetComponent<EnemyMovement>();
        visuals = GetComponent<EnemyVisuals>();
    }

    void OnEnable()
    {
        isFirstTurn = true;
        isChargingAttack = false;

        currentCooldown = 0;
        warningCells.Clear();
    }

    public void OnFirstTurnDone() { isFirstTurn = false; }

    /// <summary>
    /// Intent hesapla: charge attack mı yoksa hareket mi?
    /// </summary>
    public void LockNextMove(Vector3Int playerCell, bool isStunned)
    {
        // Already charging — attack is committed, don't re-evaluate
        // (scaffold collapse mid-charge must not cancel the attack)
        if (isChargingAttack && !isStunned && movement.health.currentHP > 0)
            return;

        if (isChargingAttack) ForceClearWarningCells();

        if (isStunned || movement.health.currentHP <= 0)
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            isChargingAttack = false;
    
            return;
        }

        // Only decrement cooldown when not already charging
        if (!isChargingAttack && currentCooldown > 0) currentCooldown--;

        // Flip toward player
        Vector3 playerPos = movement.groundMap.GetCellCenterWorld(playerCell);
        float dxToPlayer = playerPos.x - transform.position.x;
        if (Mathf.Abs(dxToPlayer) > 0.01f && visuals != null && visuals.visualRenderer != null)
        {
            visuals.visualRenderer.flipX = (dxToPlayer < 0);
        }

        Vector3Int cell = movement.GetCurrentCellPosition();

        // Charge attack check — only attack if the line actually covers the player
        if (!isFirstTurn && currentCooldown <= 0 && movement.Distance(cell, playerCell) <= aoeAttackRange)
        {
            List<Vector3Int> candidateCells = GetLineOfCells(cell, playerCell, aoeAttackRange);

            // Only charge if the player is actually on the attack line
            if (candidateCells.Contains(playerCell))
            {
                isChargingAttack = true;

                movement.hasLockedTarget = false;
                if (visuals != null) visuals.SetArrowVisibility(false);
                if (AudioManager.instance != null) AudioManager.instance.PlayCharge();
                if (movement.animator != null) movement.animator.SetBool("IsCharging", true);

                warningCells = candidateCells;

                if (TurnManager.instance != null)
                {
                    foreach (var wCell in warningCells)
                    {
                        TurnManager.instance.DrawWarningTile(wCell);
                    }
                }
                return;
            }
            // Player not on the line — fall through to movement instead of wasting attack
        }
        else
        {
            isChargingAttack = false;
        }

        // Yanındaysa hareket etme
        if (movement.IsNeighbor(cell, playerCell))
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            return;
        }

        // A* pathfinding
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
    /// Kilitlenmiş hareketi uygula. Charge durumundaysa hareket etme.
    /// </summary>
    public void ExecuteLockedMove()
    {
        if (movement.IsCurrentlyMoving() || movement.health.currentHP <= 0 || movement.skipTurns > 0)
        {
            movement.hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
            return;
        }

        if (isChargingAttack) return;

        if (movement.hasLockedTarget)
        {
            movement.TryMoveToLockedTarget();
        }

        movement.hasLockedTarget = false;
        if (visuals != null) visuals.SetArrowVisibility(false);
    }

    /// <summary>
    /// Stun geldiğinde charge'ı iptal et.
    /// </summary>
    public void CancelCharge()
    {
        if (!isChargingAttack) return;
        isChargingAttack = false;

        currentCooldown = 0;
        if (movement.animator != null)
        {
            movement.animator.SetBool("IsCharging", false);
            movement.animator.SetTrigger("GotHit");
        }
        ForceClearWarningCells();
    }

    public void HandleDamaged()
    {
        if (movement.animator == null) return;
        movement.animator.SetBool("IsCharging", false);
        movement.animator.SetTrigger("GotHit");
    }

    // ─── AoE Attack Execution ───

    public IEnumerator ExecuteAoEAttackCoroutine(HexMovement player)
    {
        if (!isChargingAttack) yield break;

        Tilemap warningMap = movement.warningMap;

        if (warningMap != null && warningCells.Count > 0)
        {
            HashSet<Vector3Int> sharedCells = new HashSet<Vector3Int>();
            List<Vector3Int> ownCells = new List<Vector3Int>();
            foreach (var c in warningCells)
            {
                if (IsCellTargetedByOtherEnemy(c)) sharedCells.Add(c);
                else ownCells.Add(c);
            }

            Color attackFlashColor = new Color(1f, 0.8f, 0f, 1f);
            if (movement.animator != null)
            {
                movement.animator.SetBool("IsCharging", false);
                movement.animator.SetTrigger("Attack");
            }
            foreach (var c in ownCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetColor(c, attackFlashColor);
            }
            foreach (var c in sharedCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetColor(c, attackFlashColor);
            }

            yield return new WaitForSeconds(0.08f);

            if (hammerImpactVFXPrefab != null)
                StartCoroutine(SpawnImpactVFXSequence(new List<Vector3Int>(warningCells)));

            float fadeDur = 0.4f;
            float elapsed = 0f;
            Color startFadeColor = attackFlashColor;
            Color endFadeColor = new Color(1f, 0.8f, 0f, 0f);

            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                t = t * t * (3f - 2f * t);

                Color current = Color.Lerp(startFadeColor, endFadeColor, t);
                foreach (var c in ownCells)
                {
                    if (warningMap.HasTile(c)) warningMap.SetColor(c, current);
                }

                Color sharedFade = Color.Lerp(startFadeColor, new Color(1f, 0.2f, 0.2f, 0.65f), t);
                foreach (var c in sharedCells)
                {
                    if (warningMap.HasTile(c)) warningMap.SetColor(c, sharedFade);
                }

                yield return null;
            }

            foreach (var c in sharedCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetColor(c, new Color(1f, 0.2f, 0.2f, 0.65f));
            }
        }

        if (warningCells.Contains(player.GetCurrentCellPosition()))
        {
            bool dodged = false;
            if (RunManager.instance != null) dodged = Random.value < RunManager.instance.dodgeChance;

            if (dodged)
            {
                if (AudioManager.instance != null) AudioManager.instance.PlayShieldBreak();
                if (TurnManager.instance != null && TurnManager.instance.dodgeEffectPrefab != null)
                {
                    Object.Instantiate(TurnManager.instance.dodgeEffectPrefab, player.transform.position, Quaternion.identity);
                }
            }
            else if (RunManager.instance.hasBioBarrier)
            {
                foreach (var perk in RunManager.instance.activePerks) if (perk is BioBarrierPerk aegis) { aegis.BreakShield(); break; }
                RunManager.instance.hasBioBarrier = false;
            }
            else TurnManager.instance.PlayerTakeDamage(2);

            Vector3Int pushTarget = TurnManager.instance.GetOppositeCell(player.GetCurrentCellPosition(), movement.GetCurrentCellPosition());
            player.StartKnockbackMovement(pushTarget);
            yield return new WaitUntil(() => !player.IsMoving());
        }

        isChargingAttack = false;
        // Clean remaining warning tiles
        if (warningMap != null)
        {
            foreach (var c in warningCells)
            {
                if (warningMap.HasTile(c) && !IsCellTargetedByOtherEnemy(c))
                    warningMap.SetTile(c, null);
            }
        }
        warningCells.Clear();
        currentCooldown = aoeCooldown;
        yield return new WaitForSeconds(0.2f);
    }

    // ─── Utility ───

    private List<Vector3Int> GetLineOfCells(Vector3Int startCell, Vector3Int targetCell, int length)
    {
        List<Vector3Int> line = new List<Vector3Int>();
        int bestDirIndex = 0;
        float minDist = float.MaxValue;
        Vector3Int[] startOffsets = (startCell.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;

        for (int i = 0; i < 6; i++)
        {
            float d = movement.Distance(startCell + startOffsets[i], targetCell);
            if (d < minDist)
            {
                minDist = d;
                bestDirIndex = i;
            }
        }

        Vector3Int currentStep = startCell;
        for (int i = 0; i < length; i++)
        {
            Vector3Int[] currOffsets = (currentStep.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;
            currentStep += currOffsets[bestDirIndex];

            if (movement.groundMap.HasTile(currentStep) || (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(currentStep)))
                line.Add(currentStep);
        }
        return line;
    }

    private bool IsCellTargetedByOtherEnemy(Vector3Int targetCell)
    {
        if (TurnManager.instance == null) return false;
        foreach (var e in TurnManager.instance.enemies)
        {
            if (e == null || e == movement) continue;
            var otherBruiser = e.GetComponent<BruiserEnemyAI>();
            if (otherBruiser != null && otherBruiser != this && e.health.currentHP > 0)
            {
                var eVisuals = e.GetComponent<EnemyVisuals>();
                if (eVisuals != null && eVisuals.isFading) continue;
                if (otherBruiser.isChargingAttack && otherBruiser.warningCells.Contains(targetCell)) return true;
            }
        }
        return false;
    }

    public void ForceClearWarningCells()
    {
        Tilemap warningMap = movement != null ? movement.warningMap : null;
        if (warningMap == null) { warningCells.Clear(); return; }
        StartCoroutine(FadeClearWarningCells(new List<Vector3Int>(warningCells), warningMap));
        warningCells.Clear();
    }

    private IEnumerator FadeClearWarningCells(List<Vector3Int> cells, Tilemap warningMap)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        List<Vector3Int> ownCells = new List<Vector3Int>();
        List<Vector3Int> sharedCells = new List<Vector3Int>();
        foreach (var c in cells)
        {
            if (warningMap.HasTile(c))
            {
                if (IsCellTargetedByOtherEnemy(c)) sharedCells.Add(c);
                else ownCells.Add(c);
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(0.65f, 0f, t);
            Color fadeColor = new Color(1f, 1f, 1f, alpha);
            foreach (var c in ownCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetColor(c, fadeColor);
            }
            yield return null;
        }

        foreach (var c in ownCells)
        {
            if (warningMap.HasTile(c)) warningMap.SetTile(c, null);
        }

        foreach (var c in sharedCells)
        {
            if (warningMap.HasTile(c)) warningMap.SetColor(c, new Color(1f, 0.2f, 0.2f, 0.65f));
        }
    }

    private IEnumerator SpawnImpactVFXSequence(List<Vector3Int> cells)
    {
        foreach (var c in cells)
        {
            Vector3 worldPos = movement.groundMap.GetCellCenterWorld(c);
            worldPos.z = 0f;
            worldPos.x += vfxXOffset;
            worldPos.y += vfxYOffset;
            GameObject vfx = Instantiate(hammerImpactVFXPrefab, worldPos, Quaternion.identity);
            Destroy(vfx, 3f);
            if (HitstopManager.instance != null) HitstopManager.instance.TriggerHitstop();
            CameraController.ShakeLighter();
            AudioManager.instance?.PlayHammer();
            yield return new WaitForSeconds(vfxDelayBetweenCells);
        }
    }
}
