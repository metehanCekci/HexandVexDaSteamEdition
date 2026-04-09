using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Düşman hareket ve pozisyon yönetimi. Eski EnemyAI class'ının yeni hali.
/// Saldırı/davranış mantığı ayrı AI component'lerinde (MeleeEnemyAI, BruiserEnemyAI, vb).
/// Visual/UI mantığı EnemyVisuals component'inde.
/// </summary>
public enum MovementStyle { Chase, Evade, None }

public class EnemyMovement : MonoBehaviour
{
    [Header("Hareket Tipi")]
    public MovementStyle movementStyle = MovementStyle.Chase;

    public Tilemap groundMap;
    public HealthScript health;

    [Header("Animasyon")]
    public Animator animator;

    [Header("AoE Uyarı Ayarları")]
    public Tilemap warningMap;
    public TileBase warningTile;

    [Header("Tur Sırası")]
    public int speed = 0; // Yüksek speed = önce hareket eder

    [HideInInspector] public bool isElite = false;

    /// <summary>Neural Hijack: taraf değiştirmiş dost düşman.</summary>
    [HideInInspector] public bool isAllied = false;
    /// <summary>Bir kez dost olan düşman bir daha düşman olamaz.</summary>
    [HideInInspector] public bool wasAllied = false;
    /// <summary>Neural Hijack: dönüştürülürken oyuncunun attığı zar toplamı. Her vuruşta bu kadar hasar verir.</summary>
    [HideInInspector] public int hijackDamage = 0;

    private Vector3Int cell;
    private Vector3 targetWorldPos;
    private bool isMoving = false;
    public bool isBumping = false;

    private const float ENEMY_MOVE_SPEED = 5f;

    public Vector3Int lockedTargetCell;
    public bool hasLockedTarget = false;
    public int skipTurns = 0;

    public static readonly Vector3Int[] oddOffsets = { new Vector3Int(+1, 0, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0) };
    public static readonly Vector3Int[] evenOffsets = { new Vector3Int(+1, 0, 0), new Vector3Int(+1, +1, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(+1, -1, 0) };

    void Awake()
    {
        if (groundMap == null)
        {
            GameObject mapObj = GameObject.Find("Ground");
            if (mapObj != null) groundMap = mapObj.GetComponent<Tilemap>();
        }

        if (warningMap == null)
        {
            GameObject warnObj = GameObject.Find("WarningA");
            if (warnObj != null) warningMap = warnObj.GetComponent<Tilemap>();
        }

        if (warningTile == null && TurnManager.instance != null)
            warningTile = TurnManager.instance.warningTile;
    }

    void Start()
    {
        if (GetComponent<Collider2D>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        cell = groundMap.WorldToCell(transform.position);
        if (TurnManager.instance != null) TurnManager.instance.RegisterEnemy(this);
        targetWorldPos = groundMap.GetCellCenterWorld(cell);

        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnDamaged += HandleDamaged;
        }
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnDamaged -= HandleDamaged;
        }
    }

    void Update()
    {
        HandleMovement();
    }

    // NecroShot & PhaseShift targeting clicks are handled in TurnManager.Update
    // via cell-based + proximity lookup for accurate targeting.

    // ─── Death & Damage Handlers ───

    private void HandleDeath()
    {
        // Bruiser: clear warning cells
        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null)
        {
            bruiser.isChargingAttack = false;
            if (bruiser.warningCells.Count > 0) bruiser.ForceClearWarningCells();
        }

        // Type-specific death notifications
        var totem = GetComponent<TotemEnemyAI>();
        if (totem != null) { totem.OnTotemDeath(); return; }

        var bossAI = GetComponent<SpawnerBossAI>();
        if (bossAI != null && SpawnerBossAI.instance != null) { GameEvents.BossDefeated(); SpawnerBossAI.instance.OnBossDied(); return; }

        var warlock = GetComponent<WarlockEnemyAI>();
        if (warlock != null) { warlock.OnWarlockDied(); return; }

