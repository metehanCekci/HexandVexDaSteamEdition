using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager instance;

    [Header("Düşman Uyarı (Warning) Karosu")]
    public Tilemap warningMap;
    public UnityEngine.Tilemaps.TileBase warningTile;

    [Header("Efektler & Prefablar")]
    public GameObject explosionPrefab;
    public GameObject dodgeEffectPrefab;
    public GameObject vacuumVfxPrefab;
    public GameObject slashEffectPrefab; // Düşman hasar alınca çıkan slash efekti
    public float slashEffectYOffset = 0f; // Y ekseninde offset

    // ========================================================
    // MAYIN PREFABI BURAYA GELECEK
    // ========================================================
    public GameObject phantomMinePrefab;

    [Header("Frag-Mine")]
    public GameObject fragMinePlaceholderPrefab;

    public HexMovement player;
    public Tilemap groundMap;

    [Header("Dinamik Zar UI Sistemi")]
    public GameObject dieUIPrefab;
    public Transform diceUIContainer;
    public TMP_Text totalDamageText;
    public Sprite[] diceSprites;
    public GameObject criticalText;
    public GameObject comboTextObj; // Inspector'dan bağla — TMP_Text içermeli
    public UnityEngine.UI.Image dicePanelBackground; // Inspector'dan bağla, Source Image = None


    [Header("Coin UI")]
    public TMP_Text coinText;
    public Sprite coinSprite;

    [HideInInspector] public bool skipDiceAnim { get => diceUI != null ? diceUI.skipDiceAnim : false; set { if (diceUI != null) diceUI.skipDiceAnim = value; } }
    [HideInInspector] public bool skipDiceVisuals { get => diceUI != null ? diceUI.skipDiceVisuals : false; set { if (diceUI != null) diceUI.skipDiceVisuals = value; } }

    public List<EnemyMovement> enemies = new List<EnemyMovement>();
    [HideInInspector] public CoinDropService coinService;
    private DiceUIController diceUI;
    private PerkCombatProcessor perkProcessor;
    [HideInInspector] public bool isLevelClearTriggered = false;
    private bool manualDiceSkip = false;
    private bool speedDiceMode = false;
    [HideInInspector] public bool holdingSkip = false;
    public bool isPlayerTurn = true;
    public bool hasAttackedThisTurn = false;
    public bool isAttackAnimationPlaying = false;
    private bool isCollapsingIslands = false;

    [HideInInspector] public bool isNecroShotTargeting = false;
    [HideInInspector] public bool isBombPlacementTargeting = false;
    [HideInInspector] public bool isPhaseShiftTargeting = false;
    [HideInInspector] public bool isThornPlacementTargeting = false;
    [HideInInspector] public bool isMitsuriTargeting = false;

    // Item targeting iptali için cache
    [HideInInspector] public BaseItem pendingTargetingItem;
    [HideInInspector] public int pendingTargetingSlot = -1;

    // Thorn preview & lifetime tracking
    private GameObject thornPreviewObj;
    private Dictionary<Vector3Int, int> thornTurnsRemaining = new Dictionary<Vector3Int, int>();

    // Ally charge state: ally → pending attack cells (bruiser line veya warlock AoE)
    private Dictionary<EnemyMovement, List<Vector3Int>> allyChargeCells = new Dictionary<EnemyMovement, List<Vector3Int>>();

    public bool IsAnyTargetingActive => isNecroShotTargeting || isBombPlacementTargeting || isPhaseShiftTargeting || isThornPlacementTargeting || isMitsuriTargeting;

    public int hexesMovedThisTurn = 0;

    // MAYIN DEĞİŞKENLERİ
    public Vector3Int activeMineCell = new Vector3Int(-999, -999, -999);
    private GameObject activeMineObj;

    private static readonly Vector3Int[] oddOffsets = HexGridUtils.OddOffsets;
    private static readonly Vector3Int[] evenOffsets = HexGridUtils.EvenOffsets;

    public long finalDamage = 0;

    [Header("Run Reset (R tuşu)")]
    private float holdRTimer = 0f;
    private const float HOLD_R_DURATION = 0.8f;

    [Header("Yeni Oyun Başlangıç Ayarları")]
    public int startingLevel = 1;
    public int startingGold = 0;
    public int startingMaxHP = 5;
    public int startingDiceCount = 2;
    public float startingCritMultiplier = 2.0f;
    public float startingCritChance = 0.10f;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        coinService = new CoinDropService();

        diceUI = gameObject.AddComponent<DiceUIController>();
        diceUI.dieUIPrefab = dieUIPrefab;
        diceUI.diceUIContainer = diceUIContainer;
        diceUI.totalDamageText = totalDamageText;
        diceUI.diceSprites = diceSprites;
        diceUI.criticalText = criticalText;
        diceUI.comboTextObj = comboTextObj;
        diceUI.dicePanelBackground = dicePanelBackground;

        perkProcessor = gameObject.AddComponent<PerkCombatProcessor>();
        perkProcessor.Initialize(diceUI);

        if (player == null) player = FindFirstObjectByType<HexMovement>();
        if (groundMap == null) groundMap = GameObject.Find("Ground")?.GetComponent<Tilemap>();
        if (warningMap == null) warningMap = GameObject.Find("WarningA")?.GetComponent<Tilemap>();
        if (totalDamageText == null) totalDamageText = GameObject.Find("TotalDamageText")?.GetComponent<TMP_Text>();
        if (coinText == null) coinText = GameObject.Find("CoinText")?.GetComponent<TMP_Text>();
    }

    void Start()
    {
        HideDiceResults();
        SetupCoinIcon();
        UpdateCoinUI();

        // Warning tile'lar scaffold'larin ustunde gorunsun
        if (warningMap != null)
        {
            var renderer = warningMap.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.sortingOrder = 2;
        }

        // Scaffold yıkıldığında warning tile'ı temizle
        TrapTileEvents.OnTileDestroyed += OnScaffoldDestroyed;
    }

    void OnDestroy()
    {
        TrapTileEvents.OnTileDestroyed -= OnScaffoldDestroyed;
    }

    private void OnScaffoldDestroyed(Vector3Int cell)
    {
        // Don't remove warning tiles that belong to a charging bruiser
        if (warningMap != null && warningMap.HasTile(cell) && !IsCellTargetedByBruiser(cell))
            warningMap.SetTile(cell, null);

        // During island cascade collapse, skip turn restart and recursive checks
        if (isCollapsingIslands) return;

        // Only restart the player turn if it's currently the player's turn.
        // During enemy phase, scaffold collapse must NOT trigger an extra StartPlayerTurn
        // (otherwise bruiser charge→attack happens in one perceived turn).
        if (isPlayerTurn)
            Invoke("StartPlayerTurn", 0.5f);

        // After any tile collapse, check if enemies are stranded on disconnected islands
        if (player != null && player.health.currentHP > 0 && enemies.Count > 0)
            StartCoroutine(CollapseDisconnectedIslands());
    }

    private bool IsCellTargetedByBruiser(Vector3Int cell)
    {
        foreach (var e in enemies)
        {
            if (e == null || e.health.currentHP <= 0) continue;
            var bruiser = e.GetComponent<BruiserEnemyAI>();
            if (bruiser != null && bruiser.isChargingAttack && bruiser.warningCells.Contains(cell))
                return true;
        }
        return false;
    }


    void Update()
    {
        if (PauseManager.isPaused) return;

        if (diceUI != null) diceUI.CheckFastModeSkip();

        // Speed dice: animasyonları atla ama zarları göster
        if (speedDiceMode && diceUI != null && diceUI.IsDiceAnimPlaying)
            diceUI.skipDiceAnim = true;

        // fastMode veya manuel skip aktifse zarları gizle
        if (RunManager.instance != null)
            skipDiceVisuals = RunManager.instance.fastMode || manualDiceSkip;

        // Hold-to-skip: basılı tutunca her tur otomatik skip at
        if (holdingSkip && isPlayerTurn) SkipTurn();

        // Hold R: basılı tutunca hızlı run reset (fade ile yeni oyun başlat)
        if (Input.GetKey(KeyCode.R))
        {
            holdRTimer += Time.unscaledDeltaTime;
            if (holdRTimer >= HOLD_R_DURATION)
            {
                holdRTimer = 0f;
                StopAllCoroutines();
                Time.timeScale = 1f;

                // Perk menüsünü zorla kapat
                if (LevelUpManager.instance != null)
                {
                    LevelUpManager.instance.StopAllCoroutines();
                    LevelUpManager.instance.ForceClose();
                }

                // Enchant seçim panelini zorla kapat
                if (EnchantNodeUI.instance != null)
                {
                    EnchantNodeUI.instance.StopAllCoroutines();
                    EnchantNodeUI.instance.ForceClose();
                }

                int sceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        ResetGame();
                        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
                    });
                }
                else
                {
                    ResetGame();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
                }
            }
        }
        else
        {
            holdRTimer = 0f;
        }

        // Mitsuri Blade: turu sırasında istediği zaman düşmana tıklayarak saldırabilir
        if (isMitsuriTargeting)
        {
            HandleMitsuriTargetingClick();
        }
        else if (!isMitsuriTargeting && isPlayerTurn && !hasAttackedThisTurn && !isAttackAnimationPlaying
            && RunManager.instance != null && RunManager.instance.selectedWeapon == WeaponType.MitsuriBlade
            && Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()
            && player != null && !player.IsMoving())
        {
            // Oyuncu hareket etmeden de düşmana tıklayarak saldırabilir
            Vector3 mPos = Input.mousePosition;
            mPos.z = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 wPoint = Camera.main.ScreenToWorldPoint(mPos);
            wPoint.z = 0;
            Vector3Int cCell = groundMap.WorldToCell(wPoint);
            EnemyMovement directTarget = GetEnemyAtCell(cCell);
            // Proximity fallback
            if (directTarget == null)
            {
                float best = 1.5f;
                foreach (var e in enemies)
                {
                    if (e == null || e.health.currentHP <= 0) continue;
                    float d = Vector3.Distance(wPoint, e.transform.position);
                    if (d < best) { best = d; directTarget = e; }
                }
            }
            if (directTarget != null && directTarget.health.currentHP > 0)
            {
                isPlayerTurn = false;
                hasAttackedThisTurn = true;
                isAttackAnimationPlaying = true;
                RunManager.instance.remainingMoves = 0;
                player.ClearHighlights();
                List<EnemyMovement> singleTarget = new List<EnemyMovement> { directTarget };
                StartCoroutine(MitsuriAttackSequence(singleTarget));
            }
        }

        // NecroShot / PhaseShift: cell-based + proximity click (bypasses OnMouseDown collider issues)
        if ((isNecroShotTargeting || isPhaseShiftTargeting) && Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 mPos = Input.mousePosition;
            mPos.z = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 wPoint = Camera.main.ScreenToWorldPoint(mPos);
            wPoint.z = 0;
            Vector3Int cCell = groundMap.WorldToCell(wPoint);
            EnemyMovement clickedEnemy = GetEnemyAtCell(cCell);
            if (clickedEnemy == null)
            {
                float best = 1.5f;
                foreach (var e in enemies)
                {
                    if (e == null || e.health.currentHP <= 0) continue;
                    float d = Vector3.Distance(wPoint, e.transform.position);
                    if (d < best) { best = d; clickedEnemy = e; }
                }
            }
            if (clickedEnemy != null)
            {
                if (isNecroShotTargeting) TryNecroShotKill(clickedEnemy);
                else if (isPhaseShiftTargeting) TryPhaseShift(clickedEnemy);
            }
        }

        if ((isBombPlacementTargeting || isThornPlacementTargeting) && Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(mousePos);
            worldPoint.z = 0;
            Vector3Int clickedCell = groundMap.WorldToCell(worldPoint);

            if (HasWalkableTile(clickedCell))
            {
                if (isBombPlacementTargeting) StartCoroutine(ExecuteBombAt(clickedCell));
                else if (isThornPlacementTargeting) ExecuteThornAt(clickedCell);
            }
        }

        // Thorn preview follows mouse during placement
        if (isThornPlacementTargeting && thornPreviewObj != null)
        {
            Vector3 mPos = Input.mousePosition;
            mPos.z = Mathf.Abs(Camera.main.transform.position.z);
            Vector3 wp = Camera.main.ScreenToWorldPoint(mPos);
            wp.z = 0;
            Vector3Int hoverCell = groundMap.WorldToCell(wp);

            if (HasWalkableTile(hoverCell) && !IsThornCellBlocked(hoverCell))
            {
                Vector3 snapPos = groundMap.GetCellCenterWorld(hoverCell);
                snapPos.z = 0;
                thornPreviewObj.transform.position = snapPos;
                thornPreviewObj.SetActive(true);
            }
            else
            {
                thornPreviewObj.SetActive(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            DebugSpawnWalkerAndBruiser();
        }
        if (Input.GetKeyDown(KeyCode.F6))
        {
            // Cheat: tüm state'leri resetle ki level clear akışı takılmasın
            isAttackAnimationPlaying = false;
            isLevelClearTriggered = false;

            // Boss kalkanını kaldır yoksa TakeDamage işlemez
            if (SpawnerBossAI.instance != null) SpawnerBossAI.instance.isShielded = false;

            foreach (var e in new List<EnemyMovement>(enemies))
                if (e != null && e.health.currentHP > 0) e.health.TakeDamage(e.health.currentHP);
            enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);
            CleanupDeadAndCheckLevelClear();
        }
        if (Input.GetKeyDown(KeyCode.F7))
        {
            RunManager.instance.currentGold += 10000;
            UpdateCoinUI();
            GameEvents.GoldChanged(RunManager.instance.currentGold);
        }
        if (Input.GetKeyDown(KeyCode.F8)) { if (player != null) player.health.TakeDamage(player.health.currentHP + 999); }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            if (PerkCollectionManager.instance != null)
            {
                PerkCollectionManager.instance.UnlockAll();
                Debug.Log("<color=#00FF00>[CHEAT]</color> All collection perks unlocked!");
            }
        }
        if (Input.GetKeyDown(KeyCode.F10)) DebugEquipAllPerks();
        if (Input.GetKeyDown(KeyCode.F12)) DebugUpgradeAllPerks();
    }

    private void DebugSpawnWalkerAndBruiser()
    {
        if (LevelGenerator.instance == null || player == null) return;
        var ts = LevelGenerator.instance.GetActiveTileSet();
        if (ts?.enemies == null || ts.enemies.Length == 0) return;

        Vector3Int playerCell = player.GetCurrentCellPosition();
        // 1 hex altı = (0, -1), 2 hex altı = (0, -2)
        Vector3Int walkerCell = playerCell + new Vector3Int(0, -1, 0);
        Vector3Int bruiserCell = walkerCell + new Vector3Int(0, -1, 0);

        // Walker = enemies[0] (varsayılan melee)
        GameObject walkerPrefab = ts.enemies[0].prefab;

        // Bruiser = BruiserEnemyAI olan prefab
        GameObject bruiserPrefab = null;
        foreach (var entry in ts.enemies)
        {
            if (entry.prefab != null && entry.prefab.GetComponent<BruiserEnemyAI>() != null)
            {
                bruiserPrefab = entry.prefab;
                break;
            }
        }
        if (bruiserPrefab == null) { Debug.Log("[F2] Bu tileset'te bruiser prefab yok!"); return; }

        // Walker spawn (yukarıda)
        if (HasWalkableTile(walkerCell) && !IsEnemyAtCell(walkerCell) && (player.GetCurrentCellPosition() != walkerCell))
            DebugSpawnSingleEnemy(walkerPrefab, walkerCell);

        // Bruiser spawn (aşağıda)
        if (HasWalkableTile(bruiserCell) && !IsEnemyAtCell(bruiserCell) && (player.GetCurrentCellPosition() != bruiserCell))
            DebugSpawnSingleEnemy(bruiserPrefab, bruiserCell);

        StartCoroutine(LockIntentsNextFrame());
        Debug.Log("[F2] Walker + Bruiser spawned below player");
    }

    private void DebugSpawnSingleEnemy(GameObject prefab, Vector3Int cell)
    {
        Vector3 pos = groundMap.GetCellCenterWorld(cell); pos.z = 0;
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);
        EnemyMovement ai = obj.GetComponent<EnemyMovement>();
        ai.groundMap = groundMap;
        ai.health.maxHP = 50; ai.health.currentHP = 50; ai.health.updateHealth();
        RegisterEnemy(ai);
        StartCoroutine(ai.FadeSpawnCoroutine());
    }

    private void SpawnDebugAoEEnemies()
    {
        var _dbgTs = LevelGenerator.instance?.GetActiveTileSet();
        if (LevelGenerator.instance == null || _dbgTs?.enemies == null || _dbgTs.enemies.Length == 0) return;
        Vector3Int playerCell = player.GetCurrentCellPosition();
        Vector3Int[] oddOff = { new Vector3Int(+1, 0, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0) };
        Vector3Int[] evenOff = { new Vector3Int(+1, 0, 0), new Vector3Int(+1, +1, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(+1, -1, 0) };
        Vector3Int[] offsets = (playerCell.y % 2 != 0) ? evenOff : oddOff;
        List<Vector3Int> spawnCells = new List<Vector3Int>();

        int[][] pairs = { new[] { 0, 3 }, new[] { 1, 4 }, new[] { 2, 5 } };
        foreach (var pair in pairs)
        {
            Vector3Int c1 = playerCell + offsets[pair[0]];
            Vector3Int c2 = playerCell + offsets[pair[1]];
            if (HasWalkableTile(c1) && HasWalkableTile(c2) && !IsEnemyAtCell(c1) && !IsEnemyAtCell(c2))
            {
                Vector3Int[] off1 = (c1.y % 2 != 0) ? evenOff : oddOff; Vector3Int far1 = c1 + off1[pair[0]];
                Vector3Int[] off2 = (c2.y % 2 != 0) ? evenOff : oddOff; Vector3Int far2 = c2 + off2[pair[1]];
                if (HasWalkableTile(far1) && HasWalkableTile(far2) && !IsEnemyAtCell(far1) && !IsEnemyAtCell(far2)) { spawnCells.Add(far1); spawnCells.Add(far2); break; }
                spawnCells.Add(c1); spawnCells.Add(c2); break;
            }
        }
        if (spawnCells.Count < 2) return;
        foreach (var spawnCell in spawnCells)
        {
            Vector3 spawnPos = groundMap.GetCellCenterWorld(spawnCell); spawnPos.z = 0;
            var _dbgEnemies = LevelGenerator.instance.GetActiveTileSet().enemies;
            GameObject obj = Instantiate(_dbgEnemies[Mathf.Min(1, _dbgEnemies.Length - 1)].prefab, spawnPos, Quaternion.identity);
            EnemyMovement ai = obj.GetComponent<EnemyMovement>(); ai.groundMap = groundMap;
            ai.health.maxHP = 50; ai.health.currentHP = 50; ai.health.updateHealth();
            RegisterEnemy(ai); StartCoroutine(ai.FadeSpawnCoroutine());
        }
    }

    private void DebugEquipAllPerks()
    {
        if (LevelUpManager.instance == null || RunManager.instance == null) return;
        var lum = LevelUpManager.instance;
        List<List<GameObject>> allLists = new List<List<GameObject>> { lum.commonPerks, lum.rarePerks, lum.epicPerks, lum.legendaryPerks };
        int added = 0;
        foreach (var list in allLists)
        {
            foreach (var prefab in list)
            {
                if (prefab == null) continue;
                BasePerk ps = prefab.GetComponent<BasePerk>();
                if (RunManager.instance.activePerks.Exists(p => p.GetType() == ps.GetType())) continue;
                RunManager.instance.AddPerk(prefab);
                added++;
            }
        }
        Debug.Log($"[DEBUG] {added} perk eklendi!");
    }

    private void DebugUpgradeAllPerks()
    {
        if (RunManager.instance == null) return;
        int upgraded = 0;
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk == null) continue;
            while (perk.currentLevel < perk.maxLevel)
            {
                perk.Upgrade();
                upgraded++;
            }
        }
        Debug.Log($"[DEBUG] {upgraded} upgrade yapıldı! Tüm perkler max seviyede.");
    }

    public void StartPlayerTurn()
    {
        if (player == null || player.health.currentHP <= 0) return;

        isPlayerTurn = true;
        hasAttackedThisTurn = false;
        isAttackAnimationPlaying = false;

        if (RunManager.instance != null)
        {
            int moves = RunManager.instance.extraMovesPerTurn;
            RunManager.instance.remainingMoves = moves;

            // Surge Boot: her tur başında sıfırla (Use() anında aktifleştirir)
            RunManager.instance.surgeBootActive = false;
            RunManager.instance.surgeBootNextTurn = false;

            // Blue Magic Tile: activate surge boot (range-2 movement) while standing on it
            if (MagicTileManager.instance != null && MagicTileManager.instance.IsPlayerOnMagicTile(out MagicTileType blueCheck) && blueCheck == MagicTileType.Blue)
            {
                RunManager.instance.surgeBootActive = true;
                MagicTileManager.instance.MarkBlueTileActive(player.GetCurrentCellPosition());
            }

            // Orange Magic Tile: handled on arrival in HandlePlayerPhase (not at turn start)
        }
        TickThornLifetimes();
        if (CleanupDeadAndCheckLevelClear()) return;
        player.UpdateHighlights();
        StartCoroutine(LockIntentsNextFrame());

        // MitsuriBlade: tur başında menzil göstergelerini aç
        if (RunManager.instance != null && RunManager.instance.selectedWeapon == WeaponType.MitsuriBlade)
            ShowMitsuriRangeIndicators();
    }

    // ========================================================
    // YENİ: MAYIN BIRAKMA / YER DEĞİŞTİRME KOMUTU
    // ========================================================
    public void TryDropMine(Vector3Int cell, Vector3 offset = default)
    {
        if (activeMineObj != null)
        {
            Destroy(activeMineObj);
        }

        activeMineCell = cell;
        Vector3 pos = groundMap.GetCellCenterWorld(cell);
        pos.z = 0;
        pos += offset; // Offset ekle

        if (phantomMinePrefab != null)
        {
            activeMineObj = Instantiate(phantomMinePrefab, pos, Quaternion.identity);
        }
    }
    public void ResetGame()
    {
        // Perk menüsünü zorla kapat
        if (LevelUpManager.instance != null)
            LevelUpManager.instance.ForceClose();

        // 1. Zamanı normale döndür (Pause'dan geliyorsa)
        Time.timeScale = 1f;

        // Düşman scaling'ini sıfırla
        LevelGenerator.ResetBossMultiplier();

        // 2. RunManager verilerini sıfırla
        if (RunManager.instance != null)
        {
            RunManager rm = RunManager.instance;

            rm.currentLevel = startingLevel;
            rm.currentGold = startingGold;
            rm.playerMaxHealth = startingMaxHP;
            rm.playerCurrentHealth = startingMaxHP; // Canı fulle
            rm.baseDiceCount = startingDiceCount;
            rm.criticalDamageMultiplier = startingCritMultiplier;

            // Diğer gizli statları sıfırla
            rm.armorChance = 0f;
            rm.dodgeChance = 0f;
            rm.bonusGold = 0;
            rm.hasBioBarrier = false;
            rm.luckyCloverLevel = 0;
            rm.criticalChance = startingCritChance;
            rm.shopRerollStack = 0;
            rm.extraMovesPerTurn = 0;
            rm.remainingMoves = 0;
            rm.bonusGoldPerKill = 0;
            rm.skipBonusGold = 0;
            rm.bonusDiceNextCombat = 0;
            rm.doubleGoldNextKill = false;
            rm.doubleDamageNextCombat = false;
            rm.cleaveNextCombat = false;
            rm.surgeBootNextTurn = false;
            rm.surgeBootActive = false;
            rm.hasPerkReroll = false;
            rm.hasLuckyClover = false;
            rm.pendingRerollReset = false;
            // selectedWeapon sıfırlanMAZ — silah seçimi MainMenu'de yapılır, ResetGame silahı ezmesin

            // Perkleri temizle (Sahnedeki objeleri yok et)
            foreach (BasePerk perk in rm.activePerks)
            {
                if (perk != null) Destroy(perk.gameObject);
            }
            rm.activePerks.Clear();
            rm.acquiredMagicTiles.Clear();

            // İstatistikleri (Stats) sıfırla
            rm.totalEnemiesKilled = 0;
            rm.totalDamageDealt = 0;
            rm.totalDamageReceived = 0;
            rm.totalTurnsPlayed = 0;
            rm.totalDiceRolled = 0;
            rm.totalGoldEarned = 0;
            rm.totalLevelsPlayed = 0;
        }

        // 3. TurnManager'ın kendi listelerini temizle
        enemies.Clear();
        isLevelClearTriggered = false;
        hasAttackedThisTurn = false;
        isMitsuriTargeting = false;

        // 4. DontDestroyOnLoad UI'ları temizle
        if (ActivePerkBar.instance != null)
            Destroy(ActivePerkBar.instance.gameObject);
        if (HotbarUI.instance != null)
            Destroy(HotbarUI.instance.gameObject);
        if (PerkInventoryUI.instance != null)
            Destroy(PerkInventoryUI.instance.gameObject);
    }
    public void ClearWarningMap()
    {
        GameObject warningAObj = GameObject.Find("WarningA");
        if (warningAObj != null) warningAObj.GetComponent<Tilemap>().ClearAllTiles();
        GameObject warningBObj = GameObject.Find("WarningB");
        if (warningBObj != null) warningBObj.GetComponent<Tilemap>().ClearAllTiles();

        // Warlock uyarı haritasını da temizle
        GameObject warlockWarnObj = GameObject.Find("WarlockWarningMap");
        if (warlockWarnObj != null) warlockWarnObj.GetComponent<Tilemap>().ClearAllTiles();

        if (activeMineObj != null) Destroy(activeMineObj);
        activeMineCell = new Vector3Int(-999, -999, -999);
    }

    public void PlayerTakeDamage(long amt)
    {
        if (player == null || player.health.currentHP <= 0) return;
        ResetCombo();

        if (player.health.currentHP - amt <= 0)
        {
            var bribe = RunManager.instance.activePerks.FirstOrDefault(p => p is BribePerk);
            if (bribe != null)
            {
                RunManager.instance.currentGold = 0; UpdateCoinUI(); player.health.Heal(9999);
                RunManager.instance.activePerks.Remove(bribe); Destroy(bribe.gameObject);

                // DODGE EFEKTİNİ COROUTINE İLE ÇAĞIRIYORUZ (Kalkan Kırılma Animasyonu)
                StartCoroutine(AnimateShieldBreakFX(player.transform.position));
                return;
            }

            // Ouroboros: ölmeden diriliş — perk seviyeleri düşer
            var ouroboros = RunManager.instance.activePerks.Find(p => p is OuroborosPerk) as OuroborosPerk;
            if (ouroboros != null && ouroboros.CanRevive())
            {
                ouroboros.Revive();
                StartCoroutine(AnimateShieldBreakFX(player.transform.position));
                return;
            }
        }
        player.health.TakeDamage(amt);
    }

    // ========================================================
    // DODGE / BRIBE İÇİN KALKAN KIRILMA EFEKTİ
    // ========================================================

    /// <summary>
    /// Public wrapper: düşman AI'ları dahil her yerden çağrılabilir.
    /// Kalkan kırılma efektini başlatır ve otomatik olarak temizler.
    /// </summary>
    public void PlayShieldBreakFX(Vector3 pos)
    {
        StartCoroutine(AnimateShieldBreakFX(pos));
    }

    private IEnumerator AnimateShieldBreakFX(Vector3 pos)
    {
        if (dodgeEffectPrefab == null) yield break;

        if (AudioManager.instance != null) AudioManager.instance.PlayShieldBreak();
        GameObject fx = Instantiate(dodgeEffectPrefab, pos, Quaternion.identity);
        SpriteRenderer[] renderers = fx.GetComponentsInChildren<SpriteRenderer>();

        float duration = 0.35f;
        float elapsed = 0f;

        // Küçükten büyüyen bir efekt için scale değerleri
        Vector3 startScale = Vector3.one * 2.4f;
        Vector3 endScale = Vector3.one * 9.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Objeyi büyütüyoruz
            fx.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            // Giderek saydamlaştırıyoruz
            foreach (var sr in renderers)
            {
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    sr.color = c;
                }
            }
            yield return null;
        }

        Destroy(fx);
    }

    private IEnumerator WaitAndTriggerLevelClear()
    {
        if (isLevelClearTriggered) yield break;
        isLevelClearTriggered = true;

        while (isAttackAnimationPlaying) yield return null;
        if (CoinDropVFX.instance != null) while (CoinDropVFX.instance.activeCoinCount > 0) yield return null;
        yield return new WaitForSeconds(0.3f);

        RunManager.instance.totalLevelsPlayed++;
        int lifetimeLevels = PlayerPrefs.GetInt("lifetime_levels_cleared", 0) + 1;
        PlayerPrefs.SetInt("lifetime_levels_cleared", lifetimeLevels);
        GameEvents.LevelCleared(lifetimeLevels);

        // Combat bitti — tüm item cooldown'larını resetle
        if (InventoryManager.instance != null)
            InventoryManager.instance.ResetAllItemCooldowns();

        // Level temizlendiğinde perklerin OnLevelClear callback'ini çağır
        if (RunManager.instance != null)
        {
            foreach (var perk in RunManager.instance.activePerks)
            {
                if (perk != null) perk.OnLevelClear();
            }
        }

        // Map sistemi aktifse → haritaya dön
        if (MapManager.instance != null)
        {
            MapManager.instance.OnNodeComplete();
        }
        else if (Shopmanager.instance != null)
        {
            bool isBossLevel = RunManager.instance.currentLevel > 0 && RunManager.instance.currentLevel % 5 == 0;
            if (isBossLevel) Shopmanager.instance.OnBossCleared(); else Shopmanager.instance.OnDungeonCleared();
        }
        else if (LevelUpManager.instance != null) LevelUpManager.instance.ShowLevelUpScreen();
    }

    private void SetupCoinIcon()
    {
        if (coinSprite == null) { var vfx = FindFirstObjectByType<CoinDropVFX>(); if (vfx != null) coinSprite = vfx.coinSprite; }
        if (coinText == null || coinSprite == null) return;
        Transform parent = coinText.transform.parent;
        if (parent == null || parent.Find("CoinIcon") != null) return;

        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
        }

        GameObject iconGO = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(parent, false); iconGO.transform.SetAsFirstSibling();
        RectTransform iconRT = iconGO.GetComponent<RectTransform>(); iconRT.sizeDelta = new Vector2(22f, 22f);
        LayoutElement le = iconGO.AddComponent<LayoutElement>(); le.preferredWidth = 22f; le.preferredHeight = 22f;
        Image img = iconGO.GetComponent<Image>(); img.sprite = coinSprite; img.preserveAspect = true; img.raycastTarget = false;
    }

    public void UpdateCoinUI()
    {
        if (coinText != null && RunManager.instance != null) coinText.text = RunManager.instance.currentGold.ToString();
        if (Shopmanager.instance != null) Shopmanager.instance.RefreshAffordability();
        if (PersistentHUD.instance != null) PersistentHUD.instance.Refresh();
    }

    public void RegisterEnemy(EnemyMovement enemy) { if (!enemies.Contains(enemy)) enemies.Add(enemy); }
    public void SetTargetingItemCache(BaseItem item, int slotIndex)
    {
        pendingTargetingItem = item;
        pendingTargetingSlot = slotIndex;
    }

    public void CancelTargeting()
    {
        if (!IsAnyTargetingActive) return;

        // Restore item cooldown (item stays in slot, just clear the per-combat flag)
        if (pendingTargetingItem != null && pendingTargetingSlot >= 0)
        {
            pendingTargetingItem.usedThisCombat = false;
            GameEvents.InventoryChanged();
        }
        pendingTargetingItem = null;
        pendingTargetingSlot = -1;

        isNecroShotTargeting = false;
        isBombPlacementTargeting = false;
        isPhaseShiftTargeting = false;
        if (isThornPlacementTargeting) { isThornPlacementTargeting = false; DestroyThornPreview(); }
        // Mitsuri is weapon-based, not item-based — don't cancel with ESC
    }

    private void ClearTargetingCache() { pendingTargetingItem = null; pendingTargetingSlot = -1; }

    public void StartNecroShotTargeting() { isNecroShotTargeting = true; }

    // ──────── Mitsuri Blade Range Indicators ────────

    private List<Vector3Int> mitsuriRangeIndicatorCells = new List<Vector3Int>();
    private Coroutine mitsuriPulseCoroutine;

    /// <summary>
    /// Oyuncunun turu başlayınca tüm düşmanların altını renklendirir:
    /// Yeşil = tam hasar (1-2 hex), Kırmızı = cezalı (3+ hex, 1 zar)
    /// </summary>
    public void ShowMitsuriRangeIndicators()
    {
        ClearMitsuriRangeIndicators();
        if (warningMap == null || warningTile == null) return;
        if (player == null) return;

        Vector3Int playerCell = player.GetCurrentCellPosition();
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.health.currentHP <= 0) continue;
            Vector3Int eCell = enemy.GetCurrentCellPosition();
            float dist = HexGridUtils.DistanceCube(playerCell, eCell);
            bool isPenalty = dist >= 3f;

            warningMap.SetTile(eCell, warningTile);
            warningMap.SetTileFlags(eCell, TileFlags.None);
            warningMap.SetColor(eCell, isPenalty
                ? new Color(1f, 0.25f, 0.25f, 0.6f)
                : new Color(0.25f, 1f, 0.4f, 0.6f));
            mitsuriRangeIndicatorCells.Add(eCell);

            // Düşman sprite tint
            if (enemy.visualRenderer != null)
            {
                enemy.visualRenderer.color = isPenalty
                    ? new Color(1f, 0.6f, 0.6f, 1f)   // kırmızımsı tint
                    : new Color(0.7f, 1f, 0.7f, 1f);   // yeşilimsi tint
            }
        }

        // Pulse animasyonu başlat
        if (mitsuriPulseCoroutine != null) StopCoroutine(mitsuriPulseCoroutine);
        mitsuriPulseCoroutine = StartCoroutine(MitsuriRangePulse());
    }

    public void ClearMitsuriRangeIndicators()
    {
        if (mitsuriPulseCoroutine != null) { StopCoroutine(mitsuriPulseCoroutine); mitsuriPulseCoroutine = null; }
        if (warningMap != null)
        {
            foreach (var cell in mitsuriRangeIndicatorCells)
                if (warningMap.HasTile(cell)) warningMap.SetTile(cell, null);
        }
        mitsuriRangeIndicatorCells.Clear();

        // Düşman sprite renklerini resetle
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.visualRenderer != null && enemy.health.currentHP > 0)
                enemy.visualRenderer.color = Color.white;
        }
    }

    private IEnumerator MitsuriRangePulse()
    {
        float time = 0f;
        while (true)
        {
            time += Time.deltaTime * 2.5f;
            float pulse = Mathf.Sin(time) * 0.15f + 0.55f; // 0.4 ~ 0.7 arası alpha

            Vector3Int playerCell = player != null ? player.GetCurrentCellPosition() : Vector3Int.zero;
            foreach (var cell in mitsuriRangeIndicatorCells)
            {
                if (!warningMap.HasTile(cell)) continue;
                // Rengi tekrar hesapla (oyuncu hareket etmiş olabilir)
                Color base_col = warningMap.GetColor(cell);
                warningMap.SetColor(cell, new Color(base_col.r, base_col.g, base_col.b, pulse));
            }
            yield return null;
        }
    }

    /// <summary>
    /// Oyuncu hareket ettiğinde mitsuri range indicator'larını güncelle
    /// </summary>
    public void RefreshMitsuriRangeIndicators()
    {
        if (RunManager.instance == null || RunManager.instance.selectedWeapon != WeaponType.MitsuriBlade) return;
        if (!isPlayerTurn || hasAttackedThisTurn) return;
        ShowMitsuriRangeIndicators();
    }

    // ──────── Mitsuri Blade Targeting ────────

    /// <summary>
    /// MitsuriBlade ile düşman seçme modunu başlatır. Tüm düşmanları warningMap'te highlight eder.
    /// </summary>
    public void StartMitsuriTargeting()
    {
        isMitsuriTargeting = true;
        ShowMitsuriHighlights();
    }

    // Mitsuri targeting sırasında highlight edilen hücreler
    private List<Vector3Int> mitsuriHighlightedCells = new List<Vector3Int>();

    private void ShowMitsuriHighlights()
    {
        if (warningMap == null || warningTile == null) return;

        mitsuriHighlightedCells.Clear();
        Vector3Int playerCell = player.GetCurrentCellPosition();

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.health.currentHP <= 0) continue;
            Vector3Int eCell = enemy.GetCurrentCellPosition();
            float dist = HexGridUtils.DistanceCube(playerCell, eCell);

            warningMap.SetTile(eCell, warningTile);
            warningMap.SetTileFlags(eCell, TileFlags.None);

            // 3+ hex mesafede kırmızımsı (ceza var), yakında yeşilimsi
            bool isPenaltyRange = dist >= 3f;
            Color col = isPenaltyRange
                ? new Color(1f, 0.3f, 0.3f, 0.75f)   // kırmızı — cezalı menzil
                : new Color(0.3f, 1f, 0.4f, 0.75f);   // yeşil — normal menzil
            warningMap.SetColor(eCell, col);

            mitsuriHighlightedCells.Add(eCell);
        }
    }

    private void ClearMitsuriHighlights()
    {
        if (warningMap == null) return;

        foreach (var cell in mitsuriHighlightedCells)
        {
            if (warningMap.HasTile(cell))
                warningMap.SetTile(cell, null);
        }
        mitsuriHighlightedCells.Clear();
    }

    /// <summary>
    /// MitsuriBlade targeting modunda düşmana tıklandığında çağrılır.
    /// Düşmanın tam üstüne değil, düşmana yakın herhangi bir yere tıklamayı da kabul eder.
    /// </summary>
    private void HandleMitsuriTargetingClick()
    {
        if (!isMitsuriTargeting) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(mousePos);
        worldPoint.z = 0;
        Vector3Int clickedCell = groundMap.WorldToCell(worldPoint);

        // Önce tıklanan hücrede düşman var mı bak
        EnemyMovement target = GetEnemyAtCell(clickedCell);

        // Tıklanan hücrede düşman yoksa, düşmanların world pozisyonuna yakınlık kontrolü yap
        if (target == null)
        {
            float bestDist = 1.5f; // Maksimum tıklama toleransı (world unit)
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.health.currentHP <= 0) continue;
                float d = Vector3.Distance(worldPoint, enemy.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    target = enemy;
                }
            }
        }

        if (target == null || target.health.currentHP <= 0) return;

        // Hedef seçildi — saldırıyı başlat
        isMitsuriTargeting = false;
        ClearMitsuriHighlights();

        hasAttackedThisTurn = true;
        isAttackAnimationPlaying = true;
        RunManager.instance.remainingMoves = 0;

        // Tek hedefli saldırı
        List<EnemyMovement> singleTarget = new List<EnemyMovement> { target };
        StartCoroutine(MitsuriAttackSequence(singleTarget));
    }

    private IEnumerator MitsuriAttackSequence(List<EnemyMovement> targets)
    {
        ClearMitsuriRangeIndicators();
        yield return StartCoroutine(MultiAttack(targets));

        if (player != null && player.health.currentHP > 0)
        {
            player.ClearHighlights();
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(EnemyPhase());
        }
    }

    public void TryNecroShotKill(EnemyMovement target)
    {
        if (!isNecroShotTargeting || target == null) return;
        if (target.IsBoss) return;
        isNecroShotTargeting = false;
        ClearTargetingCache();
        if (player != null) player.TriggerAttackAnimation();

        target.health.TakeDamage(target.health.currentHP);
        coinService.ProcessKillRewards(target);
        UpdateCoinUI();
        CleanupDeadAndCheckLevelClear();
    }

    public void StartBombPlacement() { isBombPlacementTargeting = true; }
    private IEnumerator ExecuteBombAt(Vector3Int cell)
    {
        isBombPlacementTargeting = false;
        ClearTargetingCache();

        // Placeholder'ı hemen koy, patlamadan önce göster
        GameObject placeholderObj = null;
        if (fragMinePlaceholderPrefab != null)
        {
            Vector3 placeholderPos = groundMap.GetCellCenterWorld(cell);
            placeholderPos.z = 0;
            placeholderObj = Instantiate(fragMinePlaceholderPrefab, placeholderPos, Quaternion.identity);
        }

        // Zar animasyonunu göster
        int diceCount = RunManager.instance != null ? RunManager.instance.baseDiceCount : 2;
        List<int> rolls = new List<int>();
        for (int i = 0; i < diceCount; i++) rolls.Add(Random.Range(1, 7));
        if (RunManager.instance != null) RunManager.instance.totalDiceRolled += diceCount;

        // Volatile Roll: zarları baştan 1/6 yap
        var volatilePerkBomb = RunManager.instance != null
            ? RunManager.instance.activePerks.Find(p => p is VolatileRollPerk) as VolatileRollPerk
            : null;
        if (volatilePerkBomb != null)
            volatilePerkBomb.ApplyToBaseRolls(rolls);

        CombatPayload payload = new CombatPayload(rolls);
        if (RunManager.instance != null && RunManager.instance.activePerks.Exists(p => p.GetType().Name == "SymbioticFuryPerk"))
            payload.multiplyInsteadOfAdd = true;

        diceUI.BeginDiceAnim();
        if (!skipDiceVisuals)
        {
            yield return StartCoroutine(diceUI.ShowDiceSequence(rolls));
            UpdateTotalDamageDisplay(payload.GetFinalDamage());

            // Volatile Roll: 6 gelince zincirleme extra zarlar (animasyonlu)
            if (volatilePerkBomb != null)
                yield return StartCoroutine(VolatileRollChainAnimation(volatilePerkBomb, rolls, payload));
        }
        else if (volatilePerkBomb != null)
        {
            int chainStart = 0; int chainCount = 0;
            while (chainCount < 50)
            {
                int prevCount = rolls.Count;
                var extras = volatilePerkBomb.GenerateChainRolls(rolls, chainStart);
                if (extras.Count == 0) break;
                rolls.AddRange(extras);
                payload.diceRolls.AddRange(extras);
                chainStart = prevCount;
                chainCount++;
            }
        }

        // Perk zar boost'larını uygula
        yield return StartCoroutine(perkProcessor.ProcessPerks(payload, rolls));

        // LetsGoAgain: Tüm perkler bir kez daha tetiklenir (bomb combat)
        yield return StartCoroutine(perkProcessor.ProcessLetsGoAgainPass(payload, rolls));

        if (!skipDiceVisuals && Random.value < RunManager.instance.criticalChance)
        {
            payload.isCriticalHit = true;
            UpdateTotalDamageDisplay(payload.GetFinalDamage());
            if (criticalText != null) StartCoroutine(diceUI.CriticalTextPopAnimation());
            yield return StartCoroutine(diceUI.SkippableWait(0.5f));
        }
        else if (skipDiceVisuals && Random.value < RunManager.instance.criticalChance)
        {
            payload.isCriticalHit = true;
        }

        long totalDamage = payload.GetFinalDamage();
        if (!skipDiceVisuals)
        {
            UpdateTotalDamageDisplay(totalDamage);
            if (speedDiceMode)
                yield return new WaitForSeconds(0.5f);
            else
                yield return StartCoroutine(diceUI.SkippableWait(0.6f));
        }
        diceUI.EndDiceAnim();

        // Placeholder'ı kaldır ve patlama efektini çal
        if (placeholderObj != null) Destroy(placeholderObj);
        StartCoroutine(AnimateExplosionFX(groundMap.GetCellCenterWorld(cell)));

        HideDiceResults();

        // Patlama alanı: bombadaki hex + 1 mesafe komşular
        Vector3Int[] offsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
        List<Vector3Int> blastCells = new List<Vector3Int> { cell };
        foreach (var off in offsets) blastCells.Add(cell + off);

        // Her düşmana aynı zarı ver (paylaştırma yok)
        List<EnemyMovement> hitEnemies = new List<EnemyMovement>();
        foreach (var bc in blastCells) { EnemyMovement enemy = GetEnemyAtCell(bc); if (enemy != null && !hitEnemies.Contains(enemy)) hitEnemies.Add(enemy); }

        foreach (var enemy in hitEnemies)
        {
            enemy.health.TakeDamage(totalDamage);
            ApplyBurnIfActive(enemy);
            ApplyCystIfActive(enemy);

            SpawnSlashEffect(enemy.transform.position);

            if (enemy.health.currentHP <= 0)
                coinService.ProcessKillRewards(enemy);
        }

        UpdateCoinUI();
        if (!CleanupDeadAndCheckLevelClear())
            ShowAllEnemyIntents();
    }

    public void StartPhaseShiftTargeting() { isPhaseShiftTargeting = true; }
    public void TryPhaseShift(EnemyMovement target)
    {
        if (!isPhaseShiftTargeting || target == null) return;
        isPhaseShiftTargeting = false;
        ClearTargetingCache();
        StartCoroutine(PhaseShiftCoroutine(target));
    }

    private IEnumerator PhaseShiftCoroutine(EnemyMovement target)
    {
        Vector3Int playerCell = player.GetCurrentCellPosition();
        Vector3Int enemyCell = target.GetCurrentCellPosition();

        // Oyuncu küçülür
        float shrinkDur = 0.12f; float elapsed = 0f;
        while (elapsed < shrinkDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDur;
            player.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            yield return null;
        }
        player.transform.localScale = Vector3.zero;

        // Konumları değiştir
        player.ForceSetPosition(enemyCell);
        target.ForceSetPosition(playerCell);

        // Oyuncu büyür
        elapsed = 0f;
        while (elapsed < shrinkDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shrinkDur;
            player.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }
        player.transform.localScale = Vector3.one;

        // Swap sonrası mayın kontrolü: düşman mayının üstüne geldiyse hemen patlat
        if (activeMineCell.y != -999 && target.GetCurrentCellPosition() == activeMineCell)
        {
            yield return new WaitForSeconds(0.1f);
            var phantomPerk = RunManager.instance.activePerks.Find(p => p is PhantomLimbPerk);
            float mineDamagePercent = phantomPerk != null ? phantomPerk.currentLevel * 0.25f : 0.25f;
            TriggerExplosion(activeMineCell, mineDamagePercent);

            if (target != null && target.health.currentHP > 0)
                target.ApplyStun(2, true);

            if (activeMineObj != null) Destroy(activeMineObj);
            activeMineCell = new Vector3Int(-999, -999, -999);

            List<EnemyMovement> mineKills = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
            foreach (var deadEnemy in mineKills)
                coinService.ProcessKillRewards(deadEnemy);
            UpdateCoinUI();
            if (CleanupDeadAndCheckLevelClear()) yield break;
        }

        // Teleport sonrası: saldırı varsa yap, sonra düşman fazına geç
        isPlayerTurn = false;
        if (RunManager.instance != null) RunManager.instance.remainingMoves = 0;
        player.ClearHighlights();

        // Bitişik düşmanlara otomatik saldırı
        List<EnemyMovement> adjacentEnemies = GetAdjacentEnemies(player.GetCurrentCellPosition());
        if (adjacentEnemies.Count > 0 && !hasAttackedThisTurn)
        {
            hasAttackedThisTurn = true;
            isAttackAnimationPlaying = true;
            yield return StartCoroutine(MultiAttack(adjacentEnemies));
        }

        if (!CleanupDeadAndCheckLevelClear())
        {
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(EnemyPhase());
        }
    }

    public void StartThornPlacement()
    {
        isThornPlacementTargeting = true;
        CreateThornPreview();
    }

    private void CreateThornPreview()
    {
        if (thornPreviewObj != null) return;

        Sprite previewSprite = null;
        var _ts = LevelGenerator.instance?.GetActiveTileSet();
        if (_ts != null && _ts.hazardTile is Tile ht && ht.sprite != null)
            previewSprite = ht.sprite;

        if (previewSprite == null) return;

        thornPreviewObj = new GameObject("ThornPreview");
        var sr = thornPreviewObj.AddComponent<SpriteRenderer>();
        sr.sprite = previewSprite;
        sr.color = new Color(1f, 1f, 1f, 0.45f);
        sr.sortingOrder = 10;
        thornPreviewObj.SetActive(false);
    }

    private void DestroyThornPreview()
    {
        if (thornPreviewObj != null) { Destroy(thornPreviewObj); thornPreviewObj = null; }
    }

    private bool IsThornCellBlocked(Vector3Int cell)
    {
        if (player != null && player.GetCurrentCellPosition() == cell) return true;
        if (IsEnemyAtCell(cell)) return true;
        if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(cell)) return true;

        // Bu hücreye diken koymak düşmanları sıkıştırır mı kontrol et
        if (LevelGenerator.instance != null && WouldBlockEnemyPaths(cell)) return true;

        return false;
    }

    /// <summary>
    /// Bir hücreye diken koymak düşmanların yolunu tamamen kapatır mı kontrol eder.
    /// </summary>
    private bool WouldBlockEnemyPaths(Vector3Int thornCell)
    {
        if (LevelGenerator.instance == null || player == null) return false;

        // Geçici olarak hazard ekle
        LevelGenerator.instance.hazardCells.Add(thornCell);

        Vector3Int playerCell = player.GetCurrentCellPosition();
        bool anyBlocked = false;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.health.currentHP <= 0) continue;
            Vector3Int enemyCell = enemy.GetCurrentCellPosition();
            if (enemyCell == thornCell) continue;

            // BFS — düşman oyuncuya ulaşabilir mi?
            if (!CanReachAvoidingHazards(enemyCell, playerCell))
            {
                anyBlocked = true;
                break;
            }
        }

        // Geçici hazard'ı geri al
        LevelGenerator.instance.hazardCells.Remove(thornCell);
        return anyBlocked;
    }

    /// <summary>BFS ile hazard'ları atlayarak hedefe ulaşılabilir mi kontrol eder.</summary>
    private bool CanReachAvoidingHazards(Vector3Int from, Vector3Int to)
    {
        if (from == to) return true;
        var lg = LevelGenerator.instance;
        if (lg == null) return true;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(from);
        visited.Add(from);

        while (queue.Count > 0)
        {
            Vector3Int curr = queue.Dequeue();
            Vector3Int[] offsets = (curr.y % 2 != 0) ? EnemyMovement.evenOffsets : EnemyMovement.oddOffsets;

            foreach (var off in offsets)
            {
                Vector3Int neighbor = curr + off;
                if (neighbor == to) return true;
                if (visited.Contains(neighbor)) continue;
                if (!lg.groundMap.HasTile(neighbor) && !(ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(neighbor))) continue;
                if (lg.hazardCells.Contains(neighbor)) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
        return false;
    }

    private void TickThornLifetimes()
    {
        if (LevelGenerator.instance == null) return;
        List<Vector3Int> expired = new List<Vector3Int>();
        List<Vector3Int> blinkCells = new List<Vector3Int>();
        List<Vector3Int> keys = new List<Vector3Int>(thornTurnsRemaining.Keys);

        foreach (var key in keys)
        {
            int remaining = thornTurnsRemaining[key] - 1;
            thornTurnsRemaining[key] = remaining;

            if (remaining <= 0)
                expired.Add(key);
            else if (remaining == 1)
                blinkCells.Add(key);
        }

        // 2. turda (1 tur kaldı) blink efekti
        foreach (var cell in blinkCells)
        {
            if (LevelGenerator.instance.foreGroundA != null && LevelGenerator.instance.foreGroundA.HasTile(cell))
                StartCoroutine(BlinkThornTile(cell));
        }

        // 3. tur doldu → kaldır
        foreach (var cell in expired)
        {
            thornTurnsRemaining.Remove(cell);
            LevelGenerator.instance.hazardCells.Remove(cell);
            if (LevelGenerator.instance.foreGroundA != null)
            {
                LevelGenerator.instance.foreGroundA.SetTile(cell, null);
            }
        }
    }

    private IEnumerator BlinkThornTile(Vector3Int cell)
    {
        var map = LevelGenerator.instance != null ? LevelGenerator.instance.foreGroundA : null;
        if (map == null) yield break;
        // LockColor flag'ini kaldır yoksa SetColor çalışmaz
        if (map.HasTile(cell)) map.SetTileFlags(cell, TileFlags.None);
        // Son tur boyunca sürekli yanıp sön, tile kaldırılana kadar
        while (map.HasTile(cell) && thornTurnsRemaining.ContainsKey(cell))
        {
            map.SetColor(cell, new Color(1f, 1f, 1f, 0.25f));
            yield return new WaitForSeconds(0.2f);
            if (!map.HasTile(cell)) break;
            map.SetColor(cell, Color.white);
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void ExecuteThornAt(Vector3Int cell)
    {
        if (LevelGenerator.instance == null) { isThornPlacementTargeting = false; DestroyThornPreview(); ClearTargetingCache(); return; }
        if (IsThornCellBlocked(cell)) return;
        isThornPlacementTargeting = false;
        DestroyThornPreview();
        ClearTargetingCache();
        LevelGenerator.instance.hazardCells.Add(cell);
        var thornTs = LevelGenerator.instance?.GetActiveTileSet();
        if (LevelGenerator.instance.foreGroundA != null && thornTs?.hazardTile != null) LevelGenerator.instance.foreGroundA.SetTile(cell, thornTs.hazardTile);
        thornTurnsRemaining[cell] = 3;
        if (player != null) player.UpdateHighlights();
    }

    public void LockAllEnemyIntents()
    {
        if (player == null || player.health.currentHP <= 0) return;
        Vector3Int pCell = player.GetCurrentCellPosition();
        foreach (var e in enemies) if (e != null && !e.isAllied) { bool isStunned = e.skipTurns > 0; e.LockNextMove(pCell, isStunned); }
    }

    private IEnumerator LockIntentsNextFrame()
    {
        yield return null;
        LockAllEnemyIntents();
        ShowAllEnemyIntents();
    }

    public void PlayerFinishedMove(Vector3Int playerCell)
    {
        StartCoroutine(HandlePlayerPhase(playerCell));
    }

    public void SkipTurn()
    {
        if (!isPlayerTurn || IsAnyTargetingActive || isAttackAnimationPlaying) return;
        if (diceUI != null && diceUI.IsDiceAnimPlaying) return;
        isPlayerTurn = false;
        if (RunManager.instance != null) RunManager.instance.remainingMoves = 0;

        if (player != null)
        {
            player.ClearHighlights();

            if (RunManager.instance != null && RunManager.instance.activePerks.Exists(p => p is PhantomLimbPerk))
            {
                var perk = RunManager.instance.activePerks.Find(p => p is PhantomLimbPerk);
                Vector3 mineOffset = (perk as PhantomLimbPerk)?.GetMineOffset() ?? Vector3.zero;
                TryDropMine(player.GetCurrentCellPosition(), mineOffset);

                if (perk != null) perk.TriggerVisualPop();
            }
        }

        StartCoroutine(HandleSkipPhase());
    }

    private IEnumerator HandleSkipPhase()
    {
        // OnSkip'i saldırıdan ÖNCE çağır: DormantSpore zarları bu turda kullanılabilsin
        foreach (var perk in RunManager.instance.activePerks) perk.OnSkip();

        // Collection: skip sayacı
        int totalSkips = PlayerPrefs.GetInt("total_skips", 0) + 1;
        PlayerPrefs.SetInt("total_skips", totalSkips);
        GameEvents.SkipTurnPerformed(totalSkips);
        RunManager.instance.currentGold += RunManager.instance.skipBonusGold;

        UpdateCoinUI();

        bool isMitsuri = RunManager.instance != null && RunManager.instance.selectedWeapon == WeaponType.MitsuriBlade;

        // 1) Mevcut konumdaki bitişik düşmanlara saldır
        List<EnemyMovement> adjacentEnemies = GetAdjacentEnemies(player.GetCurrentCellPosition());
        if (!isMitsuri && adjacentEnemies.Count > 0 && !hasAttackedThisTurn)
        {
            RunManager.instance.remainingMoves = 0;
            hasAttackedThisTurn = true; isAttackAnimationPlaying = true;
            yield return StartCoroutine(MultiAttack(adjacentEnemies));
        }

        // 2) Synaptic Anchor teleport bekliyor mu?
        var anchorPerk = RunManager.instance.activePerks.Find(p => p is SynapticAnchorPerk) as SynapticAnchorPerk;
        if (anchorPerk != null && anchorPerk.teleportPending)
        {
            yield return StartCoroutine(anchorPerk.ExecuteTeleport());

            if (CleanupDeadAndCheckLevelClear()) yield break;

            // 3) Teleport sonrası yeni konumdaki bitişik düşmanlara da saldır
            List<EnemyMovement> newAdjacentEnemies = GetAdjacentEnemies(player.GetCurrentCellPosition());
            if (!isMitsuri && newAdjacentEnemies.Count > 0)
            {
                isAttackAnimationPlaying = true;
                yield return StartCoroutine(MultiAttack(newAdjacentEnemies));
            }
        }

        // Phantom Assault: hayaletlere sırayla ışınlan, her birinde etraftaki düşmanlara saldır
        if (!CleanupDeadAndCheckLevelClear())
        {
            var phantomAssaultPerk = RunManager.instance.activePerks.Find(p => p is PhantomAssaultPerk) as PhantomAssaultPerk;
            if (phantomAssaultPerk != null && phantomAssaultPerk.HasGhosts())
            {
                List<Vector3Int> allGhosts = phantomAssaultPerk.GetAllGhostCells();
                foreach (var ghostCell in allGhosts)
                {
                    // Teleport player to this ghost cell
                    yield return StartCoroutine(phantomAssaultPerk.TeleportPlayerToCell(ghostCell));

                    // Attack adjacent enemies at ghost position
                    List<EnemyMovement> ghostTargets = GetAdjacentEnemies(ghostCell);
                    if (!isMitsuri && ghostTargets.Count > 0)
                    {
                        isAttackAnimationPlaying = true;
                        yield return StartCoroutine(MultiAttack(ghostTargets));
                    }
                    if (CleanupDeadAndCheckLevelClear()) break;
                }
                phantomAssaultPerk.ConsumeGhosts();
            }
        }

        // Viral Cysts: marklı düşmanlara zar atarak saldır
        if (!CleanupDeadAndCheckLevelClear())
        {
            var viralPerk = RunManager.instance.activePerks.Find(p => p is ViralCystsPerk) as ViralCystsPerk;
            if (viralPerk != null)
            {
                int markCount = viralPerk.GetMarkedCount();
                if (markCount > 0)
                {
                    List<EnemyMovement> cystTargets = viralPerk.ConsumeMarkedTargets();
                    if (cystTargets.Count > 0)
                    {
                        isAttackAnimationPlaying = true;
                        yield return StartCoroutine(MultiAttack(cystTargets, markCount));
                    }
                }
            }
        }

        if (CleanupDeadAndCheckLevelClear()) yield break;
        if (enemies.Count <= 0) yield break;
        StartCoroutine(EnemyPhase());
    }

    public void TriggerExplosion(Vector3Int centerCell, float damagePercent = 0.5f, bool includeCenter = true)
    {
        Vector3 spawnPos = groundMap.GetCellCenterWorld(centerCell);
        spawnPos.z = 0;
        StartCoroutine(AnimateExplosionFX(spawnPos));

        Vector3Int[] offsets = (centerCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        List<Vector3Int> cellsToHit = includeCenter ? new List<Vector3Int> { centerCell } : new List<Vector3Int>();
        foreach (var off in offsets) cellsToHit.Add(centerCell + off);

        HashSet<EnemyMovement> enemiesToHit = new HashSet<EnemyMovement>();
        foreach (var cell in cellsToHit)
        {
            EnemyMovement e = GetEnemyAtCell(cell);
            if (e != null && e.health.currentHP > 0) enemiesToHit.Add(e);
        }

        foreach (var e in enemiesToHit)
        {
            if (e == null) continue;
            long explosionDamage = System.Math.Max(1L, (long)(e.health.maxHP * damagePercent));
            e.health.TakeDamage(explosionDamage);

            // Patlama sonrası alpha'yı hemen reset et (stun alpha kalmasın)
            // Neighborler isDeepStunnedAlpha false olduğundan SetStunnedAlpha(false) çalışmayabilir
            // Bu yüzden sprite rengini direkt ayarla
            if (e.health.currentHP > 0)
            {
                SpriteRenderer sr = e.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }

        enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);
    }

    // ──────── Layer 2: Explosion Tile Trigger ────────

    /// <summary>
    /// Explosion tile tetiklendiğinde çağrılır.
    /// Merkez + 6 komşuya hasar verir. Tile yok olmaz (kalıcı tehlike).
    /// </summary>
    private IEnumerator TriggerExplosionTileCoroutine(Vector3Int cell)
    {
        if (isCollapsingIslands) yield break;
        if (LevelGenerator.instance == null || !LevelGenerator.instance.explosionCells.Contains(cell)) yield break;

        TrapTileEvents.FireExplosionTileTriggered(cell);

        Vector3 spawnPos = groundMap.GetCellCenterWorld(cell);
        spawnPos.z = 0;
        StartCoroutine(AnimateExplosionFX(spawnPos));

        yield return new WaitForSeconds(0.1f);

        Vector3Int[] offsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;

        // Merkez + 6 komşu — tüm entity'ler hasar alır
        List<Vector3Int> cellsToHit = new List<Vector3Int> { cell };
        foreach (var off in offsets) cellsToHit.Add(cell + off);

        // Düşman hasarı
        HashSet<EnemyMovement> enemiesToHit = new HashSet<EnemyMovement>();
        foreach (var hitCell in cellsToHit)
        {
            EnemyMovement e = GetEnemyAtCell(hitCell);
            if (e != null && e.health.currentHP > 0) enemiesToHit.Add(e);
        }
        foreach (var e in enemiesToHit)
        {
            if (e == null) continue;
            e.health.TakeDamage(System.Math.Max(1L, e.health.maxHP / 2));
        }

        // Oyuncu hasarı (merkez veya komşuda ise)
        if (player != null && player.health.currentHP > 0 && cellsToHit.Contains(player.GetCurrentCellPosition()))
        {
            if (RunManager.instance.hasBioBarrier)
                TryBreakBioBarrier();
            else
                PlayerTakeDamage(1);
        }

        yield return new WaitForSeconds(0.2f);

        List<EnemyMovement> killed = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
        foreach (var dead in killed) coinService.ProcessKillRewards(dead);
        UpdateCoinUI();
    }

    // ──────── Layer 2: Teleport Tile Trigger ────────

    // Double-fire koruması: aynı turda aynı tile iki kez tetiklenmesin
    private HashSet<Vector3Int> teleportFiredThisTurn = new HashSet<Vector3Int>();

    /// <summary>
    /// Oyuncu bir teleport tile'ına bastığında çağrılır.
    /// </summary>
    private IEnumerator TriggerPlayerTeleportTile(Vector3Int fromCell)
    {
        if (LevelGenerator.instance == null) yield break;
        if (!LevelGenerator.instance.teleportCells.Contains(fromCell)) yield break;
        if (teleportFiredThisTurn.Contains(fromCell)) yield break;
        if (!LevelGenerator.instance.teleportPairs.TryGetValue(fromCell, out Vector3Int destCell)) yield break;

        teleportFiredThisTurn.Add(fromCell);

        // Hedef dolu mu?
        EnemyMovement swapEnemy = GetEnemyAtCell(destCell);

        // ScaffoldManager bildirimi (ayrılma)
        if (ScaffoldManager.instance != null) ScaffoldManager.instance.OnEntityLeave(fromCell);

        yield return StartCoroutine(TeleportAnimHelper.TeleportEntity(
            this,
            player.gameObject,
            () =>
            {
                player.ForceSetPosition(destCell);
                if (swapEnemy != null) swapEnemy.ForceSetPosition(fromCell);
            }
        ));

        // ScaffoldManager bildirimi (varış)
        if (ScaffoldManager.instance != null) ScaffoldManager.instance.OnEntityEnter(destCell);

        CameraController.ShakeLight();
        TrapTileEvents.FireTeleportTileTriggered(fromCell, destCell);
    }

    /// <summary>
    /// Bir düşman teleport tile'ına bastığında çağrılır.
    /// </summary>
    private IEnumerator TriggerEnemyTeleportTile(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health.currentHP <= 0) yield break;
        if (LevelGenerator.instance == null) yield break;

        Vector3Int fromCell = enemy.GetCurrentCellPosition();
        if (!LevelGenerator.instance.teleportCells.Contains(fromCell)) yield break;
        if (teleportFiredThisTurn.Contains(fromCell)) yield break;
        if (!LevelGenerator.instance.teleportPairs.TryGetValue(fromCell, out Vector3Int destCell)) yield break;

        teleportFiredThisTurn.Add(fromCell);

        // Hedef dolu mu? Oyuncu varsa güvenli komşuya gönder
        bool destHasPlayer = player != null && player.GetCurrentCellPosition() == destCell;
        EnemyMovement swapEnemy = destHasPlayer ? null : GetEnemyAtCell(destCell);

        Vector3Int actualDest = destHasPlayer ? GetSafeNeighborForEnemy(destCell) : destCell;

        if (ScaffoldManager.instance != null) ScaffoldManager.instance.OnEntityLeave(fromCell);

        yield return StartCoroutine(TeleportAnimHelper.TeleportEntity(
            this,
            enemy.gameObject,
            () =>
            {
                enemy.ForceSetPosition(actualDest);
                if (swapEnemy != null) swapEnemy.ForceSetPosition(fromCell);
            }
        ));

        if (ScaffoldManager.instance != null) ScaffoldManager.instance.OnEntityEnter(actualDest);
        TrapTileEvents.FireTeleportTileTriggered(fromCell, actualDest);
    }

    private IEnumerator AnimateExplosionFX(Vector3 pos)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayExplosion();
        if (explosionPrefab == null) yield break;
        GameObject fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
        SpriteRenderer[] renderers = fx.GetComponentsInChildren<SpriteRenderer>();
        float duration = 0.15f; float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; float t = elapsed / duration;
            fx.transform.localScale = Vector3.Lerp(Vector3.one * 1.0f, Vector3.one * 5.2f, t);
            foreach (var sr in renderers) { Color c = sr.color; c.a = Mathf.Lerp(0.8f, 0f, t); sr.color = c; }
            yield return null;
        }
        Destroy(fx);
    }

    private IEnumerator VacuumVFXCoroutine(Vector3 pos)
    {
        if (vacuumVfxPrefab == null) yield break;
        if (AudioManager.instance != null) AudioManager.instance.PlayVacuum();
        GameObject vfx = Instantiate(vacuumVfxPrefab, pos, Quaternion.identity);
        SpriteRenderer[] renderers = vfx.GetComponentsInChildren<SpriteRenderer>();
        float duration = 0.4f; float elapsed = 0f;
        Vector3 startScale = Vector3.one * 12f; Vector3 endScale = Vector3.one * 1.2f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; float t = elapsed / duration;
            float alpha = t < 0.2f ? Mathf.Lerp(0f, 0.4f, t / 0.2f) : Mathf.Lerp(0.4f, 0f, (t - 0.2f) / 0.8f);
            vfx.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            foreach (var sr in renderers) { if (sr != null) { Color c = sr.color; c.a = alpha; sr.color = c; } }
            yield return null;
        }
        Destroy(vfx);
    }

    private IEnumerator HandlePlayerPhase(Vector3Int playerCell)
    {
        hexesMovedThisTurn++;
        teleportFiredThisTurn.Clear();

        // Notify listeners (ShopDealer uses this to detect player stepping on its tile)
        GameEvents.PlayerMoved(playerCell);

        // Surge Boot: hareketten sonra kapat
        if (RunManager.instance != null)
            RunManager.instance.surgeBootActive = false;

        // Blue Magic Tile: 2+ hex hareket edildiyse tüket
        if (MagicTileManager.instance != null)
            MagicTileManager.instance.CheckBlueTileConsumption(playerCell);

        // Yellow Magic Tile: üstüne basınca +10 gold ve tüket (single-use)
        if (MagicTileManager.instance != null && MagicTileManager.instance.IsPlayerOnMagicTile(out MagicTileType yellowStep) && yellowStep == MagicTileType.Yellow)
        {
            RunManager.instance.currentGold += 10;
            GameEvents.GoldChanged(RunManager.instance.currentGold);
            UpdateCoinUI();
            MagicTileManager.instance.ConsumePlayerTile();
        }

        // Orange Magic Tile: üstünden ayrılınca tüket
        if (MagicTileManager.instance != null)
            MagicTileManager.instance.CheckOrangeTileConsumption(playerCell);

        // Orange Magic Tile: üstüne basınca +1 ekstra hareket hakkı (o an verilir)
        if (MagicTileManager.instance != null && MagicTileManager.instance.IsPlayerOnMagicTile(out MagicTileType orangeStep) && orangeStep == MagicTileType.Orange)
        {
            RunManager.instance.remainingMoves += 1;
            MagicTileManager.instance.MarkOrangeTileActive(playerCell);
        }

        // Layer 2: Explosion tile kontrolü (hazardCells içinde, bu yüzden önce kontrol et)
        if (LevelGenerator.instance != null && LevelGenerator.instance.explosionCells.Contains(playerCell))
        {
            yield return StartCoroutine(TriggerExplosionTileCoroutine(playerCell));
            if (CleanupDeadAndCheckLevelClear()) yield break;
            // Explosion tile oyuncuyu itmez — normal akışa devam et
        }

        // Normal spike hazard: sadece explosion tile DEĞİLSE knockback uygula
        bool isExplosionCell = LevelGenerator.instance != null && LevelGenerator.instance.explosionCells.Contains(playerCell);
        if (!isExplosionCell && LevelGenerator.instance.hazardCells.Contains(playerCell))
        {
            yield return new WaitForSeconds(0.15f);
            StartCoroutine(FlashHazardTileCoroutine(playerCell));
            if (RunManager.instance.hasBioBarrier)
                TryBreakBioBarrier();
            else PlayerTakeDamage(1);

            yield return new WaitForSeconds(0.15f);
            player.StartKnockbackMovement(GetSafeNeighbor(playerCell));
            yield return new WaitUntil(() => !player.IsMoving());

            if (RunManager.instance.remainingMoves > 0 && player != null && player.health.currentHP > 0)
            {
                RunManager.instance.remainingMoves--; isPlayerTurn = true; player.UpdateHighlights(); ShowAllEnemyIntents();
            }
            else if (player != null && player.health.currentHP > 0)
            {
                player.ClearHighlights(); yield return new WaitForSeconds(0.1f); StartCoroutine(EnemyPhase());
            }
            yield break;
        }

        // Layer 2: Teleport tile kontrolü
        if (LevelGenerator.instance != null && LevelGenerator.instance.teleportCells.Contains(playerCell))
        {
            yield return StartCoroutine(TriggerPlayerTeleportTile(playerCell));
            if (player != null && player.health.currentHP <= 0) yield break;
            // Işınlandıktan sonra yeni konumdaki adjacentEnemies ile devam et
        }

        List<EnemyMovement> adjacentEnemies = GetAdjacentEnemies(player.GetCurrentCellPosition());
        bool isMitsuri = RunManager.instance != null && RunManager.instance.selectedWeapon == WeaponType.MitsuriBlade;

        // Greatsword: bitişik düşmana otomatik saldır
        // MitsuriBlade: bitişik düşmana otomatik saldırma — oyuncu tıklayarak seçecek
        if (!isMitsuri && adjacentEnemies.Count > 0 && !hasAttackedThisTurn)
        {
            RunManager.instance.remainingMoves = 0;
            hasAttackedThisTurn = true; isAttackAnimationPlaying = true;
            yield return StartCoroutine(MultiAttack(adjacentEnemies));
        }
        else yield return new WaitForSeconds(0.05f);

        if (RunManager.instance.remainingMoves > 0 && player != null && player.health.currentHP > 0 && enemies.Count > 0)
        {
            // Hâlâ hareket hakkı var → devam et
            RunManager.instance.remainingMoves--; isPlayerTurn = true; player.UpdateHighlights(); ShowAllEnemyIntents();
            // MitsuriBlade: hareket sonrası mesafeler değişti, göstergeleri güncelle
            RefreshMitsuriRangeIndicators();
        }
        else if (player != null && player.health.currentHP > 0)
        {
            // Hareketler bitti → EnemyPhase (MitsuriBlade saldırısı Update'teki direct click ile yapılıyor)
            player.ClearHighlights(); yield return new WaitForSeconds(0.1f); StartCoroutine(EnemyPhase());
        }
    }

    private Vector3Int GetSafeNeighbor(Vector3Int centerCell)
    {
        Vector3Int[] offsets = (centerCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        foreach (var off in offsets)
        {
            Vector3Int n = centerCell + off;
            if (HasWalkableTile(n) && !IsEnemyAtCell(n) && !LevelGenerator.instance.hazardCells.Contains(n)) return n;
        }
        return centerCell;
    }

    /// <summary>Düşman için güvenli komşu hücre bul (oyuncu pozisyonunu da engellemez).</summary>
    public Vector3Int GetSafeNeighborForEnemy(Vector3Int centerCell)
    {
        Vector3Int[] offsets = (centerCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        foreach (var off in offsets)
        {
            Vector3Int n = centerCell + off;
            if (HasWalkableTile(n) && !IsEnemyAtCell(n) && !LevelGenerator.instance.hazardCells.Contains(n)
                && (player == null || player.GetCurrentCellPosition() != n)) return n;
        }
        // Fallback: herhangi bir yürünebilir komşu
        foreach (var off in offsets)
        {
            Vector3Int n = centerCell + off;
            if (HasWalkableTile(n) && !IsEnemyAtCell(n)) return n;
        }
        return centerCell;
    }

    private void TryBreakBioBarrier()
    {
        foreach (var perk in RunManager.instance.activePerks)
            if (perk is BioBarrierPerk aegis) { aegis.BreakShield(); break; }
        RunManager.instance.hasBioBarrier = false;
    }

    private void SpawnSlashEffect(Vector3 position)
    {
        if (slashEffectPrefab == null) return;
        position.y += slashEffectYOffset;
        GameObject slash = Instantiate(slashEffectPrefab, position, Quaternion.identity);
        slash.transform.localScale = new Vector3(-1, 1, 1);
    }

    private bool CleanupDeadAndCheckLevelClear()
    {
        // Ölü düşmanları listeden çıkarmadan önce, Die() çağrılmamış olanları temizle
        // (TotemDestroySequence gibi yerlerden currentHP=0 yapılıp Die() atlanabilir)
        foreach (var e in enemies)
        {
            if (e != null && e.health.currentHP <= 0 && !e.health.IsDead)
            {
                // FadeDie başlatarak görsel temizliği garanti et
                if (e.gameObject.activeInHierarchy)
                    StartCoroutine(e.FadeDieCoroutine());
            }
        }

        enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);

        // Neural Hijack: dost düşmanlar level clear'ı engellemez
        bool hasHostileEnemies = enemies.Exists(e => e != null && !e.isAllied);
        if (!hasHostileEnemies)
        {
            // Kalan dostları da öldür — level bitti
            foreach (var ally in new List<EnemyMovement>(enemies))
            {
                if (ally != null && ally.isAllied)
                {
                    ally.health.TakeDamageSilent(ally.health.currentHP);
                    if (ally.gameObject.activeInHierarchy)
                        StartCoroutine(ally.FadeDieCoroutine());
                }
            }
            enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);

            ClearWarningMap();
            StartCoroutine(WaitAndTriggerLevelClear());
            return true;
        }
        return false;
    }

    private IEnumerator EnemyPhase()
    {
        ClearMitsuriRangeIndicators();
        yield return new WaitForSeconds(0.2f);
        enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);

        // Burn tick düşmanlar hareket etmeden önce çalışır
        TickBurnsIfActive();
        if (CleanupDeadAndCheckLevelClear()) yield break;

        // Düşmanları speed değerine göre sırala (yüksek speed = önce hareket)
        enemies.Sort((a, b) => (b != null ? b.speed : 0).CompareTo(a != null ? a.speed : 0));

        teleportFiredThisTurn.Clear();
        foreach (var e in enemies) if (e != null && e.skipTurns <= 0 && !e.isAllied) e.ExecuteLockedMove();
        yield return new WaitUntil(() => { foreach (var e in enemies) if (e != null && e.IsMoving()) return false; return true; });
        yield return new WaitForSeconds(0.2f);

        // Layer 2: Explosion tile — düşman üzerinde patlayan tile var mı?
        if (LevelGenerator.instance != null && LevelGenerator.instance.explosionCells.Count > 0)
        {
            List<Vector3Int> explodedThisPhase = new List<Vector3Int>();
            List<EnemyMovement> explosionSnapshot = new List<EnemyMovement>(enemies);
            foreach (var e in explosionSnapshot)
            {
                if (e == null || e.health.currentHP <= 0) continue;
                Vector3Int eCell = e.GetCurrentCellPosition();
                if (LevelGenerator.instance.explosionCells.Contains(eCell) && !explodedThisPhase.Contains(eCell))
                {
                    explodedThisPhase.Add(eCell);
                    yield return StartCoroutine(TriggerExplosionTileCoroutine(eCell));
                    if (CleanupDeadAndCheckLevelClear()) yield break;
                }
            }
        }

        // Layer 2: Teleport tile — düşman teleport tile üzerinde mi?
        if (LevelGenerator.instance != null && LevelGenerator.instance.teleportCells.Count > 0)
        {
            List<EnemyMovement> enemySnapshot = new List<EnemyMovement>(enemies);
            foreach (var e in enemySnapshot)
            {
                if (e == null || e.health.currentHP <= 0) continue;
                Vector3Int eCell = e.GetCurrentCellPosition();
                if (LevelGenerator.instance.teleportCells.Contains(eCell) && !teleportFiredThisTurn.Contains(eCell))
                {
                    yield return StartCoroutine(TriggerEnemyTeleportTile(e));
                }
            }
        }

        // Phantom Assault: düşmanlar hayaletlerin üstüne yürürse o hayalet yok olur
        if (RunManager.instance != null)
        {
            var paPerk = RunManager.instance.activePerks.Find(p => p is PhantomAssaultPerk) as PhantomAssaultPerk;
            if (paPerk != null && paPerk.HasGhosts())
            {
                List<Vector3Int> ghostsToRemove = new List<Vector3Int>();
                foreach (var ghostCell in paPerk.GetAllGhostCells())
                {
                    if (IsEnemyAtCell(ghostCell)) ghostsToRemove.Add(ghostCell);
                }
                foreach (var cell in ghostsToRemove) paPerk.DestroyGhostAtCell(cell);
            }
        }

        // ========================================================
        // MAYIN KONTROLÜ
        // ========================================================
        if (activeMineCell.y != -999)
        {
            EnemyMovement victim = GetEnemyAtCell(activeMineCell);

            if (victim != null && victim.health.currentHP > 0)
            {
                var phantomPerk = RunManager.instance.activePerks.Find(p => p is PhantomLimbPerk);
                float mineDamagePercent = phantomPerk != null ? phantomPerk.currentLevel * 0.25f : 0.25f;
                TriggerExplosion(activeMineCell, mineDamagePercent);

                if (victim != null && victim.health.currentHP > 0)
                    victim.ApplyStun(2, true);

                if (activeMineObj != null) Destroy(activeMineObj);
                activeMineCell = new Vector3Int(-999, -999, -999);

                // Mayınla ölen düşmanlar için coin drop ve perk callback
                List<EnemyMovement> mineKills = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
                foreach (var deadEnemy in mineKills)
                    coinService.ProcessKillRewards(deadEnemy);

                UpdateCoinUI();
                if (CleanupDeadAndCheckLevelClear()) yield break;
                else
                {
                    var perk = RunManager.instance.activePerks.Find(p => p is PhantomLimbPerk);
                    if (perk != null) perk.TriggerVisualPop();
                }
            }
        }

        List<EnemyMovement> readyToBossAttack = new List<EnemyMovement>();
        List<EnemyMovement> readyToAoEAttack = new List<EnemyMovement>();
        List<EnemyMovement> readyToMeleeAttack = new List<EnemyMovement>();
        List<WarlockEnemyAI> readyWarlockAttack1 = new List<WarlockEnemyAI>();
        List<WarlockEnemyAI> readyWarlockAttack2 = new List<WarlockEnemyAI>();
        List<NinjaEnemyAI> readyNinjaStrike = new List<NinjaEnemyAI>();

        foreach (var e in enemies)
        {
            if (e != null && e.skipTurns <= 0)
            {
                if (e.IsBoss && SpawnerBossAI.instance != null && SpawnerBossAI.instance.readyToExplodeThisTurn) readyToBossAttack.Add(e);
                else if (e.IsBruiser && !e.isAllied && e.isChargingAttack) readyToAoEAttack.Add(e);
                else if (e.IsMelee && !e.isAllied && IsNeighbor(e.GetCurrentCellPosition(), player.GetCurrentCellPosition())) readyToMeleeAttack.Add(e);
                else if (e.IsWarlock && !e.isAllied)
                {
                    WarlockEnemyAI warlock = e.GetComponent<WarlockEnemyAI>();
                    if (warlock != null)
                    {
                        if (warlock.IsReadyToExplodeAttack1()) readyWarlockAttack1.Add(warlock);
                        if (warlock.IsReadyToExplodeAttack2()) readyWarlockAttack2.Add(warlock);
                    }
                }
                else if (e.IsNinja && !e.isAllied)
                {
                    NinjaEnemyAI ninja = e.GetComponent<NinjaEnemyAI>();
                    if (ninja != null && ninja.IsReadyToStrike()) readyNinjaStrike.Add(ninja);
                }
            }
        }

        // Warlock saldırı 1 (artı paterni) - PARALLEL
        if (readyWarlockAttack1.Count > 0)
        {
            List<Coroutine> attack1Coroutines = new List<Coroutine>();
            foreach (var warlock in readyWarlockAttack1) attack1Coroutines.Add(StartCoroutine(warlock.ExecuteAttack1()));
            foreach (var coroutine in attack1Coroutines) yield return coroutine;
            yield return new WaitForSeconds(0.2f);
        }

        // Warlock saldırı 2 (çapraz paterni) - PARALLEL
        if (readyWarlockAttack2.Count > 0)
        {
            List<Coroutine> attack2Coroutines = new List<Coroutine>();
            foreach (var warlock in readyWarlockAttack2) attack2Coroutines.Add(StartCoroutine(warlock.ExecuteAttack2()));
            foreach (var coroutine in attack2Coroutines) yield return coroutine;
            yield return new WaitForSeconds(0.2f);
        }

        // Ninja saldırıları (ışınlan + AoE) - PARALLEL
        if (readyNinjaStrike.Count > 0)
        {
            List<Coroutine> ninjaCoroutines = new List<Coroutine>();
            foreach (var ninja in readyNinjaStrike) ninjaCoroutines.Add(StartCoroutine(ninja.ExecuteStrike()));
            foreach (var c in ninjaCoroutines) yield return c;
            yield return new WaitForSeconds(0.2f);
        }

        // Melee saldırılar
        if (readyToMeleeAttack.Count > 0)
        {
            yield return StartCoroutine(EnemyAttackCoroutine(readyToMeleeAttack));
        }

        // Boss saldırısı (melee sonra)
        if (readyToBossAttack.Count > 0)
        {
            foreach (var boss in readyToBossAttack) yield return StartCoroutine(SpawnerBossAI.instance.ExecuteCheckerboardAoE());
            yield return new WaitForSeconds(0.2f);
        }

        // AoE saldırılar EN SON - PARALLEL
        if (readyToAoEAttack.Count > 0)
        {
            List<Coroutine> aoeCoroutines = new List<Coroutine>();
            foreach (var aoeEnemy in readyToAoEAttack) { var bruiser = aoeEnemy.GetComponent<BruiserEnemyAI>(); if (bruiser != null) aoeCoroutines.Add(StartCoroutine(bruiser.ExecuteAoEAttackCoroutine(player))); }
            foreach (var coroutine in aoeCoroutines) yield return coroutine;
            yield return new WaitForSeconds(0.2f);
        }

        // Emniyet: Eğer readyToExplodeThisTurn bu turda execute edilmedi ise, bir sonraki turda sorun olmasın diye sıfırla
        if (readyToBossAttack.Count == 0 && SpawnerBossAI.instance != null && SpawnerBossAI.instance.readyToExplodeThisTurn)
        {
            SpawnerBossAI.instance.readyToExplodeThisTurn = false;
        }

        // Emniyet: Warlock ready flağlarını sıfırla (eğer bu turda execute edilmediyse)
        if (readyWarlockAttack1.Count == 0 && readyWarlockAttack2.Count == 0)
        {
            foreach (var e in enemies)
            {
                if (e != null && e.IsWarlock)
                {
                    WarlockEnemyAI warlock = e.GetComponent<WarlockEnemyAI>();
                    if (warlock != null)
                    {
                        warlock.ClearAttackFlags();
                    }
                }
            }
        }

        // Emniyet: EnemyPhase sonu — burn veya diğer efektlerden ölen düşmanları temizle
        if (CleanupDeadAndCheckLevelClear()) yield break;

        EndTurnAndDecreaseStuns();
    }

    public void HideAllEnemyIntents() { foreach (var e in enemies) if (e != null) e.SetArrowVisibility(false); }
    public void ShowAllEnemyIntents() { foreach (var e in enemies) if (e != null && e.skipTurns <= 0 && !e.isAllied) e.SetArrowVisibility(true); }

    public void ToggleFastMode()
    {
        if (RunManager.instance != null)
            RunManager.instance.fastMode = !RunManager.instance.fastMode;
    }
    public bool GetFastMode() => RunManager.instance != null && RunManager.instance.fastMode;

    public bool IsDiceAnimPlaying => diceUI != null && diceUI.IsDiceAnimPlaying;

    public void ToggleSkipDiceVisuals()
    {
        manualDiceSkip = !manualDiceSkip;
        skipDiceVisuals = manualDiceSkip || (RunManager.instance != null && RunManager.instance.fastMode);

        if (skipDiceVisuals && diceUI != null)
        {
            // Animasyon devam ediyorsa hemen atla
            diceUI.skipDiceAnim = true;
            // Ekranda kalan zarları temizle
            HideDiceResults();
        }
    }
    public bool GetSkipDiceVisuals() => manualDiceSkip;

    public void ToggleSpeedDice()
    {
        speedDiceMode = !speedDiceMode;
        if (speedDiceMode && diceUI != null && diceUI.IsDiceAnimPlaying)
            diceUI.skipDiceAnim = true;
    }
    public bool GetSpeedDice() => speedDiceMode;

    private IEnumerator MultiAttack(List<EnemyMovement> targets, int bonusDice = 0)
    {

        bool hasBioMag = RunManager.instance.activePerks.Exists(p => p is BioMagnetismPerk);
        if (hasBioMag)
        {
            Vector3Int pCell = player.GetCurrentCellPosition();
            List<EnemyMovement> pullTargets = new List<EnemyMovement>();
            foreach (var e in enemies)
            {
                if (e != null && e.health.currentHP > 0 && DistanceCube(e.GetCurrentCellPosition(), pCell) == 2f) pullTargets.Add(e);
            }
            bool anyonePulled = false;
            if (pullTargets.Count > 0 && vacuumVfxPrefab != null) StartCoroutine(VacuumVFXCoroutine(player.transform.position));

            foreach (var e in pullTargets)
            {
                Vector3Int eCell = e.GetCurrentCellPosition();
                Vector3Int bestPullCell = eCell;
                Vector3Int[] pOffsets = (pCell.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in pOffsets)
                {
                    Vector3Int neighborToPlayer = pCell + off;
                    if (IsNeighbor(neighborToPlayer, eCell))
                    {
                        if (HasWalkableTile(neighborToPlayer) && !IsEnemyAtCell(neighborToPlayer) && (LevelGenerator.instance == null || !LevelGenerator.instance.hazardCells.Contains(neighborToPlayer)))
                        {
                            bestPullCell = neighborToPlayer; break;
                        }
                    }
                }
                if (bestPullCell != eCell) { e.StartKnockbackMovement(bestPullCell); anyonePulled = true; }
            }
            if (anyonePulled)
            {
                yield return new WaitUntil(() => pullTargets.All(e => e == null || !e.IsMoving()));
                yield return new WaitForSeconds(0.1f);

                // Mayın kontrolü: çekilen düşman mayının üstüne geldiyse hemen patlat
                if (activeMineCell.y != -999)
                {
                    EnemyMovement mineVictim = GetEnemyAtCell(activeMineCell);
                    if (mineVictim != null && mineVictim.health.currentHP > 0)
                    {
                        var phantomPerk = RunManager.instance.activePerks.Find(p => p is PhantomLimbPerk);
                        float mineDmgPct = phantomPerk != null ? phantomPerk.currentLevel * 0.25f : 0.25f;
                        TriggerExplosion(activeMineCell, mineDmgPct);

                        if (mineVictim != null && mineVictim.health.currentHP > 0)
                            mineVictim.ApplyStun(2, true);

                        if (activeMineObj != null) Destroy(activeMineObj);
                        activeMineCell = new Vector3Int(-999, -999, -999);

                        List<EnemyMovement> mineKills = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
                        foreach (var deadEnemy in mineKills)
                            coinService.ProcessKillRewards(deadEnemy);
                        UpdateCoinUI();
                        if (CleanupDeadAndCheckLevelClear()) yield break;
                    }
                }

                targets = GetAdjacentEnemies(pCell);
                var perk = RunManager.instance.activePerks.Find(p => p is BioMagnetismPerk);
                if (perk != null) perk.TriggerVisualPop();
            }
        }

        if (!skipDiceVisuals) yield return new WaitForSeconds(0.3f);
        List<int> currentRolls = new List<int>();
        int diceCount = RunManager.instance != null ? RunManager.instance.baseDiceCount : 2;

        // MitsuriBlade: 3+ hex mesafedeki düşmana saldırırken zar cezası
        if (RunManager.instance != null && RunManager.instance.selectedWeapon == WeaponType.MitsuriBlade && targets.Count > 0)
        {
            Vector3Int pCell = player.GetCurrentCellPosition();
            // En uzak hedefin mesafesine bak (tek hedef olacak genelde ama güvenlik için)
            float maxDist = 0f;
            foreach (var t in targets)
            {
                if (t == null) continue;
                float d = HexGridUtils.DistanceCube(pCell, t.GetCurrentCellPosition());
                if (d > maxDist) maxDist = d;
            }
            if (maxDist >= 3f) diceCount = 1;
        }

        int extraDices = bonusDice;
        foreach (var p in RunManager.instance.activePerks) if (p is DormantSporePerk ambushPerk) { extraDices += ambushPerk.ConsumeStoredDice(); }
        if (RunManager.instance.bonusDiceNextCombat > 0) { extraDices += RunManager.instance.bonusDiceNextCombat; RunManager.instance.bonusDiceNextCombat = 0; }
        // Host Syndrome: +1 die per adjacent enemy
        foreach (var p in RunManager.instance.activePerks) if (p is HostSyndromePerk hostPerk) { extraDices += hostPerk.GetExtraDice(); }
        // Dice Hoarder: +1 die per visited rest (active + stash kopyaları toplanır)
        foreach (var p in RunManager.instance.activePerks) if (p is DiceHoarderPerk hoardPerk) { extraDices += hoardPerk.GetExtraDice(); }
        foreach (var p in RunManager.instance.inventoryPerks) if (p is DiceHoarderPerk hoardPerk) { extraDices += hoardPerk.GetExtraDice(); }
        // Green Magic Tile: +1 die while standing on it (single-use)
        if (MagicTileManager.instance != null && MagicTileManager.instance.IsPlayerOnMagicTile(out MagicTileType magicType) && magicType == MagicTileType.Green)
        {
            extraDices += 1;
            MagicTileManager.instance.ConsumePlayerTile();
        }
        // Condensed Fury: roll 1 fewer die (minimum 1 die always)
        int diceReduction = 0;
        foreach (var p in RunManager.instance.activePerks) if (p is CondensedFuryPerk cfPerk) { diceReduction += cfPerk.GetDiceReduction(); }
        int totalDice = Mathf.Max(1, diceCount + extraDices - diceReduction);
        for (int i = 0; i < totalDice; i++) currentRolls.Add(Random.Range(1, 7));
        // Reroll stack: her zara kalıcı bonus ekle (AMA SADECE PERK VARSA)
        if (RunManager.instance != null && RunManager.instance.shopRerollStack > 0)
        {
            bool hasGeneticCartel = RunManager.instance.activePerks.Exists(p => p is ShopRerollStackPerk);

            if (hasGeneticCartel)
            {
                for (int i = 0; i < currentRolls.Count; i++)
                    currentRolls[i] += RunManager.instance.shopRerollStack;
            }
        }
        if (RunManager.instance != null) RunManager.instance.totalDiceRolled += totalDice;

        // Volatile Roll: zarları baştan 1/6 yap
        var volatilePerk = RunManager.instance != null
            ? RunManager.instance.activePerks.Find(p => p is VolatileRollPerk) as VolatileRollPerk
            : null;
        if (volatilePerk != null)
            volatilePerk.ApplyToBaseRolls(currentRolls);

        CombatPayload payload = new CombatPayload(currentRolls);
        if (RunManager.instance != null && RunManager.instance.activePerks.Exists(p => p.GetType().Name == "SymbioticFuryPerk")) payload.multiplyInsteadOfAdd = true;
        if (!skipDiceVisuals && PerkListUI.instance != null) PerkListUI.instance.ForceOpen();
        diceUI.BeginDiceAnim();
        if (!skipDiceVisuals)
        {
            yield return StartCoroutine(diceUI.ShowDiceSequence(currentRolls));
            UpdateTotalDamageDisplay(payload.GetFinalDamage());

            // Volatile Roll: 6 gelince zincirleme extra zarlar (animasyonlu)
            if (volatilePerk != null)
            {
                yield return StartCoroutine(VolatileRollChainAnimation(volatilePerk, currentRolls, payload));
            }
            else
            {
                yield return StartCoroutine(diceUI.SkippableWait(0.5f));
            }
        }
        else if (volatilePerk != null)
        {
            // skipDiceVisuals: extra zarları sessizce ekle
            int chainStart = 0;
            int chainCount = 0;
            while (chainCount < 50)
            {
                int prevCount = currentRolls.Count;
                var extras = volatilePerk.GenerateChainRolls(currentRolls, chainStart);
                if (extras.Count == 0) break;
                currentRolls.AddRange(extras);
                payload.diceRolls.AddRange(extras);
                chainStart = prevCount;
                chainCount++;
            }
        }

        yield return StartCoroutine(perkProcessor.ProcessPerks(payload, currentRolls));
        yield return StartCoroutine(perkProcessor.ProcessLetsGoAgainPass(payload, currentRolls));

        if (!skipDiceVisuals && PerkListUI.instance != null) PerkListUI.instance.ForceClose();

        // Fatal Sight Protocol: isCriticalHit may already be set by a perk in ModifyCombat
        if (payload.isCriticalHit)
        {
            if (!skipDiceVisuals)
            {
                UpdateTotalDamageDisplay(payload.GetFinalDamage());
                if (criticalText != null) StartCoroutine(diceUI.CriticalTextPopAnimation());
                yield return StartCoroutine(diceUI.SkippableWait(0.5f));
            }
        }
        else if (!skipDiceVisuals && Random.value < RunManager.instance.criticalChance)
        {
            payload.isCriticalHit = true; UpdateTotalDamageDisplay(payload.GetFinalDamage());
            if (criticalText != null) StartCoroutine(diceUI.CriticalTextPopAnimation()); yield return StartCoroutine(diceUI.SkippableWait(0.5f));
        }
        else if (skipDiceVisuals && Random.value < RunManager.instance.criticalChance)
        {
            payload.isCriticalHit = true;
        }

        // OverClok: zar gizlenmeden önce 2x hasarı göster
        long finalDamage = payload.GetFinalDamage();
        if (RunManager.instance.doubleDamageNextCombat)
        {
            finalDamage *= 2;
            RunManager.instance.doubleDamageNextCombat = false;
            if (!skipDiceVisuals)
            {
                UpdateTotalDamageDisplay(finalDamage);
                yield return StartCoroutine(diceUI.SkippableWait(0.5f));
            }
        }
        // Red Magic Tile: 2x damage while standing on it (single-use)
        if (MagicTileManager.instance != null && MagicTileManager.instance.IsPlayerOnMagicTile(out MagicTileType redCheck) && redCheck == MagicTileType.Red)
        {
            finalDamage *= 2;
            MagicTileManager.instance.ConsumePlayerTile();
            if (!skipDiceVisuals)
            {
                UpdateTotalDamageDisplay(finalDamage);
                yield return StartCoroutine(diceUI.SkippableWait(0.5f));
            }
        }

        if (!skipDiceVisuals)
        {
            if (speedDiceMode)
                yield return new WaitForSeconds(0.5f);
            else
                yield return StartCoroutine(diceUI.SkippableWait(0.4f));
        }
        HideDiceResults();
        diceUI.EndDiceAnim();

        long damagePerEnemy = 0;
        if (targets.Count > 0)
        {
            if (RunManager.instance.cleaveNextCombat) { damagePerEnemy = finalDamage; RunManager.instance.cleaveNextCombat = false; }
            else { damagePerEnemy = finalDamage / targets.Count; }
        }

        if (player != null) player.TriggerAttackAnimation();
        if (AudioManager.instance != null) AudioManager.instance.PlaySwing();
        yield return new WaitForSeconds(0.3f);
        if (AudioManager.instance != null) AudioManager.instance.PlayHit();
        isAttackAnimationPlaying = false;
        hexesMovedThisTurn = 0;

        List<EnemyMovement> knockedEnemies = new List<EnemyMovement>(); List<EnemyMovement> deadEnemiesThisTurn = new List<EnemyMovement>();

        var voodooPerk = RunManager.instance.activePerks.Find(p => p is VoodooParasitePerk) as VoodooParasitePerk;
        var retributionPerk = RunManager.instance.activePerks.Find(p => p is RetributionSplicerPerk) as RetributionSplicerPerk;
        var pressurePointPerk = RunManager.instance.activePerks.Find(p => p is PressurePointPerk) as PressurePointPerk;
        var echoStrikePerk = RunManager.instance.activePerks.Find(p => p is EchoStrikePerk) as EchoStrikePerk;
        var necroticTouchPerk = RunManager.instance.activePerks.Find(p => p is NecroticTouchPerk) as NecroticTouchPerk;
        var deadweightPerk = RunManager.instance.activePerks.Find(p => p is DeadweightPerk) as DeadweightPerk;
        var overkillPerk = RunManager.instance.activePerks.Find(p => p is OverkillProtocolPerk) as OverkillProtocolPerk;

        foreach (var enemy in targets)
        {
            if (enemy == null) continue;
            long actualDamage = damagePerEnemy;

            // Pressure Point: dusmanin HP yuzdesine gore hasar carpani
            if (pressurePointPerk != null)
            {
                double ppMult = pressurePointPerk.GetMultiplier(enemy);
                actualDamage = (long)System.Math.Min(actualDamage * ppMult, long.MaxValue);
                if (ppMult > 1f) pressurePointPerk.TriggerVisualPop();
            }

            // Necrotic Touch: %25 alti HP'deki dusmanlar 2x hasar alir
            if (necroticTouchPerk != null)
            {
                double ntMult = necroticTouchPerk.GetMultiplier(enemy);
                if (ntMult > 1f)
                {
                    actualDamage = (long)System.Math.Min(actualDamage * ntMult, long.MaxValue);
                    necroticTouchPerk.TriggerVisualPop();
                }
            }

            // Deadweight: stunlanmış düşmanlar ekstra hasar alır
            if (deadweightPerk != null && deadweightPerk.IsStunned(enemy))
            {
                double dwMult = deadweightPerk.GetStunnedMultiplier();
                actualDamage = (long)System.Math.Min(actualDamage * dwMult, long.MaxValue);
                deadweightPerk.TriggerVisualPop();
            }

            if (retributionPerk != null)
            {
                long stackBonus = retributionPerk.GetBonusFor(enemy);
                retributionPerk.RegisterHit(enemy);
                if (stackBonus > 0)
                {
                    // Retribution bonusunu payload multiplier ve crit ile scale et
                    double scaledBonus = stackBonus * (double)payload.multiplier;
                    if (payload.isCriticalHit)
                        scaledBonus *= RunManager.instance.criticalDamageMultiplier;
                    actualDamage += (long)System.Math.Min(scaledBonus, long.MaxValue - actualDamage);
                    retributionPerk.TriggerVisualPop();
                    if (!skipDiceVisuals && PerkListUI.instance != null) PerkListUI.instance.TriggerShakeForPerk(retributionPerk);
                }
            }

            long hpBefore = enemy.health.currentHP;
            bool dies = hpBefore <= actualDamage;
            enemy.health.TakeDamage(actualDamage, true);
            ApplyBurnIfActive(enemy);
            ApplyCystIfActive(enemy);

            SpawnSlashEffect(enemy.transform.position);

            // Echo Strike: ayni hedefe tekrar vur (animasyonlu)
            if (echoStrikePerk != null && !dies && enemy.health.currentHP > 0 && echoStrikePerk.ShouldEcho())
            {
                yield return new WaitForSeconds(0.25f);
                if (player != null) player.TriggerAttackAnimation();
                if (AudioManager.instance != null) AudioManager.instance.PlaySwing();
                yield return new WaitForSeconds(0.2f);
                if (AudioManager.instance != null) AudioManager.instance.PlayHit();

                long echoDmg = actualDamage;
                if (enemy.health.currentHP <= echoDmg) dies = true;
                enemy.health.TakeDamage(echoDmg, true);
                SpawnSlashEffect(enemy.transform.position);
            }

            // Overkill Protocol: fazla hasari baska dusmana aktar
            if (overkillPerk != null && dies)
            {
                long overkill = actualDamage - hpBefore;
                if (overkill > 0) overkillPerk.TransferOverkill(enemy, overkill);
            }

            RegisterComboHit();
            knockedEnemies.Add(enemy); if (dies) deadEnemiesThisTurn.Add(enemy);

            // ========================================================
            // VOODOO PARASITE GÜNCELLEMESİ (Canı en çok olana vurma)
            // ========================================================
            if (voodooPerk != null && enemies.Count > 1)
            {
                // Hayatta olanları candan (büyükten küçüğe) sıralayıp liste haline getir
                var others = enemies.Where(e => e != null && e != enemy && e.health.currentHP > 0
                                            && !e.IsBoss)
                                    .OrderByDescending(e => e.health.currentHP)
                                    .ToList();

                int voodooHits = Mathf.Min(voodooPerk.currentLevel, others.Count);
                for (int v = 0; v < voodooHits; v++)
                {
                    // Voodoo Parasite ikincil hasari — AI/hareket interrupt etme (Pyrogenic Glands ile ayni pattern).
                    // applyStun=false => HealthScript enemy.ApplyStun cagirmaz.
                    // Warlock'un teleport etmemesi icin isBurnDamage flag'ini set et.
                    var warlock = others[v].GetComponent<WarlockEnemyAI>();
                    if (warlock != null) warlock.isBurnDamage = true;
                    others[v].health.TakeDamage(damagePerEnemy, false, false);

                    SpawnSlashEffect(others[v].transform.position);
                }
                if (voodooHits > 0) voodooPerk.TriggerVisualPop();
            }
        }

        foreach (var e in knockedEnemies)
        {
            if (e == null) continue;
            // MitsuriBlade uzak saldırıda yön hesabı farklı
            Vector3Int rawTargetCell;
            bool isRangedAttack = !IsNeighbor(e.GetCurrentCellPosition(), player.GetCurrentCellPosition());
            if (isRangedAttack)
                rawTargetCell = HexGridUtils.GetKnockbackCellRanged(e.GetCurrentCellPosition(), player.GetCurrentCellPosition());
            else
                rawTargetCell = GetRawOppositeCell(e.GetCurrentCellPosition(), player.GetCurrentCellPosition());
            EnemyMovement enemyBehind = GetEnemyAtCell(rawTargetCell);

            if (enemyBehind != null)
            {
                // Neural Hijack: knockback yapan düşman (e) dönüşür
                var neuralPerk = RunManager.instance != null
                    ? RunManager.instance.activePerks.Find(p => p is NeuralHijackPerk) as NeuralHijackPerk
                    : null;
                bool converted = false;
                if (neuralPerk != null && !e.isAllied && !e.wasAllied && !e.IsBoss)
                {
                    ConvertToAlly(e, damagePerEnemy, enemyBehind);
                    neuralPerk.TriggerVisualPop();
                    converted = true;
                }

                if (!converted) { e.ApplyStun(2, true); }
                enemyBehind.ApplyStun(2, true);
                Vector3 cPos = groundMap.GetCellCenterWorld(e.GetCurrentCellPosition()); Vector3 pPos = groundMap.GetCellCenterWorld(player.GetCurrentCellPosition());
                cPos.z = 0; pPos.z = 0; Vector3 bumpDir = (cPos - pPos).normalized;
                if (!converted) { e.StartWallBump(bumpDir); }
                enemyBehind.StartWallBump(bumpDir);

                // Hydraulic Impact: düşmanı düşmana çarptırınca ikisine de hasar
                if (RunManager.instance != null)
                {
                    var hiPerk = RunManager.instance.activePerks.Find(p => p is HydraulicImpactPerk) as HydraulicImpactPerk;
                    if (hiPerk != null) { hiPerk.ApplyWallDamage(e); hiPerk.ApplyWallDamage(enemyBehind); }
                }
            }
            else if (!HasWalkableTile(rawTargetCell) || player.GetCurrentCellPosition() == rawTargetCell)
            {
                // Düşman scaffold duvarındaysa: scaffold kırılsın, düşman düşsün (sessiz kill — damage text yok)
                Vector3Int enemyCell = e.GetCurrentCellPosition();
                if (ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(enemyCell))
                {
                    e.health.TakeDamageSilent(e.health.currentHP);
                    ScaffoldManager.instance.OnEntityDied(enemyCell);
                }
                else
                {
                    e.ApplyStun(2, true);
                    Vector3 cPos = groundMap.GetCellCenterWorld(enemyCell); Vector3 pPos = groundMap.GetCellCenterWorld(player.GetCurrentCellPosition());
                    cPos.z = 0; pPos.z = 0; e.StartWallBump((cPos - pPos).normalized);

                    // Hydraulic Impact: duvara itince hasar
                    if (RunManager.instance != null)
                    {
                        var hiPerk = RunManager.instance.activePerks.Find(p => p is HydraulicImpactPerk) as HydraulicImpactPerk;
                        if (hiPerk != null) hiPerk.ApplyWallDamage(e);
                    }
                }
            }
            else
            {
                // Phantom Assault: düşman knockback yiyip hücresini boşaltıyorsa, eski yerine hayalet bırak
                if (RunManager.instance != null)
                {
                    var paPerk = RunManager.instance.activePerks.Find(p => p is PhantomAssaultPerk) as PhantomAssaultPerk;
                    if (paPerk != null) paPerk.SpawnGhostAtCell(e.GetCurrentCellPosition());
                }
                e.ApplyStun(1, false); e.StartKnockbackMovement(rawTargetCell);
            }
        }

        var recoilPerk = RunManager.instance.activePerks.Find(p => p is RecoilSpringPerk) as RecoilSpringPerk;
        bool didRecoil = false;
        if (recoilPerk != null && targets.Count > 0)
        {
            Vector3Int playerOriginal = player.GetCurrentCellPosition();
            Vector3 playerWorld = groundMap.GetCellCenterWorld(playerOriginal); playerWorld.z = 0;

            // Tüm hedeflerin yönlerini topla, ortalama vektör bul
            Vector2 avgDir = Vector2.zero;
            foreach (var t in targets)
            {
                if (t == null) continue;
                Vector3 eWorld = groundMap.GetCellCenterWorld(t.GetCurrentCellPosition()); eWorld.z = 0;
                avgDir += (Vector2)(eWorld - playerWorld).normalized;
            }

            Vector3Int bounceTo = playerOriginal;
            Vector2 recoilDir = Vector2.zero;
            if (avgDir.sqrMagnitude > 0.001f)
            {
                // Ortalama vektörün tam tersine en yakın hex komşusunu bul
                recoilDir = -avgDir.normalized;
                Vector3Int[] offsets = (playerOriginal.y % 2 != 0) ? evenOffsets : oddOffsets;

                // İki geçiş: önce güvenli kareler, sonra tehlikeli kareler
                float bestDot = -2f;
                bool foundSafe = false;
                foreach (var off in offsets)
                {
                    Vector3Int neighbor = playerOriginal + off;
                    if (!HasWalkableTile(neighbor) || IsEnemyAtCell(neighbor)) continue;
                    bool isHazard = LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(neighbor);
                    if (isHazard) continue; // Önce sadece güvenli kareleri dene
                    Vector3 nWorld = groundMap.GetCellCenterWorld(neighbor); nWorld.z = 0;
                    Vector2 nDir = (Vector2)(nWorld - playerWorld).normalized;
                    float dot = Vector2.Dot(recoilDir, nDir);
                    if (dot > bestDot) { bestDot = dot; bounceTo = neighbor; foundSafe = true; }
                }
                // Güvenli kare bulunamadıysa tehlikeli kareleri de değerlendir
                if (!foundSafe)
                {
                    foreach (var off in offsets)
                    {
                        Vector3Int neighbor = playerOriginal + off;
                        if (!HasWalkableTile(neighbor) || IsEnemyAtCell(neighbor)) continue;
                        Vector3 nWorld = groundMap.GetCellCenterWorld(neighbor); nWorld.z = 0;
                        Vector2 nDir = (Vector2)(nWorld - playerWorld).normalized;
                        float dot = Vector2.Dot(recoilDir, nDir);
                        if (dot > bestDot) { bestDot = dot; bounceTo = neighbor; }
                    }
                }
            }

            if (bounceTo != playerOriginal)
            {
                recoilPerk.TriggerVisualPop();
                player.StartKnockbackMovement(bounceTo, true);
                didRecoil = true;
            }
            else if (recoilDir.sqrMagnitude > 0.001f)
            {
                // Arkada tile yok — duvara çarp
                recoilPerk.TriggerVisualPop();
                player.StartWallBump(new Vector3(recoilDir.x, recoilDir.y, 0f));
                didRecoil = true;
            }
        }

        yield return new WaitUntil(() => { foreach (var e in knockedEnemies) if (e != null && e.IsMoving()) return false; if (didRecoil && player.IsMoving()) return false; return true; });

        // Knockback sonrası: ölü düşman scaffold üzerinde kaldıysa scaffold çöksün
        if (ScaffoldManager.instance != null)
        {
            foreach (var e in knockedEnemies)
            {
                if (e != null && e.health.currentHP <= 0)
                    ScaffoldManager.instance.OnEntityDied(e.GetCurrentCellPosition());
            }
        }

        foreach (var e in knockedEnemies) if (e != null && payload.triggerExplosion) TriggerExplosion(e.GetCurrentCellPosition(), payload.explosionDamagePercent, false);

        List<EnemyMovement> deadFromSpikes = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
        foreach (var deadEnemy in deadFromSpikes)
            coinService.ProcessKillRewards(deadEnemy);
        UpdateCoinUI();
        if (CleanupDeadAndCheckLevelClear()) yield break;

        // Layer 2: Knockback ile explosion tile'a itilen düşmanlar
        if (LevelGenerator.instance != null && LevelGenerator.instance.explosionCells.Count > 0)
        {
            List<Vector3Int> knockbackExplosions = new List<Vector3Int>();
            foreach (var s in knockedEnemies)
            {
                if (s == null || s.health.currentHP <= 0) continue;
                Vector3Int eCell = s.GetCurrentCellPosition();
                if (LevelGenerator.instance.explosionCells.Contains(eCell) && !knockbackExplosions.Contains(eCell))
                {
                    knockbackExplosions.Add(eCell);
                    yield return StartCoroutine(TriggerExplosionTileCoroutine(eCell));
                    if (CleanupDeadAndCheckLevelClear()) yield break;
                }
            }
        }

        List<EnemyMovement> spikedEnemies = new List<EnemyMovement>(); bool anyoneBounced = false;
        // Explosion cell'ler hazardCells içinde ama spike olarak davranmamalı
        foreach (var s in knockedEnemies) if (s != null
            && LevelGenerator.instance.hazardCells.Contains(s.GetCurrentCellPosition())
            && !LevelGenerator.instance.explosionCells.Contains(s.GetCurrentCellPosition())) spikedEnemies.Add(s);

        if (spikedEnemies.Count > 0)
        {
            // Collection: spike'a itilen düşman sayacı
            int spikeTotal = PlayerPrefs.GetInt("total_spike_pushes", 0) + spikedEnemies.Count;
            PlayerPrefs.SetInt("total_spike_pushes", spikeTotal);
            GameEvents.EnemyPushedIntoSpike(spikeTotal);

            yield return new WaitForSeconds(0.2f);
            foreach (var s in spikedEnemies) { StartCoroutine(FlashHazardTileCoroutine(s.GetCurrentCellPosition())); s.health.TakeDamage(System.Math.Max(1L, s.health.maxHP / 2)); }

            var acidPerk = RunManager.instance.activePerks.Find(p => p is AcidBloodPerk) as AcidBloodPerk;
            if (acidPerk != null) { player.health.Heal(spikedEnemies.Count * acidPerk.currentLevel); acidPerk.TriggerVisualPop(); }

            yield return new WaitForSeconds(0.2f);
            foreach (var s in spikedEnemies) if (s != null && s.health.currentHP > 0) { Vector3Int randomBounceCell = GetRandomSafeNeighbor(s.GetCurrentCellPosition()); s.StartKnockbackMovement(randomBounceCell); anyoneBounced = true; }
        }

        if (anyoneBounced) yield return new WaitUntil(() => { foreach (var s in spikedEnemies) if (s != null && s.IsMoving()) return false; return true; });

        deadFromSpikes = enemies.FindAll(e => e != null && e.health.currentHP <= 0);
        foreach (var deadEnemy in deadFromSpikes)
            coinService.ProcessKillRewards(deadEnemy);
        UpdateCoinUI();
        if (CleanupDeadAndCheckLevelClear()) yield break;

        // Recoil Spring: oyuncu spike'a düştüyse hasar + geri itme
        if (didRecoil && LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(player.GetCurrentCellPosition()))
        {
            yield return new WaitForSeconds(0.15f);
            StartCoroutine(FlashHazardTileCoroutine(player.GetCurrentCellPosition()));
            if (RunManager.instance.hasBioBarrier)
                TryBreakBioBarrier();
            else PlayerTakeDamage(1);

            Vector3Int safeCell = GetSafeNeighbor(player.GetCurrentCellPosition());
            player.StartKnockbackMovement(safeCell);
            yield return new WaitUntil(() => !player.IsMoving());
        }

        if (didRecoil)
        {
            List<EnemyMovement> allAdjacent = GetAdjacentEnemies(player.GetCurrentCellPosition());
            List<EnemyMovement> nextTargets = allAdjacent.Where(e => e != null && e.health.currentHP > 0).ToList();
            if (nextTargets.Count > 0) yield return StartCoroutine(MultiAttack(nextTargets));
        }
        finalDamage = payload.GetFinalDamage();
        RunManager.instance.totalDamageDealt += finalDamage; // Toplam hasarı ekle

        // (Phantom Assault ghost placement now handled in knockback section above)
    }

    private IEnumerator EnemyAttackCoroutine(List<EnemyMovement> attackers)
    {
        attackers.RemoveAll(a => a.skipTurns > 0);
        if (attackers.Count == 0) { yield break; }

        // Melee saldırı başladığında animasyonları tetikle
        foreach (var attacker in attackers)
        {
            if (attacker.animator != null)
                attacker.animator.SetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.2f);
        bool dodged = false;
        if (RunManager.instance != null) dodged = Random.value < RunManager.instance.dodgeChance;

        if (dodged)
        {
            // DODGE EFEKTİNİ COROUTINE İLE ÇAĞIRIYORUZ (Kalkan Kırılma Animasyonu)
            StartCoroutine(AnimateShieldBreakFX(player.transform.position));
        }
        else if (RunManager.instance.hasBioBarrier)
            TryBreakBioBarrier();
        else PlayerTakeDamage(1);

        Vector3Int playerOriginalCell = player.GetCurrentCellPosition();
        Vector3Int playerTarget = GetOppositeCell(playerOriginalCell, attackers[0].GetCurrentCellPosition());
        player.StartKnockbackMovement(playerTarget);
        yield return new WaitUntil(() => !player.IsMoving());

        Vector3Int playerAfterKnockback = player.GetCurrentCellPosition();
        if (LevelGenerator.instance != null && LevelGenerator.instance.explosionCells.Contains(playerAfterKnockback))
        {
            yield return StartCoroutine(TriggerExplosionTileCoroutine(playerAfterKnockback));
            if (CleanupDeadAndCheckLevelClear()) yield break;
        }
        else if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(playerAfterKnockback))
        {
            yield return new WaitForSeconds(0.4f);
            if (RunManager.instance.hasBioBarrier)
                TryBreakBioBarrier();
            else PlayerTakeDamage(1);

            StartCoroutine(FlashHazardTileCoroutine(playerAfterKnockback));
            // Güvenli komşu hücreye it (eski hücre scaffold ise kırılmış olabilir)
            Vector3Int safeCell = GetSafeNeighbor(playerAfterKnockback);
            player.StartKnockbackMovement(safeCell);
            yield return new WaitUntil(() => !player.IsMoving());
        }

        yield return new WaitForSeconds(0.3f);
    }

    private void EndTurnAndDecreaseStuns()
    {
        if (player != null && player.health.currentHP > 0)
        {
            foreach (var e in enemies)
            {
                if (e != null) e.DecreaseStunTurn();
            }
            StartCoroutine(EndTurnWithAlliedAttacks());
        }
        else
        {
            if (RunManager.instance != null)
                RunManager.instance.totalTurnsPlayed++;
        }
    }

    private IEnumerator EndTurnWithAlliedAttacks()
    {
        yield return StartCoroutine(TickAlliedEnemies());
        if (CleanupDeadAndCheckLevelClear()) yield break;
        StartPlayerTurn();
        if (RunManager.instance != null)
            RunManager.instance.totalTurnsPlayed++;
    }

    // ──────── Volatile Roll: Zincirleme Zar Animasyonu ────────

    private IEnumerator VolatileRollChainAnimation(VolatileRollPerk perk, List<int> rolls, CombatPayload payload)
    {
        int chainStart = 0;
        int chainCount = 0;
        while (chainCount < 50)
        {
            int prevCount = rolls.Count;
            var extras = perk.GenerateChainRolls(rolls, chainStart);
            if (extras.Count == 0) break;

            // Her extra zarı tek tek animasyonla ekle
            foreach (int val in extras)
            {
                rolls.Add(val);
                payload.diceRolls.Add(val);
                diceUI.SpawnExtraDie(val);
                if (AudioManager.instance != null) AudioManager.instance.PlayDiceHit();
                UpdateTotalDamageDisplay(payload.GetFinalDamage());
                yield return StartCoroutine(diceUI.SkippableWait(0.25f));
            }

            chainStart = prevCount;
            chainCount++;
        }

        perk.TriggerVisualPop();
        yield return StartCoroutine(diceUI.SkippableWait(0.3f));
    }

    // ──────── Neural Hijack: Dost Düşman Sistemi ────────

    private void ConvertToAlly(EnemyMovement enemy, long damage, EnemyMovement pushedEnemy = null)
    {
        enemy.isAllied = true;
        enemy.wasAllied = true;
        enemy.movementStyle = MovementStyle.None;

        // 3 HP'ye ayarla
        enemy.health.maxHP = 3;
        enemy.health.currentHP = 3;
        enemy.health.updateHealth();

        // Stun/knockback iptal — dosta döndüğü anda sakin olsun
        enemy.skipTurns = 0;
        enemy.isBumping = false;

        // Neural Hijack: oyuncunun bu saldırıdaki hasarını kaydet
        enemy.hijackDamage = System.Math.Max(1L, damage);

        // Sprite'ı itilen düşmana doğru çevir
        if (pushedEnemy != null)
        {
            var visuals = enemy.GetComponent<EnemyVisuals>();
            if (visuals != null && visuals.visualRenderer != null)
            {
                float dx = pushedEnemy.transform.position.x - enemy.transform.position.x;
                if (Mathf.Abs(dx) > 0.01f)
                    visuals.visualRenderer.flipX = (dx < 0);
            }
        }

        // Bruiser: charge state iptal et
        var bruiser = enemy.GetComponent<BruiserEnemyAI>();
        if (bruiser != null)
        {
            bruiser.CancelCharge();
            bruiser.isChargingAttack = false;
        }

        // Warlock: saldırı döngüsü iptal et
        var warlock = enemy.GetComponent<WarlockEnemyAI>();
        if (warlock != null)
            warlock.OnWarlockDied(); // Tüm warning'leri temizler, döngüyü sıfırlar

        // Ninja: warning'leri temizle, döngüyü sıfırla
        var ninja = enemy.GetComponent<NinjaEnemyAI>();
        if (ninja != null)
            ninja.OnNinjaDied();

        // Animator'ı idle'a çek
        if (enemy.animator != null)
        {
            enemy.animator.SetBool("IsCharging", false);
            enemy.animator.SetBool("IsAttacking", false);
            enemy.animator.ResetTrigger("Attack");
            enemy.animator.ResetTrigger("GotHit");
        }

        // Okları gizle — ally'nin yön oku göstermesine gerek yok
        enemy.SetArrowVisibility(false);

        // Yeşil tint — dost olduğu belli olsun (hasar flash sonrası da korunsun)
        Color allyColor = new Color(0.3f, 1f, 0.4f, 1f);
        enemy.health.SetOriginalColor(allyColor);
    }

    /// <summary>
    /// Her tur sonunda dost düşmanlar:
    /// 1. En yakın düşmana doğru 1 hex hareket eder
    /// 2. Komşu düşmanlara oyuncunun son saldırı hasarıyla vurur (animasyonlu)
    /// Tıpatıp normal düşman gibi davranır.
    /// </summary>
    private IEnumerator TickAlliedEnemies()
    {
        List<EnemyMovement> allies = enemies.FindAll(e => e != null && e.isAllied && e.health.currentHP > 0);
        if (allies.Count == 0) yield break;

        // 1) Hareket: en yakın düşmana doğru 1 hex (charge durumundaki ally hareket etmez)
        foreach (var ally in allies)
        {
            if (allyChargeCells.ContainsKey(ally)) continue; // Charge turunda — yerinde kal

            Vector3Int allyCell = ally.GetCurrentCellPosition();
            Vector3Int bestTarget = allyCell;
            float bestDist = float.MaxValue;

            // En yakın düşmanı bul
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.isAllied || enemy.health.currentHP <= 0) continue;
                float dist = HexGridUtils.DistanceCube(allyCell, enemy.GetCurrentCellPosition());
                if (dist < bestDist) { bestDist = dist; bestTarget = enemy.GetCurrentCellPosition(); }
            }

            if (bestTarget == allyCell) continue; // Düşman yok

            // Komşu hücrelerden düşmana en yakın olanı seç
            Vector3Int[] offsets = (allyCell.y % 2 != 0) ? evenOffsets : oddOffsets;
            Vector3Int bestMove = allyCell;
            float bestMoveDist = bestDist;

            foreach (var off in offsets)
            {
                Vector3Int neighbor = allyCell + off;
                if (!HasWalkableTile(neighbor)) continue;
                if (IsEnemyAtCell(neighbor) && !GetEnemyAtCell(neighbor).isAllied) continue; // Düşmanın üzerine basma, saldıracaksın
                if (IsEnemyAtCell(neighbor)) continue; // Başka ally'nin üzerine basma
                if (player != null && player.GetCurrentCellPosition() == neighbor) continue;
                if (LevelGenerator.instance != null && LevelGenerator.instance.hazardCells.Contains(neighbor)) continue;

                float d = HexGridUtils.DistanceCube(neighbor, bestTarget);
                if (d < bestMoveDist) { bestMoveDist = d; bestMove = neighbor; }
            }

            if (bestMove != allyCell)
            {
                // Scaffold handling
                if (ScaffoldManager.instance != null)
                {
                    ScaffoldManager.instance.OnEntityLeave(allyCell);
                    ScaffoldManager.instance.OnEntityEnter(bestMove);
                }
                ally.StartKnockbackMovement(bestMove); // Hareket için knockback kullan (smooth movement)
                ally.SetArrowVisibility(false); // Ally okları göstermesin
            }
        }

        // Hareket animasyonunu bekle
        yield return new WaitUntil(() => { foreach (var a in allies) if (a != null && a.IsMoving()) return false; return true; });
        yield return new WaitForSeconds(0.15f);

        // 2) Saldırı: tip bazlı ally saldırısı (tur bazlı charge → attack)
        bool anyAttack = false;

        // Ölmüş ally'lerin charge state'ini temizle
        List<EnemyMovement> deadKeys = new List<EnemyMovement>();
        foreach (var kv in allyChargeCells)
            if (kv.Key == null || kv.Key.health.currentHP <= 0) deadKeys.Add(kv.Key);
        foreach (var dk in deadKeys) { ClearAllyWarnings(allyChargeCells[dk]); allyChargeCells.Remove(dk); }

        foreach (var ally in allies)
        {
            if (ally == null || ally.health.currentHP <= 0) continue;

            Vector3Int allyCell = ally.GetCurrentCellPosition();

            // ── BRUISER ALLY ──
            var bruiserAI = ally.GetComponent<BruiserEnemyAI>();
            if (bruiserAI != null)
            {
                if (allyChargeCells.ContainsKey(ally))
                {
                    // ATTACK TURN: önceki turda charge edildi, şimdi saldır
                    yield return StartCoroutine(AllyBruiserExecute(ally, bruiserAI, allyCell));
                    anyAttack = true;
                }
                else
                {
                    // CHARGE TURN: hedef bul, warning göster, charge animasyonu
                    yield return StartCoroutine(AllyBruiserCharge(ally, bruiserAI, allyCell));
                }
                continue;
            }

            // ── WARLOCK ALLY ──
            if (ally.IsWarlock)
            {
                if (allyChargeCells.ContainsKey(ally))
                {
                    yield return StartCoroutine(AllyWarlockExecute(ally, allyCell));
                    anyAttack = true;
                }
                else
                {
                    yield return StartCoroutine(AllyWarlockCharge(ally, allyCell));
                }
                continue;
            }

            // ── NORMAL MELEE ALLY ──
            Vector3Int[] offsets = (allyCell.y % 2 != 0) ? evenOffsets : oddOffsets;
            List<EnemyMovement> targets = new List<EnemyMovement>();

            foreach (var off in offsets)
            {
                Vector3Int neighborCell = allyCell + off;
                EnemyMovement target = GetEnemyAtCell(neighborCell);
                if (target != null && !target.isAllied && target.health.currentHP > 0)
                    targets.Add(target);
            }

            if (targets.Count > 0)
            {
                if (ally.animator != null) ally.animator.SetTrigger("Attack");
                anyAttack = true;

                foreach (var target in targets)
                {
                    long allyDamage = System.Math.Max(1L, ally.hijackDamage);
                    target.health.TakeDamage(allyDamage);
                    AllyKnockbackTarget(target, allyCell);
                }
            }
        }

        if (anyAttack)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayHit();
            yield return new WaitForSeconds(0.3f);
            yield return new WaitUntil(() =>
            {
                foreach (var e in enemies)
                    if (e != null && !e.isAllied && e.IsMoving()) return false;
                return true;
            });
        }
    }

    // ─── ALLY ATTACK HELPERS ───

    /// <summary>Bruiser ally CHARGE turu: hedef bul, warning göster, charge animasyonu.</summary>
    private IEnumerator AllyBruiserCharge(EnemyMovement ally, BruiserEnemyAI bruiserAI, Vector3Int allyCell)
    {
        // Find nearest non-allied enemy within range
        EnemyMovement bestTarget = null;
        float bestDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == null || e.isAllied || e.health.currentHP <= 0) continue;
            float dist = HexGridUtils.DistanceCube(allyCell, e.GetCurrentCellPosition());
            if (dist < bestDist && dist <= bruiserAI.aoeAttackRange)
            {
                bestDist = dist;
                bestTarget = e;
            }
        }
        if (bestTarget == null) yield break;

        Vector3Int targetCell = bestTarget.GetCurrentCellPosition();
        List<Vector3Int> lineCells = GetAllyBruiserLine(allyCell, targetCell, bruiserAI.aoeAttackRange);
        if (lineCells.Count == 0) yield break;

        // Charge animasyonu + warning tile'lar
        if (ally.animator != null) ally.animator.SetBool("IsCharging", true);
        yield return null;
        if (AudioManager.instance != null) AudioManager.instance.PlayCharge();
        foreach (var c in lineCells) DrawWarningTile(c);

        // State'i kaydet — bir sonraki tur attack yapacak
        allyChargeCells[ally] = new List<Vector3Int>(lineCells);
    }

    /// <summary>Bruiser ally ATTACK turu: önceki turda charge edilen hücrelere saldır.</summary>
    private IEnumerator AllyBruiserExecute(EnemyMovement ally, BruiserEnemyAI bruiserAI, Vector3Int allyCell)
    {
        List<Vector3Int> lineCells = allyChargeCells[ally];
        allyChargeCells.Remove(ally);

        // Attack animasyonu
        if (ally.animator != null)
        {
            ally.animator.SetBool("IsCharging", false);
            yield return null;
            ally.animator.SetTrigger("Attack");
        }

        // Flash warning tiles
        Color attackFlash = new Color(0f, 1f, 0.5f, 1f);
        if (warningMap != null)
        {
            foreach (var c in lineCells)
            {
                if (warningMap.HasTile(c))
                {
                    warningMap.SetTileFlags(c, TileFlags.None);
                    warningMap.SetColor(c, attackFlash);
                }
            }
        }

        yield return new WaitForSeconds(0.08f);

        // VFX
        if (bruiserAI.hammerImpactVFXPrefab != null)
        {
            foreach (var c in lineCells)
            {
                Vector3 worldPos = groundMap.GetCellCenterWorld(c);
                worldPos.z = 0f;
                worldPos.x += bruiserAI.vfxXOffset;
                worldPos.y += bruiserAI.vfxYOffset;
                GameObject vfx = Instantiate(bruiserAI.hammerImpactVFXPrefab, worldPos, Quaternion.identity);
                Destroy(vfx, 3f);
                if (HitstopManager.instance != null) HitstopManager.instance.TriggerHitstop();
                CameraController.ShakeLighter();
            }
        }

        // Damage
        long allyDamage = System.Math.Max(1L, ally.hijackDamage);
        foreach (var c in lineCells)
        {
            EnemyMovement hit = GetEnemyAtCell(c);
            if (hit != null && !hit.isAllied && hit.health.currentHP > 0)
            {
                hit.health.TakeDamage(allyDamage);
                AllyKnockbackTarget(hit, allyCell);
            }
        }

        // Fade and clear warning tiles
        if (warningMap != null)
        {
            float fadeDur = 0.3f;
            float elapsed = 0f;
            Color endFade = new Color(attackFlash.r, attackFlash.g, attackFlash.b, 0f);
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                Color current = Color.Lerp(attackFlash, endFade, t);
                foreach (var c in lineCells)
                {
                    if (warningMap.HasTile(c)) warningMap.SetColor(c, current);
                }
                yield return null;
            }
            foreach (var c in lineCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetTile(c, null);
            }
        }

        // Animator cleanup
        if (ally.animator != null)
        {
            ally.animator.SetBool("IsCharging", false);
            ally.animator.SetBool("IsAttacking", false);
            ally.animator.ResetTrigger("Attack");
            ally.animator.ResetTrigger("GotHit");
        }
    }

    private List<Vector3Int> GetAllyBruiserLine(Vector3Int startCell, Vector3Int targetCell, int length)
    {
        List<Vector3Int> line = new List<Vector3Int>();
        int bestDirIndex = 0;
        float minDist = float.MaxValue;
        Vector3Int[] startOffsets = (startCell.y % 2 != 0) ? evenOffsets : oddOffsets;

        for (int i = 0; i < 6; i++)
        {
            float d = HexGridUtils.DistanceCube(startCell + startOffsets[i], targetCell);
            if (d < minDist) { minDist = d; bestDirIndex = i; }
        }

        Vector3Int currentStep = startCell;
        for (int i = 0; i < length; i++)
        {
            Vector3Int[] currOffsets = (currentStep.y % 2 != 0) ? evenOffsets : oddOffsets;
            currentStep += currOffsets[bestDirIndex];
            if (HasWalkableTile(currentStep)) line.Add(currentStep);
        }
        return line;
    }

    /// <summary>Warlock ally CHARGE turu: hedef bul, warning göster, charge animasyonu.</summary>
    private IEnumerator AllyWarlockCharge(EnemyMovement ally, Vector3Int allyCell)
    {
        var warlockAI = ally.GetComponent<WarlockEnemyAI>();
        Animator wAnimator = (warlockAI != null && warlockAI.animator != null) ? warlockAI.animator : ally.animator;

        // Find nearest non-allied enemy
        EnemyMovement bestTarget = null;
        float bestDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == null || e.isAllied || e.health.currentHP <= 0) continue;
            float dist = HexGridUtils.DistanceCube(allyCell, e.GetCurrentCellPosition());
            if (dist < bestDist) { bestDist = dist; bestTarget = e; }
        }
        if (bestTarget == null) yield break;

        Vector3Int targetCell = bestTarget.GetCurrentCellPosition();
        List<Vector3Int> aoeCells = new List<Vector3Int> { targetCell };
        Vector3Int[] targetOffsets = (targetCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        int[] indices = { 0, 2, 4 };
        foreach (int i in indices)
        {
            Vector3Int neighbor = targetCell + targetOffsets[i];
            if (HasWalkableTile(neighbor)) aoeCells.Add(neighbor);
        }

        // Charge animasyonu + warning tile'lar
        if (wAnimator != null) wAnimator.SetBool("IsCharging", true);
        yield return null;
        if (AudioManager.instance != null) AudioManager.instance.PlayCharge();

        Color warnColor = new Color(0.4f, 0.8f, 1f, 0.7f);
        if (warningMap != null)
        {
            foreach (var c in aoeCells)
            {
                warningMap.SetTile(c, warningTile);
                warningMap.SetTileFlags(c, TileFlags.None);
                warningMap.SetColor(c, warnColor);
            }
        }

        allyChargeCells[ally] = new List<Vector3Int>(aoeCells);
    }

    /// <summary>Warlock ally ATTACK turu: önceki turda charge edilen hücrelere saldır.</summary>
    private IEnumerator AllyWarlockExecute(EnemyMovement ally, Vector3Int allyCell)
    {
        List<Vector3Int> aoeCells = allyChargeCells[ally];
        allyChargeCells.Remove(ally);

        var warlockAI = ally.GetComponent<WarlockEnemyAI>();
        Animator wAnimator = (warlockAI != null && warlockAI.animator != null) ? warlockAI.animator : ally.animator;

        // Attack animasyonu
        if (wAnimator != null)
        {
            wAnimator.SetBool("IsCharging", false);
            yield return null;
            wAnimator.SetBool("IsAttacking", true);
        }

        // Flash warning tiles
        Color flashColor = new Color(0.4f, 0.8f, 1f, 1f);
        if (warningMap != null)
        {
            foreach (var c in aoeCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetColor(c, flashColor);
            }
        }

        yield return new WaitForSeconds(0.05f);

        // Sound + VFX
        if (AudioManager.instance != null) AudioManager.instance.PlayLightning();
        if (HitstopManager.instance != null) HitstopManager.instance.TriggerHitstop();
        CameraController.ShakeLighter();

        if (warlockAI != null && warlockAI.impactVFXPrefab != null)
        {
            foreach (var c in aoeCells)
            {
                Vector3 worldPos = groundMap.GetCellCenterWorld(c);
                worldPos.z = 0f;
                worldPos.y += warlockAI.vfxYOffset;
                GameObject vfx = Instantiate(warlockAI.impactVFXPrefab, worldPos, Quaternion.identity);
                Destroy(vfx, 3f);
            }
        }

        // Damage
        long allyDamage = System.Math.Max(1L, ally.hijackDamage);
        foreach (var c in aoeCells)
        {
            EnemyMovement hit = GetEnemyAtCell(c);
            if (hit != null && !hit.isAllied && hit.health.currentHP > 0)
            {
                hit.health.TakeDamage(allyDamage);
                AllyKnockbackTarget(hit, allyCell);
            }
        }

        // Fade and clear warning tiles
        if (warningMap != null)
        {
            float fadeDur = 0.25f;
            float elapsed = 0f;
            Color endFade = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                t = t * t * (3f - 2f * t);
                Color current = Color.Lerp(flashColor, endFade, t);
                foreach (var c in aoeCells)
                {
                    if (warningMap.HasTile(c)) warningMap.SetColor(c, current);
                }
                yield return null;
            }
            foreach (var c in aoeCells)
            {
                if (warningMap.HasTile(c)) warningMap.SetTile(c, null);
            }
        }

        // Animator cleanup
        if (wAnimator != null)
        {
            wAnimator.SetBool("IsCharging", false);
            wAnimator.SetBool("IsAttacking", false);
            wAnimator.ResetTrigger("Attack");
            wAnimator.ResetTrigger("GotHit");
        }
    }

    private void ClearAllyWarnings(List<Vector3Int> cells)
    {
        if (warningMap == null || cells == null) return;
        foreach (var c in cells)
        {
            if (warningMap.HasTile(c)) warningMap.SetTile(c, null);
        }
    }

    private void AllyKnockbackTarget(EnemyMovement target, Vector3Int allyCell)
    {
        if (target == null || target.health.currentHP <= 0) return;
        Vector3Int targetCell = target.GetCurrentCellPosition();
        Vector3Int rawOpposite = GetRawOppositeCell(targetCell, allyCell);

        if (HasWalkableTile(rawOpposite) && !IsEnemyAtCell(rawOpposite)
            && (player == null || player.GetCurrentCellPosition() != rawOpposite))
        {
            // Knockback + stun 1
            target.StartKnockbackMovement(rawOpposite);
            target.skipTurns = Mathf.Max(target.skipTurns, 1);
        }
        else
        {
            // Wall bump — stun 2
            target.skipTurns = Mathf.Max(target.skipTurns, 2);
        }
    }

    public void ResumeAfterShop() { StartPlayerTurn(); }

    private void ApplyBurnIfActive(EnemyMovement enemy)
    {
        if (RunManager.instance == null || enemy == null || enemy.health.currentHP <= 0) return;
        var perk = RunManager.instance.activePerks.Find(p => p is PyrogenicGlandsPerk) as PyrogenicGlandsPerk;
        if (perk != null) perk.ApplyBurn(enemy);
    }

    private void ApplyCystIfActive(EnemyMovement enemy)
    {
        if (RunManager.instance == null || enemy == null || enemy.health.currentHP <= 0) return;
        var perk = RunManager.instance.activePerks.Find(p => p is ViralCystsPerk) as ViralCystsPerk;
        if (perk != null) perk.PlantCyst(enemy);
    }

    private void TickBurnsIfActive()
    {
        if (RunManager.instance == null) return;
        var perk = RunManager.instance.activePerks.Find(p => p is PyrogenicGlandsPerk) as PyrogenicGlandsPerk;
        if (perk != null) perk.TickBurns();
    }

    public void UpdateTotalDamageDisplay(long val) => diceUI.UpdateTotalDamageDisplay(val);
    public void HideDiceResults() => diceUI.HideDiceResults();
    public void RegisterComboHit() => diceUI.RegisterComboHit();
    public void ResetCombo() => diceUI.ResetCombo();
    public void AnimateSpecificDie(int index, int newValue) => diceUI.AnimateSpecificDie(index, newValue);

    public bool IsEnemyAtCell(Vector3Int cell)
    {
        foreach (var e in enemies) if (e != null && e.health.currentHP > 0 && e.GetCurrentCellPosition() == cell) return true;
        return false;
    }

    /// <summary>Ground VEYA scaffold tile'ı var mı? Scaffold hücrelerinde groundMap boş olabilir.</summary>
    public bool HasWalkableTile(Vector3Int cell)
    {
        if (groundMap.HasTile(cell)) return true;
        return ScaffoldManager.instance != null && ScaffoldManager.instance.IsScaffoldCell(cell);
    }

    public EnemyMovement GetEnemyAtCell(Vector3Int cell)
    {
        foreach (var e in enemies) if (e != null && e.health.currentHP > 0 && e.GetCurrentCellPosition() == cell) return e;
        return null;
    }

    private Vector3Int GetRawOppositeCell(Vector3Int centerCell, Vector3Int awayFromCell) => HexGridUtils.GetRawOppositeCell(centerCell, awayFromCell);

    public List<EnemyMovement> GetAdjacentEnemies(Vector3Int playerCell)
    {
        List<EnemyMovement> adjacentList = new List<EnemyMovement>();
        foreach (var enemy in enemies) if (enemy != null && enemy.health.currentHP > 0 && !enemy.isAllied) if (IsNeighbor(playerCell, enemy.GetCurrentCellPosition())) adjacentList.Add(enemy);
        return adjacentList;
    }

    public Vector3Int GetOppositeCell(Vector3Int centerCell, Vector3Int awayFromCell)
    {
        Vector3Int[] offsets = (centerCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        for (int i = 0; i < 6; i++)
        {
            if (centerCell + offsets[i] == awayFromCell)
            {
                int oppositeIndex = (i + 3) % 6;
                Vector3Int strictKnockbackCell = centerCell + offsets[oppositeIndex];
                if (!HasWalkableTile(strictKnockbackCell) || IsEnemyAtCell(strictKnockbackCell) || player.GetCurrentCellPosition() == strictKnockbackCell)
                    return centerCell;
                return strictKnockbackCell;
            }
        }
        return centerCell;
    }

    private bool IsNeighbor(Vector3Int cell1, Vector3Int cell2) => HexGridUtils.IsNeighbor(cell1, cell2);

    public Vector3Int GetRandomSafeNeighbor(Vector3Int centerCell)
    {
        Vector3Int[] offsets = (centerCell.y % 2 != 0) ? evenOffsets : oddOffsets;
        List<Vector3Int> safeNeighbors = new List<Vector3Int>();
        List<Vector3Int> walkableNeighbors = new List<Vector3Int>();
        foreach (var off in offsets)
        {
            Vector3Int neighbor = centerCell + off;
            if (!HasWalkableTile(neighbor)) continue;
            if (IsEnemyAtCell(neighbor) || player.GetCurrentCellPosition() == neighbor) continue;
            walkableNeighbors.Add(neighbor);
            if (!LevelGenerator.instance.hazardCells.Contains(neighbor))
                safeNeighbors.Add(neighbor);
        }
        if (safeNeighbors.Count > 0) return safeNeighbors[Random.Range(0, safeNeighbors.Count)];
        // No non-hazard neighbor — bounce to any walkable cell to avoid getting stuck on spikes
        if (walkableNeighbors.Count > 0) return walkableNeighbors[Random.Range(0, walkableNeighbors.Count)];
        return centerCell;
    }

    // ──────── Island Connectivity System ────────

    /// <summary>
    /// Checks if removing excludeCell would still leave the player connected to at least one hostile enemy.
    /// Used by SeismicStep to prevent self-isolation.
    /// </summary>
    public bool WouldRemainConnectedToEnemies(Vector3Int excludeCell)
    {
        if (player == null) return true;
        bool hasHostile = enemies.Exists(e => e != null && e.health.currentHP > 0 && !e.isAllied);
        if (!hasHostile) return true;

        Vector3Int playerCell = player.GetCurrentCellPosition();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(playerCell);
        visited.Add(playerCell);

        while (queue.Count > 0)
        {
            Vector3Int curr = queue.Dequeue();
            Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
            foreach (var off in offsets)
            {
                Vector3Int n = curr + off;
                if (n == excludeCell || visited.Contains(n)) continue;
                if (!HasWalkableTile(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        foreach (var e in enemies)
        {
            if (e == null || e.health.currentHP <= 0 || e.isAllied) continue;
            if (visited.Contains(e.GetCurrentCellPosition())) return true;
        }
        return false;
    }

    /// <summary>
    /// After a tile collapses, BFS from player to find reachable cells.
    /// Any enemy on a disconnected island → island collapses, enemy dies.
    /// </summary>
    private IEnumerator CollapseDisconnectedIslands()
    {
        isCollapsingIslands = true;
        yield return new WaitForSeconds(0.5f);

        if (player == null || player.health.currentHP <= 0 || LevelGenerator.instance == null)
        {
            isCollapsingIslands = false;
            yield break;
        }

        enemies.RemoveAll(e => e == null || e.health.currentHP <= 0);
        if (enemies.Count == 0) { isCollapsingIslands = false; yield break; }

        // BFS from player position
        Vector3Int playerCell = player.GetCurrentCellPosition();
        HashSet<Vector3Int> reachable = new HashSet<Vector3Int>();
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        queue.Enqueue(playerCell);
        reachable.Add(playerCell);

        while (queue.Count > 0)
        {
            Vector3Int curr = queue.Dequeue();
            Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
            foreach (var off in offsets)
            {
                Vector3Int n = curr + off;
                if (reachable.Contains(n)) continue;
                if (!HasWalkableTile(n)) continue;
                reachable.Add(n);
                queue.Enqueue(n);
            }
        }

        // Find stranded enemies (not reachable from player)
        List<EnemyMovement> stranded = new List<EnemyMovement>();
        foreach (var e in enemies)
        {
            if (e == null || e.health.currentHP <= 0) continue;
            if (!reachable.Contains(e.GetCurrentCellPosition()))
                stranded.Add(e);
        }

        if (stranded.Count == 0) { isCollapsingIslands = false; yield break; }

        // Cancel pending StartPlayerTurn — we'll restart after collapse
        CancelInvoke("StartPlayerTurn");

        // BFS from stranded enemies to collect all disconnected island cells
        HashSet<Vector3Int> islandCells = new HashSet<Vector3Int>();
        foreach (var e in stranded)
        {
            Vector3Int start = e.GetCurrentCellPosition();
            if (islandCells.Contains(start)) continue;

            Queue<Vector3Int> iQueue = new Queue<Vector3Int>();
            iQueue.Enqueue(start);
            islandCells.Add(start);

            while (iQueue.Count > 0)
            {
                Vector3Int curr = iQueue.Dequeue();
                Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offsets)
                {
                    Vector3Int n = curr + off;
                    if (islandCells.Contains(n) || reachable.Contains(n)) continue;
                    if (!HasWalkableTile(n)) continue;
                    islandCells.Add(n);
                    iQueue.Enqueue(n);
                }
            }
        }

        // Kill stranded enemies
        foreach (var e in stranded)
        {
            if (e != null && e.health.currentHP > 0)
            {
                e.health.TakeDamage(e.health.maxHP * 10);
                if (e.gameObject.activeInHierarchy)
                    StartCoroutine(e.FadeDieCoroutine());
            }
        }

        // Cascade collapse — sort by distance from player for domino visual
        List<Vector3Int> sorted = new List<Vector3Int>(islandCells);
        sorted.Sort((a, b) => HexGridUtils.DistanceCube(a, playerCell).CompareTo(HexGridUtils.DistanceCube(b, playerCell)));

        if (AudioManager.instance != null) AudioManager.instance.PlayWall();

        Tilemap gMap = LevelGenerator.instance.groundMap;
        Tilemap bgMap = LevelGenerator.instance.columnMap;
        Tilemap fgAMap = LevelGenerator.instance.foreGroundA;
        Tilemap fgBMap = LevelGenerator.instance.foreGroundB;

        foreach (var cell in sorted)
        {
            StartCoroutine(CollapseIslandCellCoroutine(cell, gMap, bgMap, fgAMap, fgBMap));
            yield return new WaitForSeconds(0.06f);
        }

        yield return new WaitForSeconds(0.4f);
        isCollapsingIslands = false;

        if (!CleanupDeadAndCheckLevelClear() && isPlayerTurn)
            Invoke("StartPlayerTurn", 0.1f);
    }

    private IEnumerator CollapseIslandCellCoroutine(Vector3Int cell, Tilemap gMap, Tilemap bgMap, Tilemap fgAMap, Tilemap fgBMap)
    {
        bool hasG = gMap != null && gMap.HasTile(cell);
        bool hasBg = bgMap != null && bgMap.HasTile(cell);
        bool hasFgA = fgAMap != null && fgAMap.HasTile(cell);
        bool hasFgB = fgBMap != null && fgBMap.HasTile(cell);
        if (!hasG && !hasBg && !hasFgA && !hasFgB) yield break;

        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Lerp(1f, 0f, t);
            float yOff = Mathf.Lerp(0f, -0.5f, t * t);
            Color fade = new Color(1f, 1f, 1f, 1f - t);
            Matrix4x4 m = Matrix4x4.TRS(new Vector3(0f, yOff, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            if (hasG) { gMap.SetTransformMatrix(cell, m); gMap.SetTileFlags(cell, TileFlags.None); gMap.SetColor(cell, fade); }
            if (hasBg) { bgMap.SetTransformMatrix(cell, m); bgMap.SetTileFlags(cell, TileFlags.None); bgMap.SetColor(cell, fade); }
            if (hasFgA) { fgAMap.SetTransformMatrix(cell, m); fgAMap.SetTileFlags(cell, TileFlags.None); fgAMap.SetColor(cell, fade); }
            if (hasFgB) { fgBMap.SetTransformMatrix(cell, m); fgBMap.SetTileFlags(cell, TileFlags.None); fgBMap.SetColor(cell, fade); }
            yield return null;
        }

        // Remove tiles
        if (hasG) { gMap.SetTransformMatrix(cell, Matrix4x4.identity); gMap.SetColor(cell, Color.white); gMap.SetTile(cell, null); }
        if (hasBg) { bgMap.SetTransformMatrix(cell, Matrix4x4.identity); bgMap.SetColor(cell, Color.white); bgMap.SetTile(cell, null); }
        if (hasFgA) { fgAMap.SetTransformMatrix(cell, Matrix4x4.identity); fgAMap.SetColor(cell, Color.white); fgAMap.SetTile(cell, null); }
        if (hasFgB) { fgBMap.SetTransformMatrix(cell, Matrix4x4.identity); fgBMap.SetColor(cell, Color.white); fgBMap.SetTile(cell, null); }

        if (LevelGenerator.instance != null)
        {
            LevelGenerator.instance.validCells.Remove(cell);
            LevelGenerator.instance.hazardCells.Remove(cell);
            LevelGenerator.instance.scaffoldCells.Remove(cell);
        }

        TrapTileEvents.FireTileDestroyed(cell);
    }

    public float DistanceCube(Vector3Int a, Vector3Int b) => HexGridUtils.DistanceCube(a, b);

    private Vector3Int OffsetToCube(Vector3Int o) => HexGridUtils.OffsetToCube(o);

    public IEnumerator FlashHazardTileCoroutine(Vector3Int cell)
    {
        Tilemap targetMap = LevelGenerator.instance.foreGroundA;
        if (targetMap == null) yield break;

        targetMap.SetTileFlags(cell, TileFlags.None);
        Color originalColor = Color.white;
        Color flashColor = new Color(0.2f, 1f, 0.2f, 1f);
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            targetMap.SetColor(cell, Color.Lerp(originalColor, flashColor, elapsed / duration));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            targetMap.SetColor(cell, Color.Lerp(flashColor, originalColor, elapsed / duration));
            yield return null;
        }
        targetMap.SetColor(cell, originalColor);
    }

    public void DrawWarningTile(Vector3Int cell)
    {
        if (warningMap == null || warningTile == null) return;
        StartCoroutine(SmoothWarningFadeIn(cell));
    }

    private IEnumerator SmoothWarningFadeIn(Vector3Int cell)
    {
        warningMap.SetTile(cell, warningTile); warningMap.SetTileFlags(cell, TileFlags.None);
        Color startColor = new Color(1f, 1f, 1f, 0f); Color endColor = new Color(1f, 1f, 1f, 0.5f);
        warningMap.SetColor(cell, startColor);
        float duration = 0.3f; float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!warningMap.HasTile(cell)) yield break;
            elapsed += Time.deltaTime; float t = elapsed / duration; t = t * t * (3f - 2f * t);
            warningMap.SetColor(cell, Color.Lerp(startColor, endColor, t)); yield return null;
        }
        if (warningMap.HasTile(cell)) warningMap.SetColor(cell, endColor);
    }
}