        var ninja = GetComponent<NinjaEnemyAI>();
        if (ninja != null) { ninja.OnNinjaDied(); return; }
    }

    private void HandleDamaged(int remainingHP)
    {
        // Ally iken hasar alınca charge animasyonunu bozma
        if (isAllied) return;

        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null) bruiser.HandleDamaged();

        // Ninja: vurulunca hemen kaç (sersem gibi)
        var ninja = GetComponent<NinjaEnemyAI>();
        if (ninja != null) ninja.OnHit();
    }

    // ─── Movement ───

    private void HandleMovement()
    {
        if (!isMoving || isBumping) return;

        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, ENEMY_MOVE_SPEED * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.001f)
        {
            transform.position = targetWorldPos;
            isMoving = false;
        }
    }

    public void TeleportTo(Vector3Int targetCell)
    {
        Vector3Int oldCell = cell;
        cell = targetCell;
        targetWorldPos = groundMap.GetCellCenterWorld(cell);
        targetWorldPos.z = 0;
        transform.position = targetWorldPos;

        if (ScaffoldManager.instance != null)
        {
            ScaffoldManager.instance.OnEntityLeave(oldCell);
            ScaffoldManager.instance.OnEntityEnter(cell);
        }
    }

    /// <summary>
    /// AI component'lerinin ortak kullandığı "kilitlenen hedefe hareket et" metodu.
    /// Tüm validasyon burada yapılır.
    /// </summary>
    public void TryMoveToLockedTarget()
    {
        if (!IsNeighbor(cell, lockedTargetCell)) return;
        if (TurnManager.instance.IsEnemyAtCell(lockedTargetCell)) return;
        if (TurnManager.instance.player.GetCurrentCellPosition() == lockedTargetCell) return;
        if (!groundMap.HasTile(lockedTargetCell) && !(ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(lockedTargetCell))) return;
        if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(lockedTargetCell)) return;
        if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsCollapsing(lockedTargetCell)) return;

        Vector3Int oldCell = cell;
        cell = lockedTargetCell;
        targetWorldPos = groundMap.GetCellCenterWorld(cell);
        targetWorldPos.z = 0;

        var visuals = GetComponent<EnemyVisuals>();
        float dx = targetWorldPos.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f && visuals != null && visuals.visualRenderer != null)
        {
            visuals.visualRenderer.flipX = (dx < 0);
        }

        isMoving = true;
        if (AudioManager.instance != null) AudioManager.instance.PlayMove();

        if (ScaffoldManager.instance != null)
        {
            ScaffoldManager.instance.OnEntityLeave(oldCell);
            ScaffoldManager.instance.OnEntityEnter(cell);
        }

        // Slippery Secretion: slide on mucus
        CheckMucusSlide(oldCell);
    }

    /// <summary>
    /// Warlock flee hareketi — doğrudan cell'i set eder.
    /// </summary>
    public void ExecuteFleeMove(Vector3Int fleeTarget)
    {
        if (fleeTarget == cell) return;

        Vector3Int oldCell = cell;
        cell = fleeTarget;
        targetWorldPos = groundMap.GetCellCenterWorld(cell);
        targetWorldPos.z = 0;

        var visuals = GetComponent<EnemyVisuals>();
        float dx = targetWorldPos.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.01f && visuals != null && visuals.visualRenderer != null)
            visuals.visualRenderer.flipX = (dx < 0);

        isMoving = true;
        if (AudioManager.instance != null) AudioManager.instance.PlayMove();

        if (ScaffoldManager.instance != null)
        {
            ScaffoldManager.instance.OnEntityLeave(oldCell);
            ScaffoldManager.instance.OnEntityEnter(cell);
        }
    }

    // ─── Stun ───

    public void ApplyStun(int turns, bool showEffect)
    {
        if (RunManager.instance != null)
        {
            foreach (var p in RunManager.instance.activePerks)
            {
                if (p is NeuroStasisMistPerk mist) { turns += mist.GetStunBonus(); break; }
            }
        }

        skipTurns = Mathf.Max(skipTurns, turns);

        var visuals = GetComponent<EnemyVisuals>();
        if (showEffect && visuals != null)
        {
            visuals.ShowStunEffect();
        }

        // Bruiser charge cancel
        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null) bruiser.CancelCharge();
    }

    public void DecreaseStunTurn()
    {
        if (skipTurns > 0)
        {
            skipTurns--;
            if (skipTurns <= 0)
            {
                var visuals = GetComponent<EnemyVisuals>();
                if (visuals != null) visuals.HideStunEffect();
            }
        }
    }

    public void SetStunVisual(bool state)
    {
        if (!state)
        {
            var visuals = GetComponent<EnemyVisuals>();
            if (visuals != null) visuals.ForceHideStunEffect();
        }
    }

    // ─── Wall Bump ───

    public void StartWallBump(Vector3 direction)
    {
        // Totem, Boss, Warlock immune
        if (GetComponent<TotemEnemyAI>() != null) return;
        if (GetComponent<SpawnerBossAI>() != null) return;
        if (GetComponent<WarlockEnemyAI>() != null) return;
        StartCoroutine(WallBumpCoroutine(direction));
    }

    private IEnumerator WallBumpCoroutine(Vector3 direction)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayWall();
        isBumping = true; isMoving = true;
        Vector3 originalPos = groundMap.GetCellCenterWorld(cell); originalPos.z = 0;
        Vector3 bumpPos = originalPos + (direction * 0.10f);

        while (Vector3.Distance(transform.position, bumpPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, bumpPos, 4f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        while (Vector3.Distance(transform.position, originalPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, originalPos, 1f * Time.deltaTime);
            yield return null;
        }

        transform.position = originalPos;
        isBumping = false; isMoving = false;
    }

    // ─── Knockback ───

    public void StartKnockbackMovement(Vector3Int targetCell)
    {
        if (GetComponent<TotemEnemyAI>() != null) return;
        if (GetComponent<SpawnerBossAI>() != null) return;
        if (GetComponent<WarlockEnemyAI>() != null) return;

        bool isScaffold = ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(targetCell);
        if (groundMap.HasTile(targetCell) || isScaffold)
        {
            Vector3Int oldCell = cell;
            cell = targetCell; targetWorldPos = groundMap.GetCellCenterWorld(cell);
            targetWorldPos.z = 0; isMoving = true;

            if (ScaffoldManager.instance != null)
            {
                ScaffoldManager.instance.OnEntityLeave(oldCell);
                ScaffoldManager.instance.OnEntityEnter(cell);
            }

            // Slippery Secretion: slide on mucus after knockback
            CheckMucusSlide(oldCell);
        }
        else
        {
            // Hit a wall — Hydraulic Impact perk check
            if (RunManager.instance != null)
            {
                var perk = RunManager.instance.activePerks.Find(p => p is HydraulicImpactPerk) as HydraulicImpactPerk;
                if (perk != null) perk.ApplyWallDamage(this);
            }
        }
    }

    private void CheckMucusSlide(Vector3Int cameFromCell)
    {
        if (RunManager.instance == null) return;
        var perk = RunManager.instance.activePerks.Find(p => p is SlipperySecretionPerk) as SlipperySecretionPerk;
        if (perk == null || !perk.IsMucusCell(cell)) return;

        int dirIndex = perk.GetSlideDirectionIndex(cell, cameFromCell);
        if (dirIndex < 0) return;

        Vector3Int slideTarget = perk.GetRawSlideTarget(cell, dirIndex);
        if (slideTarget == cell) return;

        perk.TriggerVisualPop();

        bool isScaffold = ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(slideTarget);
        bool isWalkable = groundMap.HasTile(slideTarget) || isScaffold;
        bool hasEnemy = TurnManager.instance != null && TurnManager.instance.IsEnemyAtCell(slideTarget);
        bool hasPlayer = TurnManager.instance != null && TurnManager.instance.player != null && TurnManager.instance.player.GetCurrentCellPosition() == slideTarget;
        bool isHazard = LevelGenerator.instance != null && LevelGenerator.instance.hazardCells != null && LevelGenerator.instance.hazardCells.Contains(slideTarget);

        // Mukus kayması saldırıyı interrupt eder — bu tur saldıramaz
        skipTurns = Mathf.Max(skipTurns, 1);

        if (!isWalkable || hasPlayer)
        {
            // Wall/edge collision — bump animation + wall damage
            Vector3 cPos = groundMap.GetCellCenterWorld(cell);
            Vector3 sPos = groundMap.GetCellCenterWorld(slideTarget);
            cPos.z = 0; sPos.z = 0;
            StartWallBump((sPos - cPos).normalized);

            // Hydraulic Impact bonus
            if (RunManager.instance != null)
            {
                var hiPerk = RunManager.instance.activePerks.Find(p => p is HydraulicImpactPerk) as HydraulicImpactPerk;
                if (hiPerk != null) hiPerk.ApplyWallDamage(this);
            }
        }
        else if (hasEnemy)
        {
            // Slide into another enemy — bump both
            Vector3 cPos = groundMap.GetCellCenterWorld(cell);
            Vector3 sPos = groundMap.GetCellCenterWorld(slideTarget);
            cPos.z = 0; sPos.z = 0;
            Vector3 bumpDir = (sPos - cPos).normalized;
            StartWallBump(bumpDir);

            // Also bump the enemy we collided with + Hydraulic Impact hasar
            if (TurnManager.instance != null)
            {
                EnemyMovement otherEnemy = null;
                foreach (var e in TurnManager.instance.enemies)
                {
                    if (e != null && e != this && e.GetCurrentCellPosition() == slideTarget && e.health.currentHP > 0)
                    { otherEnemy = e; break; }
                }
                if (otherEnemy != null)
                {
                    otherEnemy.StartWallBump(bumpDir);
                    if (RunManager.instance != null)
                    {
                        var hiPerk = RunManager.instance.activePerks.Find(p => p is HydraulicImpactPerk) as HydraulicImpactPerk;
                        if (hiPerk != null) { hiPerk.ApplyWallDamage(this); hiPerk.ApplyWallDamage(otherEnemy); }
                    }
                }
            }
        }
        else
        {
            // Slide to the target cell
            Vector3Int oldCell = cell;
            cell = slideTarget;
            targetWorldPos = groundMap.GetCellCenterWorld(cell);
            targetWorldPos.z = 0;
            isMoving = true;

            if (ScaffoldManager.instance != null)
            {
                ScaffoldManager.instance.OnEntityLeave(oldCell);
                ScaffoldManager.instance.OnEntityEnter(cell);
            }

            // Hazard damage (spikes etc.)
            if (isHazard && health != null)
            {
                health.TakeDamage(1);
            }
        }
    }

    // ─── Combat State Clear (called by EnemyVisuals.FadeDieCoroutine) ───

    public void ClearCombatState()
    {
        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null)
        {
            bruiser.isChargingAttack = false;
            if (bruiser.warningCells.Count > 0) bruiser.ForceClearWarningCells();
        }

        health.currentHP = 0;
        skipTurns = 0;
        hasLockedTarget = false;
    }

    // ─── Dispatch: LockNextMove & ExecuteLockedMove ───
    // TurnManager bu metodları çağırır, biz doğru AI component'ine yönlendiririz.

    public void LockNextMove(Vector3Int playerCell, bool isStunned)
    {
        // None movement: yerinde kalır, intent yok
        if (movementStyle == MovementStyle.None) return;

        var warlock = GetComponent<WarlockEnemyAI>();
        if (warlock != null)
        {
            LockWarlockMove(warlock, playerCell, isStunned);
            return;
        }

        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null)
        {
            // Charge devam ediyorsa warning cell'leri temizleme — LockNextMove zaten koruyacak
            bruiser.LockNextMove(playerCell, isStunned);
            return;
        }

        var ninja = GetComponent<NinjaEnemyAI>();
        if (ninja != null)
        {
            // Ninja: sadece kaçış intent'i göster (saldırı döngüsü ExecuteLockedMove'da)
            if (!isStunned && health.currentHP > 0 && ninja.ShouldFlee(playerCell))
            {
                hasLockedTarget = false;
                var visuals = GetComponent<EnemyVisuals>();
                if (visuals != null) visuals.SetArrowVisibility(false);
            }
            return;
        }

        var melee = GetComponent<MeleeEnemyAI>();
        if (melee != null)
        {
            melee.LockNextMove(playerCell, isStunned);
            return;
        }
    }

    public void ExecuteLockedMove()
    {
        // Notify bruiser first turn done
        var bruiser = GetComponent<BruiserEnemyAI>();
        if (bruiser != null) bruiser.OnFirstTurnDone();

        // Boss: kendi turn mantığı var, movement None olsa bile çalışmalı
        var bossAI = GetComponent<SpawnerBossAI>();
        if (bossAI != null)
        {
            StartCoroutine(bossAI.ExecuteBossTurn());
            return;
        }

        // Warlock: kendi turn mantığı var
        var warlock = GetComponent<WarlockEnemyAI>();
        if (warlock != null)
        {
            ExecuteWarlockMove(warlock);
            return;
        }

        // Ninja: kendi turn coroutine'ini başlat
        var ninja = GetComponent<NinjaEnemyAI>();
        if (ninja != null)
        {
            StartCoroutine(ninja.ExecuteNinjaTurn());
            return;
        }

        // None movement: yerinde kal, hareket etme
        if (movementStyle == MovementStyle.None) return;

        // Bruiser
        if (bruiser != null)
        {
            bruiser.ExecuteLockedMove();
            return;
        }

        // Melee
        var melee = GetComponent<MeleeEnemyAI>();
        if (melee != null)
        {
            melee.ExecuteLockedMove();
            return;
        }
    }

    // ─── Warlock helpers (arrow logic needs visuals) ───

    private void LockWarlockMove(WarlockEnemyAI warlockAI, Vector3Int playerCell, bool isStunned)
    {
        var visuals = GetComponent<EnemyVisuals>();

        if (warlockAI.IsInIdlePhase() && !warlockAI.IsTransitioning() && !isStunned && visuals != null)
        {
            Vector3Int fleeTarget = CalculateEvadeMove(playerCell);
            if (fleeTarget != cell)
            {
                hasLockedTarget = true;
                lockedTargetCell = fleeTarget;
                visuals.PointArrowAt(cell, fleeTarget);
                visuals.SetArrowVisibility(true);
            }
            else
            {
                hasLockedTarget = false;
                visuals.SetArrowVisibility(false);
            }
        }
        else
        {
            hasLockedTarget = false;
            if (visuals != null) visuals.SetArrowVisibility(false);
        }
    }

    private void ExecuteWarlockMove(WarlockEnemyAI warlockAI)
    {
        if (warlockAI.IsInIdlePhase() && !warlockAI.IsTransitioning() && skipTurns <= 0)
        {
            Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
            Vector3Int fleeTarget = CalculateEvadeMove(playerCell);
            ExecuteFleeMove(fleeTarget);
        }
        StartCoroutine(warlockAI.ExecuteWarlockTurn());
    }

    // ─── Movement Dispatch ───

    /// <summary>
    /// Inspector'daki movementStyle'a göre Chase veya Evade hesaplar.
    /// </summary>
    public Vector3Int CalculateMove(Vector3Int playerCell)
    {
        if (movementStyle == MovementStyle.None) return GetCurrentCellPosition();
        return movementStyle == MovementStyle.Evade
            ? CalculateEvadeMove(playerCell)
            : CalculateChaseMove(playerCell);
    }

    // ─── Pathfinding (A*) ───

    public Vector3Int CalculateChaseMove(Vector3Int playerCell)
    {
        List<Vector3Int> openSet = new List<Vector3Int>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        Dictionary<Vector3Int, float> gScore = new Dictionary<Vector3Int, float>();
        Dictionary<Vector3Int, float> fScore = new Dictionary<Vector3Int, float>();

        openSet.Add(cell);
        gScore[cell] = 0;
        fScore[cell] = Distance(cell, playerCell);

        Vector3Int bestReachableCell = cell;
        float closestDistanceToPlayer = Distance(cell, playerCell);

        while (openSet.Count > 0)
        {
            Vector3Int current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (fScore.ContainsKey(openSet[i]) && fScore[openSet[i]] < fScore[current])
                {
                    current = openSet[i];
                }
            }

            if (IsNeighbor(current, playerCell))
            {
                bestReachableCell = current;
                break;
            }

            openSet.Remove(current);
            closedSet.Add(current);

            Vector3Int[] offsets = (current.y % 2 != 0) ? evenOffsets : oddOffsets;

            foreach (var off in offsets)
            {
                Vector3Int neighbor = current + off;

                if (closedSet.Contains(neighbor)) continue;

                bool isScaffold = ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(neighbor);
                if (!groundMap.HasTile(neighbor) && !isScaffold) continue;
                if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells != null && LevelGenerator.instance.hazardCells.Contains(neighbor)) continue;
                if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsCollapsing(neighbor)) continue;
                if (TurnManager.instance != null && TurnManager.instance.IsEnemyAtCell(neighbor) && neighbor != cell) continue;

                float cellPenalty = 0f;

                // Seismic Step: titreyen tile'lardan kaçın
                if (RunManager.instance != null)
                {
                    var seismicPerk = RunManager.instance.activePerks.Find(p => p is SeismicStepPerk) as SeismicStepPerk;
                    if (seismicPerk != null && seismicPerk.IsCellShaking(neighbor))
                        cellPenalty += 20f;
                }

                if (TurnManager.instance != null)
                {
                    foreach (var otherEnemy in TurnManager.instance.enemies)
                    {
                        if (otherEnemy != null && otherEnemy != this && otherEnemy.health.currentHP > 0)
                        {
                            if (Distance(neighbor, otherEnemy.GetCurrentCellPosition()) <= 1.5f)
                            {
                                cellPenalty += 2f;
                            }
                            if (otherEnemy.hasLockedTarget && otherEnemy.lockedTargetCell == neighbor)
                            {
                                cellPenalty += 5f;
                            }

                            var otherBruiser = otherEnemy.GetComponent<BruiserEnemyAI>();
                            if (otherBruiser != null && otherBruiser.isChargingAttack && otherBruiser.warningCells.Contains(neighbor))
                            {
                                cellPenalty += 15f;
                            }
                        }
                    }
                }

                float tentative_gScore = gScore[current] + 1f + cellPenalty;

                if (!gScore.ContainsKey(neighbor) || tentative_gScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative_gScore;
                    fScore[neighbor] = tentative_gScore + Distance(neighbor, playerCell);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }

                    float distToPlayer = Distance(neighbor, playerCell);
                    if (distToPlayer < closestDistanceToPlayer)
                    {
                        closestDistanceToPlayer = distToPlayer;
                        bestReachableCell = neighbor;
                    }
                }
            }
        }

        if (bestReachableCell == cell) return cell;

        Vector3Int step = bestReachableCell;
        while (cameFrom.ContainsKey(step) && cameFrom[step] != cell)
        {
            step = cameFrom[step];
        }

        return step;
    }

    // ─── Evade: Oyuncudan kaç ───

    /// <summary>
    /// Oyuncudan en uzak komşu hücreye kaç. Kaçış yolu yoksa rastgele güvenli hücre seç.
    /// </summary>
    public Vector3Int CalculateEvadeMove(Vector3Int playerCell)
    {
        Vector3Int currentCell = GetCurrentCellPosition();
        Vector3Int[] offsets = (currentCell.y % 2 != 0) ? evenOffsets : oddOffsets;

        Vector3Int bestCell = currentCell;
        float bestDist = Distance(currentCell, playerCell);

        foreach (var off in offsets)
        {
            Vector3Int neighbor = currentCell + off;
            if (!HasWalkableTile(neighbor)) continue;
            if (TurnManager.instance != null && TurnManager.instance.IsEnemyAtCell(neighbor)) continue;
            if (TurnManager.instance != null && TurnManager.instance.player.GetCurrentCellPosition() == neighbor) continue;
            if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells != null && LevelGenerator.instance.hazardCells.Contains(neighbor)) continue;
            if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsCollapsing(neighbor)) continue;

            float dist = Distance(neighbor, playerCell);
            if (dist > bestDist)
            {
                bestDist = dist;
                bestCell = neighbor;
            }
        }

        // Kaçış yolu yoksa rastgele güvenli hücre
        if (bestCell == currentCell)
        {
            List<Vector3Int> safeCells = new List<Vector3Int>();
            foreach (var off in offsets)
            {
                Vector3Int neighbor = currentCell + off;
                if (HasWalkableTile(neighbor) &&
                    (TurnManager.instance == null || !TurnManager.instance.IsEnemyAtCell(neighbor)) &&
                    (TurnManager.instance == null || TurnManager.instance.player.GetCurrentCellPosition() != neighbor) &&
                    (LevelGenerator.instance == null || LevelGenerator.instance.hazardCells == null || !LevelGenerator.instance.hazardCells.Contains(neighbor)))
                {
                    safeCells.Add(neighbor);
                }
            }
            if (safeCells.Count > 0) bestCell = safeCells[Random.Range(0, safeCells.Count)];
        }

        return bestCell;
    }

    private bool HasWalkableTile(Vector3Int c)
    {
        if (groundMap.HasTile(c)) return true;
        return ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(c);
    }

    // ─── Hex Utility ───

    public float Distance(Vector3Int a, Vector3Int b)
    {
        Vector3Int ac = OffsetToCube(a); Vector3Int bc = OffsetToCube(b);
        return (Mathf.Abs(ac.x - bc.x) + Mathf.Abs(ac.y - bc.y) + Mathf.Abs(ac.z - bc.z)) * 0.5f;
    }

    private Vector3Int OffsetToCube(Vector3Int o)
    {
        int x = o.x - (o.y - (o.y & 1)) / 2; int z = o.y; int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    public Vector3Int GetCurrentCellPosition() => cell;

    public void ForceSetPosition(Vector3Int newCell)
    {
        cell = newCell;
        Tilemap gm = TurnManager.instance.groundMap;
        Vector3 worldPos = gm.GetCellCenterWorld(newCell);
        worldPos.z = 0;
        transform.position = worldPos;
    }

    public bool IsMoving() => isMoving || isBumping;
    public bool IsCurrentlyMoving() => isMoving;

    public bool IsNeighbor(Vector3Int cell1, Vector3Int cell2)
    {
        Vector3Int[] offsets = (cell1.y % 2 != 0) ? evenOffsets : oddOffsets;
        foreach (var off in offsets) if (cell1 + off == cell2) return true;
        return false;
    }

    // ─── Backward Compatibility: Fade coroutines delegate to EnemyVisuals ───

    public IEnumerator FadeSpawnCoroutine()
    {
        var visuals = GetComponent<EnemyVisuals>();
        if (visuals != null) yield return StartCoroutine(visuals.FadeSpawnCoroutine());
    }

    public IEnumerator FadeDieCoroutine()
    {
        var visuals = GetComponent<EnemyVisuals>();
        if (visuals != null) yield return StartCoroutine(visuals.FadeDieCoroutine());
    }

    public IEnumerator FadeOutWithoutDestroy()
    {
        var visuals = GetComponent<EnemyVisuals>();
        if (visuals != null) yield return StartCoroutine(visuals.FadeOutWithoutDestroy());
    }

    public void SetArrowVisibility(bool show)
    {
        var visuals = GetComponent<EnemyVisuals>();
        if (visuals != null) visuals.SetArrowVisibility(show);
    }

    // ─── Backward Compat Properties ───
    // These allow external scripts to check state without knowing about sub-components

    public bool isFading
    {
        get
        {
            var visuals = GetComponent<EnemyVisuals>();
            return visuals != null && visuals.isFading;
        }
    }

    public bool isChargingAttack
    {
        get
        {
            var bruiser = GetComponent<BruiserEnemyAI>();
            return bruiser != null && bruiser.isChargingAttack;
        }
    }

    public List<Vector3Int> warningCells
    {
        get
        {
            var bruiser = GetComponent<BruiserEnemyAI>();
            return bruiser != null ? bruiser.warningCells : new List<Vector3Int>();
        }
    }

    public SpriteRenderer visualRenderer
    {
        get
        {
            var visuals = GetComponent<EnemyVisuals>();
            return visuals != null ? visuals.visualRenderer : null;
        }
    }

    // ─── Type check helpers for TurnManager ───

    public bool IsMelee => GetComponent<MeleeEnemyAI>() != null;
    public bool IsBruiser => GetComponent<BruiserEnemyAI>() != null;
    public bool IsTotem => GetComponent<TotemEnemyAI>() != null;
    public bool IsBoss => GetComponent<SpawnerBossAI>() != null;
    public bool IsWarlock => GetComponent<WarlockEnemyAI>() != null;
    public bool IsNinja => GetComponent<NinjaEnemyAI>() != null;
}
