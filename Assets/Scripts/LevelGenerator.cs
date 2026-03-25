using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class LevelGenerator : MonoBehaviour
{
    public static LevelGenerator instance;

    [Header("Tilemaps")]
    public Tilemap groundMap;
    public Tilemap backgroundMap;
    // ==========================================
    // YENİ: DİKENLER İÇİN AYRI TİLEMAP
    // ==========================================
    public Tilemap hazardMap;

    // ==========================================
    // SCAFFOLD: ÇÖKEN PLATFORM TİLEMAP
    // ==========================================
    public Tilemap scaffoldMap;

    [Header("Tiles (Üst Zemin)")]
    public TileBase groundTile;
    public TileBase hazardTile;

    [Header("Scaffold (Çöken Platform)")]
    public TileBase scaffoldTile; // Üst kısım (tileMap_303)
    public TileBase lowerScaffoldTile; // Alt kısım (tileMap_101)
    [Range(0f, 1f)] public float scaffoldSpawnChance = 0.08f;

    [Header("Tiles (Arka Plan Sütun)")]
    public TileBase columnTile;

    [Header("Prefabs & Settings")]
    public GameObject playerPrefab;
    public GameObject meleeEnemyPrefab;
    public GameObject aoeEnemyPrefab;

    [Header("Boss Arenası Prefableri")]
    public GameObject bossPrefab;
    public GameObject totemPrefab;

    [Header("Shop Arena")]
    public GameObject shopDealerPrefab; // Optional — if null, creates a placeholder sprite

    [Header("Warlock Düşman")]
    public GameObject warlockEnemyPrefab;
    public int warlockStartLevel = 0; // Her bölümde çıkabilir
    [Range(0f, 1f)] public float warlockSpawnChance = 0.15f;
    private static float bossLegendaryMultiplier = 1f;  // Her bosstan sonra 2 ile çarpılır

    public static void ResetBossMultiplier() { bossLegendaryMultiplier = 1f; }

    public float CurrentEnemyHealth
    {
        get { 
            float baseHealth = 7f * bossLegendaryMultiplier;
            return baseHealth * Mathf.Pow(1.2f, RunManager.instance.currentLevel); 
        }
    }

    public int baseMapRadius = 3;
    public int aoeStartLevel = 1; // Her bölümde çıkabilir

    public List<Vector3Int> validCells = new List<Vector3Int>();
    public HashSet<Vector3Int> hazardCells = new HashSet<Vector3Int>();
    public HashSet<Vector3Int> scaffoldCells = new HashSet<Vector3Int>();

    private static readonly Vector3Int[] oddOffsets = { new Vector3Int(+1, 0, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0) };
    private static readonly Vector3Int[] evenOffsets = { new Vector3Int(+1, 0, 0), new Vector3Int(+1, +1, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(+1, -1, 0) };

    void Awake()
    {
        if (instance == null) instance = this;

        if (backgroundMap == null)
        {
            GameObject bgObj = GameObject.Find("BackgroundMap");
            if (bgObj != null) backgroundMap = bgObj.GetComponent<Tilemap>();
        }

        if (hazardMap == null)
        {
            GameObject hzObj = GameObject.Find("HazardMap");
            if (hzObj != null) hazardMap = hzObj.GetComponent<Tilemap>();
        }

        if (scaffoldMap == null)
        {
            GameObject scObj = GameObject.Find("ScaffoldMap");
            if (scObj != null) scaffoldMap = scObj.GetComponent<Tilemap>();
        }
    }

    void Start()
    {
        StartCoroutine(LevelBaslatmaSırası());
    }
    
    System.Collections.IEnumerator LevelBaslatmaSırası()
    {
        yield return null;

        // Map sistemi aktifse: ilk level'i üretme, haritayı göster
        Debug.Log($"[LEVEL-DEBUG] LevelBaslatmaSırası: MapManager.instance={MapManager.instance}, null={MapManager.instance == null}");
        if (MapManager.instance != null)
        {
            // ScreenFader'ın otomatik fade'lerini durdur — MapManager kontrol edecek
            if (ScreenFader.instance != null)
                ScreenFader.instance.StopAllCoroutines();

            MapManager.instance.StartNewRun();
            Debug.Log("Map sistemi aktif — harita gösteriliyor.");
        }
        else
        {
            // Legacy flow: direkt level üret
            GenerateNextLevel();

            if (ScreenFader.instance != null)
            {
                Debug.Log("Harita çizildi. Ekran karartması (veya aydınlanması) arka planda çalışıyor.");
            }
        }
    }

    public void GenerateNextLevel()
    {
        // Yeni oyun başlıyorsa (level 0) multiplier'ı reset et
        if (RunManager.instance.currentLevel == 0)
        {
            bossLegendaryMultiplier = 1f;
        }

        // Boss hezimetini algıla ve legendary multiplier'ı artır (sadece legacy modda)
        if (MapManager.instance == null && RunManager.instance.currentLevel > 0 && RunManager.instance.currentLevel % 5 == 1)
        {
            bossLegendaryMultiplier *= 2f;
            Debug.Log($"🏆 Boss yenildi! Legendary multiplier şimdi: {bossLegendaryMultiplier}x");
        }

        if (TurnManager.instance != null) TurnManager.instance.isLevelClearTriggered = false;

        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk != null) perk.OnLevelStart();
        }

        // Map sistemi aktifse boss kontrolü MapManager'a ait — burada tetikleme
        if (MapManager.instance == null && RunManager.instance.currentLevel > 0 && RunManager.instance.currentLevel % 5 == 0)
        {
            GenerateBossArena();
            return;
        }

        groundMap.ClearAllTiles();
        if (backgroundMap != null) backgroundMap.ClearAllTiles();
        if (hazardMap != null) hazardMap.ClearAllTiles();
        if (scaffoldMap != null) scaffoldMap.ClearAllTiles();
        if (ScaffoldManager.instance != null) ScaffoldManager.instance.ClearAll();

        validCells.Clear();
        hazardCells.Clear();
        scaffoldCells.Clear();

        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        TurnManager.instance.enemies.Clear();

        bool isPostBossLevel = RunManager.instance.currentLevel > 1 && RunManager.instance.currentLevel % 5 == 1;
        bool isEliteNode = RunManager.instance.currentNodeType == MapNodeType.EliteCombat;
        int currentRadius = Mathf.Min(baseMapRadius + (RunManager.instance.currentLevel / 8), baseMapRadius + 3); // Max radius cap
        int enemyCountToSpawn = Mathf.Min(3 + (RunManager.instance.currentLevel / 8), 6); // Max 6 enemy limit

        // Elite node'larda daha fazla ve güçlü düşman
        if (isEliteNode) enemyCountToSpawn += 2;

        for (int x = -currentRadius; x <= currentRadius; x++)
        {
            for (int y = -currentRadius; y <= currentRadius; y++)
            {
                if (Mathf.Abs(x + y) <= currentRadius)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    if (Random.value > 0.15f)
                    {
                        float roll = Random.value;

                        groundMap.SetTile(cell, groundTile);
                        groundMap.SetColor(cell, Color.white);

                        // Merkeze asla tehlikeli tile koyma
                        if (cell != Vector3Int.zero)
                        {
                            float hazardThreshold = scaffoldSpawnChance + 0.08f;
                            float scaffoldThreshold = hazardThreshold + scaffoldSpawnChance;

                            if (roll < hazardThreshold)
                            {
                                // Diken (Hazard) tile
                                // Komşusunda 2+ hazard varsa spawn etme → geçilmez duvarlar engellenir
                                int adjacentHazardCount = 0;
                                Vector3Int[] hazOffsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
                                foreach (var off in hazOffsets)
                                {
                                    if (hazardCells.Contains(cell + off)) adjacentHazardCount++;
                                }

                                if (adjacentHazardCount < 2)
                                {
                                    if (hazardMap != null) hazardMap.SetTile(cell, hazardTile);
                                    hazardCells.Add(cell);
                                }
                            }
                            else if (roll < scaffoldThreshold)
                            {
                                // Scaffold (Çöken Platform) tile
                                // Komşusunda zaten scaffold varsa spawn etme → kümeleme ve adacık oluşumunu engelle
                                bool hasAdjacentScaffold = false;
                                Vector3Int[] cellOffsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
                                foreach (var off in cellOffsets)
                                {
                                    if (scaffoldCells.Contains(cell + off)) { hasAdjacentScaffold = true; break; }
                                }

                                if (!hasAdjacentScaffold && scaffoldMap != null && scaffoldTile != null)
                                {
                                    groundMap.SetTile(cell, null);
                                    scaffoldMap.SetTile(cell, scaffoldTile);
                                    scaffoldCells.Add(cell);
                                }
                            }
                        }

                        validCells.Add(cell);
                    }
                }
            }
        }

        CleanUpDisconnectedIslands();
        EnsureSafeConnectivity();
        RemoveBottleneckHazards();

        // Minimum tile garantisi: düşman sayısı + hazard sayısı + 4 (oyuncu + hareket alanı)
        int minSafeTiles = enemyCountToSpawn + hazardCells.Count + 4;
        int safeTileCount = validCells.Count(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c));
        if (safeTileCount < minSafeTiles)
        {
            int needed = minSafeTiles - safeTileCount;
            List<Vector3Int> existingCells = new List<Vector3Int>(validCells);
            foreach (var existing in existingCells)
            {
                if (needed <= 0) break;
                Vector3Int[] offs = (existing.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offs)
                {
                    if (needed <= 0) break;
                    Vector3Int neighbor = existing + off;
                    if (!validCells.Contains(neighbor))
                    {
                        groundMap.SetTile(neighbor, groundTile);
                        groundMap.SetColor(neighbor, Color.white);
                        validCells.Add(neighbor);
                        needed--;
                    }
                }
            }
        }

        GenerateColumns();

        Vector3 worldCenter = groundMap.GetCellCenterWorld(Vector3Int.zero);
        List<Vector3Int> safePlayerSpawns = validCells.Where(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c)).ToList();

        if (safePlayerSpawns.Count == 0)
            safePlayerSpawns = validCells.Where(c => !hazardCells.Contains(c)).ToList();
        if (safePlayerSpawns.Count == 0)
            safePlayerSpawns = new List<Vector3Int>(validCells);
        if (safePlayerSpawns.Count == 0)
        {
            Vector3Int center = Vector3Int.zero;
            validCells.Add(center);
            safePlayerSpawns.Add(center);
        }

        Vector3Int playerStartCell = safePlayerSpawns.OrderBy(c => Vector3.Distance(groundMap.GetCellCenterWorld(c), worldCenter)).First();

        // ========================================================
        // KESİN ÇÖZÜM: OYUNCU DOĞDUĞU KAREYİ ZORLA TERTEMİZ YAP!
        // ========================================================
        hazardCells.Remove(playerStartCell);
        if (hazardMap != null) hazardMap.SetTile(playerStartCell, null);

        scaffoldCells.Remove(playerStartCell);
        if (scaffoldMap != null) scaffoldMap.SetTile(playerStartCell, null);

        groundMap.SetTile(playerStartCell, groundTile); // Altına sağlam zemin koy
        if (!validCells.Contains(playerStartCell)) validCells.Add(playerStartCell);

        TurnManager.instance.player.transform.position = groundMap.GetCellCenterWorld(playerStartCell);
        TurnManager.instance.player.StartKnockbackMovement(playerStartCell);
        validCells.Remove(playerStartCell);

        List<Vector3Int> spawnedEnemyCells = new List<Vector3Int>();
        int spawnedWarlockCount = 0;
        Vector3 playerWorldPos = groundMap.GetCellCenterWorld(playerStartCell);

        for (int i = 0; i < enemyCountToSpawn; i++)
        {
            if (validCells.Count == 0) break;

            List<Vector3Int> candidates = new List<Vector3Int>();
            int minHexDist = 3;

            while (candidates.Count == 0 && minHexDist >= 2)
            {
                int dist = minHexDist;
                candidates = validCells.FindAll(cell =>
                    !hazardCells.Contains(cell) &&
                    !scaffoldCells.Contains(cell) &&
                    HexDistance(cell, playerStartCell) >= dist
                );

                candidates.RemoveAll(cell =>
                    spawnedEnemyCells.Any(spawned => HexDistance(cell, spawned) < 2)
                );

                if (candidates.Count == 0)
                    minHexDist--;
            }

            Vector3Int bestSpawnCell;

            if (candidates.Count == 0)
            {
                var safeCells = validCells.Where(c =>
                    !hazardCells.Contains(c) &&
                    !scaffoldCells.Contains(c) &&
                    HexDistance(c, playerStartCell) >= 2
                ).ToList();
                if (safeCells.Count == 0)
                    safeCells = validCells.Where(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c)).ToList();
                if (safeCells.Count == 0)
                    safeCells = validCells.Where(c => !hazardCells.Contains(c)).ToList();
                bestSpawnCell = safeCells.Count > 0 ? safeCells[Random.Range(0, safeCells.Count)] : validCells[0];
            }
            else if (spawnedEnemyCells.Count == 0)
            {
                bestSpawnCell = candidates.OrderByDescending(c => Vector3.Distance(groundMap.GetCellCenterWorld(c), playerWorldPos)).First();
            }
            else
            {
                float bestScore = -float.MaxValue;
                bestSpawnCell = candidates[0];

                foreach (var candidate in candidates)
                {
                    Vector3 candidateDir = (groundMap.GetCellCenterWorld(candidate) - playerWorldPos).normalized;
                    float maxDotProduct = -1f;

                    foreach (var spawned in spawnedEnemyCells)
                    {
                        Vector3 spawnedDir = (groundMap.GetCellCenterWorld(spawned) - playerWorldPos).normalized;
                        float dot = Vector3.Dot(candidateDir, spawnedDir);
                        if (dot > maxDotProduct) maxDotProduct = dot;
                    }

                    float score = (-maxDotProduct * 10f) + Vector3.Distance(groundMap.GetCellCenterWorld(candidate), playerWorldPos);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSpawnCell = candidate;
                    }
                }
            }

            validCells.Remove(bestSpawnCell);
            spawnedEnemyCells.Add(bestSpawnCell);

            Vector3 spawnPos = groundMap.GetCellCenterWorld(bestSpawnCell);

            GameObject prefabToSpawn = meleeEnemyPrefab;

            if (warlockEnemyPrefab != null && spawnedWarlockCount < 3 &&
                (RunManager.instance.currentLevel >= warlockStartLevel || RunManager.instance.currentLevel == 0))
            {
                float effectiveChance = (RunManager.instance.currentLevel == 0) ? 0.50f : warlockSpawnChance;
                if (Random.value < effectiveChance)
                {
                    prefabToSpawn = warlockEnemyPrefab;
                }
            }

            if (prefabToSpawn == meleeEnemyPrefab && RunManager.instance.currentLevel >= aoeStartLevel)
            {
                if (Random.value < 0.30f && aoeEnemyPrefab != null)
                {
                    prefabToSpawn = aoeEnemyPrefab;
                }
            }

            GameObject newEnemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            if (prefabToSpawn == warlockEnemyPrefab) spawnedWarlockCount++;
            EnemyMovement enemyAI = newEnemyObj.GetComponent<EnemyMovement>();
            enemyAI.groundMap = this.groundMap;

            float randomMultiplier = Random.Range(0.8f, 1.25f);

            // ========================================================
            // ELITE DÜŞMAN: Elite node'da ilk düşman garanti elite, geri kalan %20
            // Normal node'larda: %10 şans (level 6+)
            // ========================================================
            bool makeElite = false;
            if (isEliteNode)
            {
                // Elite node: ilk düşman garanti, geri kalanı %20
                makeElite = (i == 0) || (Random.value < 0.20f);
            }

            if (makeElite)
            {
                randomMultiplier *= 2.0f;
                enemyAI.isElite = true;
                newEnemyObj.name = "ELITE " + newEnemyObj.name;
            }
            // ========================================================

            float postBossMultiplier = isPostBossLevel ? 2.4f : 1f;
            float eliteNodeMultiplier = isEliteNode ? 1.5f : 1f;
            // Dikkat: bossLegendaryMultiplier zaten CurrentEnemyHealth'te uygulandığı için postBossMultiplier KULLANMA!
            int finalHP = Mathf.RoundToInt(CurrentEnemyHealth * randomMultiplier * eliteNodeMultiplier);
            enemyAI.health.maxHP = Mathf.Max(1, finalHP);
            enemyAI.health.currentHP = enemyAI.health.maxHP;

            enemyAI.health.updateHealth();
            TurnManager.instance.RegisterEnemy(enemyAI);
            StartCoroutine(enemyAI.FadeSpawnCoroutine());
        }

        TurnManager.instance.isPlayerTurn = true;
        TurnManager.instance.hasAttackedThisTurn = false;

        // Reset remaining moves for the new level so perks like ReflexFiber start fresh
        if (RunManager.instance != null)
        {
            RunManager.instance.remainingMoves = RunManager.instance.extraMovesPerTurn;
        }

        TurnManager.instance.player.UpdateHighlights();

        TurnManager.instance.Invoke("LockAllEnemyIntents", 0.1f);

        Debug.Log($"🗺️ Level {RunManager.instance.currentLevel} oluşturuldu!");
    }

    // ========================================================
    // YENİ: ELİTE DÜŞMAN AURASI İÇİN NABIZ EFEKTİ (PULSE)
    // ========================================================
    private IEnumerator PulseAura(Transform auraTransform)
    {
        if (auraTransform == null) yield break;
        Vector3 baseScale = auraTransform.localScale;
        
        while (auraTransform != null)
        {
            float pulse = Mathf.PingPong(Time.time * 2f, 0.2f); // 0 ile 0.2 arası gidip gelir
            auraTransform.localScale = baseScale + new Vector3(pulse, pulse, 0f);
            yield return null;
        }
    }
    // ========================================================

    public void GenerateBossArena()
    {
        Debug.Log("🔥 BOSS BÖLÜMÜ YÜKLENİYOR! 🔥");

        // isLevelClearTriggered reset — yoksa boss ölünce WaitAndTriggerLevelClear tetiklenmez
        if (TurnManager.instance != null) TurnManager.instance.isLevelClearTriggered = false;
        groundMap.ClearAllTiles();
        if (backgroundMap != null) backgroundMap.ClearAllTiles();
        if (hazardMap != null) hazardMap.ClearAllTiles();
        if (scaffoldMap != null) scaffoldMap.ClearAllTiles();
        if (ScaffoldManager.instance != null) ScaffoldManager.instance.ClearAll();
        validCells.Clear();
        hazardCells.Clear();
        scaffoldCells.Clear();

        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        TurnManager.instance.enemies.Clear();

        int arenaRadius = Mathf.Min(baseMapRadius + 1 + (RunManager.instance.currentLevel / 10), baseMapRadius + 4); // Boss arena radius cap

        for (int x = -arenaRadius; x <= arenaRadius; x++)
        {
            for (int y = -arenaRadius; y <= arenaRadius; y++)
            {
                if (Mathf.Abs(x + y) <= arenaRadius)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    if (Random.value > 0.05f)
                    {
                        float roll = Random.value;
                        
                        // Zemin banko konuluyor
                        groundMap.SetTile(cell, groundTile);
                        groundMap.SetColor(cell, Color.white);

                        // Boss arenasında merkeze değil de rastgele bir yerlere sadece diken (hazard) serpiştiriyoruz. Scaffold YÖK EDİLDİ!
                        if (roll < 0.08f && Vector3Int.zero != cell)
                        {
                            if (hazardMap != null) hazardMap.SetTile(cell, hazardTile);
                            hazardCells.Add(cell);
                        }

                        validCells.Add(cell);
                    }
                }
            }
        }

        CleanUpDisconnectedIslands();
        EnsureSafeConnectivity();
        RemoveBottleneckHazards();
        GenerateColumns();

        Vector3 worldCenter = groundMap.GetCellCenterWorld(Vector3Int.zero);
        List<Vector3Int> safePlayerSpawns = validCells.Where(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c)).ToList();

        if (safePlayerSpawns.Count == 0)
            safePlayerSpawns = validCells.Where(c => !hazardCells.Contains(c)).ToList();
        if (safePlayerSpawns.Count == 0)
            safePlayerSpawns = new List<Vector3Int>(validCells);
        if (safePlayerSpawns.Count == 0)
        {
            Vector3Int center = Vector3Int.zero;
            validCells.Add(center);
            safePlayerSpawns.Add(center);
        }

        Vector3Int playerStartCell = safePlayerSpawns.OrderBy(c => Vector3.Distance(groundMap.GetCellCenterWorld(c), worldCenter)).First();

        // ========================================================
        // KESİN ÇÖZÜM: BOSS ARENASINDA DA OYUNCU DOĞDUĞU KAREYİ TERTEMİZ YAP!
        // ========================================================
        hazardCells.Remove(playerStartCell);
        if (hazardMap != null) hazardMap.SetTile(playerStartCell, null);

        scaffoldCells.Remove(playerStartCell);
        if (scaffoldMap != null) scaffoldMap.SetTile(playerStartCell, null);

        groundMap.SetTile(playerStartCell, groundTile); // Altına sağlam zemin koy
        if (!validCells.Contains(playerStartCell)) validCells.Add(playerStartCell);

        TurnManager.instance.player.transform.position = groundMap.GetCellCenterWorld(playerStartCell);
        TurnManager.instance.player.StartKnockbackMovement(playerStartCell);
        validCells.Remove(playerStartCell);

        List<Vector3Int> availableSpawnCells = validCells.Where(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c)).ToList();

        for (int i = 0; i < availableSpawnCells.Count; i++)
        {
            Vector3Int temp = availableSpawnCells[i];
            int r = Random.Range(i, availableSpawnCells.Count);
            availableSpawnCells[i] = availableSpawnCells[r];
            availableSpawnCells[r] = temp;
        }

        availableSpawnCells = availableSpawnCells.OrderByDescending(c => Vector3.Distance(groundMap.GetCellCenterWorld(c), worldCenter)).ToList();

        EnemyMovement spawnedBossAI = null;
        if (bossPrefab != null && availableSpawnCells.Count > 0)
        {
            Vector3Int bossCell = availableSpawnCells[0];
            Vector3 bossPos = groundMap.GetCellCenterWorld(bossCell);

            GameObject bossObj = Instantiate(bossPrefab, bossPos, Quaternion.identity);
            EnemyMovement bossAI = bossObj.GetComponent<EnemyMovement>();

            // Boss sahnesinde legendary multiplier'ı uyguLAMA, ama normal düşmanın 3 katı HP'ye sahip
            float bossHealth = LevelGenerator.instance.CurrentEnemyHealth * 2f;
            bossAI.health.maxHP = Mathf.RoundToInt(bossHealth);
            bossAI.health.currentHP = bossAI.health.maxHP;
            bossAI.health.updateHealth();

            StartCoroutine(bossAI.FadeSpawnCoroutine());
            spawnedBossAI = bossAI;

            availableSpawnCells.RemoveAt(0);
        }

        if (totemPrefab != null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (availableSpawnCells.Count == 0) break;

                int index = (i * (availableSpawnCells.Count / 4));
                Vector3Int totemCell = availableSpawnCells[index];
                Vector3 totemPos = groundMap.GetCellCenterWorld(totemCell);

                GameObject totemObj = Instantiate(totemPrefab, totemPos, Quaternion.identity);
                EnemyMovement totemAI = totemObj.GetComponent<EnemyMovement>();

                totemAI.health.maxHP = 1;
                totemAI.health.currentHP = 1;
                totemAI.health.updateHealth();

                StartCoroutine(totemAI.FadeSpawnCoroutine());

                availableSpawnCells.RemoveAt(index);
            }
        }

        TurnManager.instance.hasAttackedThisTurn = false;

        if (spawnedBossAI != null && BossIntroSequence.instance != null)
        {
            StartCoroutine(DelayedBossIntro(spawnedBossAI));
        }
        else
        {
            TurnManager.instance.isPlayerTurn = true;
            TurnManager.instance.player.UpdateHighlights();
        }
    }

    private IEnumerator DelayedBossIntro(EnemyMovement boss)
    {
        yield return new WaitForSeconds(0.8f);
        BossIntroSequence.instance.PlayIntro(boss);
    }

    private void GenerateColumns()
    {
        if (backgroundMap == null) return;
        backgroundMap.ClearAllTiles();

        foreach (var cell in validCells)
        {
            if (scaffoldCells.Contains(cell))
            {
                if (lowerScaffoldTile != null)
                {
                    backgroundMap.SetTile(cell, lowerScaffoldTile);
                }
            }
            else
            {
                if (columnTile != null)
                {
                    backgroundMap.SetTile(cell, columnTile);
                }
            }
        }
    }

    private void EnsureSafeConnectivity()
    {
        // Güvenli hücreler: hazard ve scaffold OLMAYAN hücreler.
        // Scaffold çökebilir → bağlantıda güvenilmez köprü sayılır.
        // Scaffold hariç tutularak, sadece kalıcı zemin üzerinden bağlantı kontrol edilir.
        List<Vector3Int> safeCells = validCells.Where(c => !hazardCells.Contains(c) && !scaffoldCells.Contains(c)).ToList();
        if (safeCells.Count == 0) return;

        List<List<Vector3Int>> safeIslands = new List<List<Vector3Int>>();
        HashSet<Vector3Int> unvisitedSafe = new HashSet<Vector3Int>(safeCells);

        while (unvisitedSafe.Count > 0)
        {
            Vector3Int startCell = unvisitedSafe.First();
            List<Vector3Int> currentIsland = new List<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();

            queue.Enqueue(startCell);
            unvisitedSafe.Remove(startCell);
            currentIsland.Add(startCell);

            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;

                foreach (var off in offsets)
                {
                    Vector3Int neighbor = curr + off;
                    if (unvisitedSafe.Contains(neighbor))
                    {
                        unvisitedSafe.Remove(neighbor);
                        queue.Enqueue(neighbor);
                        currentIsland.Add(neighbor);
                    }
                }
            }
            safeIslands.Add(currentIsland);
        }

        List<Vector3Int> largestSafeIsland = safeIslands[0];
        foreach (var island in safeIslands)
        {
            if (island.Count > largestSafeIsland.Count) largestSafeIsland = island;
        }

        HashSet<Vector3Int> mainSafeSet = new HashSet<Vector3Int>(largestSafeIsland);
        List<Vector3Int> cellsToRemove = new List<Vector3Int>();

        foreach (var cell in validCells)
        {
            if (hazardCells.Contains(cell))
            {
                // Diken: ana adaya komşuysa kalsın
                bool touchesMain = false;
                Vector3Int[] offsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offsets)
                {
                    if (mainSafeSet.Contains(cell + off)) { touchesMain = true; break; }
                }
                if (!touchesMain) cellsToRemove.Add(cell);
            }
            else if (scaffoldCells.Contains(cell))
            {
                // Scaffold: en az 1 komşusu ana adadaysa kalsın, yoksa sil.
                // Ayrıca scaffold'un sağlam zemin ile bağlantı kopması yaratmamasını garanti et.
                bool touchesMain = false;
                Vector3Int[] offsets = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offsets)
                {
                    if (mainSafeSet.Contains(cell + off)) { touchesMain = true; break; }
                }
                if (!touchesMain) cellsToRemove.Add(cell);
            }
            else
            {
                // Normal zemin: ana adada değilse sil
                if (!mainSafeSet.Contains(cell)) cellsToRemove.Add(cell);
            }
        }

        foreach (var cell in cellsToRemove)
        {
            groundMap.SetTile(cell, null);
            if (hazardMap != null) hazardMap.SetTile(cell, null);
            if (scaffoldMap != null) scaffoldMap.SetTile(cell, null);
            validCells.Remove(cell);
            hazardCells.Remove(cell);
            scaffoldCells.Remove(cell);
        }
    }

    /// <summary>
    /// Hazard hücreleri darboğaz oluşturuyorsa kaldır.
    /// Bir hazard kaldırıldığında, güvenli hücrelerin bağlantısı artarsa o hazard darboğazdır.
    /// </summary>
    private void RemoveBottleneckHazards()
    {
        if (hazardCells.Count == 0) return;

        // Tekrarlı kaldırma: her iterasyonda yeni bottleneck'ler açığa çıkabilir
        const int maxIterations = 20;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            int baseComponents = CountSafeComponents();
            if (baseComponents <= 1) return;

            List<Vector3Int> toRemove = new List<Vector3Int>();
            foreach (var hCell in new List<Vector3Int>(hazardCells))
            {
                hazardCells.Remove(hCell);
                int newComponents = CountSafeComponents();
                hazardCells.Add(hCell);

                if (newComponents < baseComponents)
                    toRemove.Add(hCell);
            }

            // Tek tek kaldırarak bağlantı sağlanamadıysa,
            // kopuk bölgeler arasındaki en yakın hazard'ları BFS ile bul ve kaldır
            if (toRemove.Count == 0 && baseComponents > 1)
            {
                toRemove = FindHazardsBridgingIslands();
            }

            if (toRemove.Count == 0) break;

            foreach (var cell in toRemove)
            {
                hazardCells.Remove(cell);
                if (hazardMap != null) hazardMap.SetTile(cell, null);
            }
        }
    }

    /// <summary>
    /// Kopuk güvenli adalar arasında hazard hücreleri üzerinden en kısa yolu bulur
    /// ve o yoldaki hazard'ları kaldırır.
    /// </summary>
    private List<Vector3Int> FindHazardsBridgingIslands()
    {
        List<Vector3Int> result = new List<Vector3Int>();

        // Güvenli adaları bul
        List<HashSet<Vector3Int>> islands = new List<HashSet<Vector3Int>>();
        HashSet<Vector3Int> unvisited = new HashSet<Vector3Int>();
        foreach (var c in validCells)
            if (!hazardCells.Contains(c) && !scaffoldCells.Contains(c))
                unvisited.Add(c);

        while (unvisited.Count > 0)
        {
            HashSet<Vector3Int> island = new HashSet<Vector3Int>();
            Queue<Vector3Int> q = new Queue<Vector3Int>();
            Vector3Int start = unvisited.First();
            q.Enqueue(start); unvisited.Remove(start); island.Add(start);
            while (q.Count > 0)
            {
                Vector3Int curr = q.Dequeue();
                Vector3Int[] offs = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offs)
                {
                    Vector3Int nb = curr + off;
                    if (unvisited.Contains(nb)) { unvisited.Remove(nb); island.Add(nb); q.Enqueue(nb); }
                }
            }
            islands.Add(island);
        }

        if (islands.Count <= 1) return result;

        // En büyük adadan diğer adalara hazard hücreleri üzerinden BFS yap
        HashSet<Vector3Int> mainIsland = islands.OrderByDescending(i => i.Count).First();
        HashSet<Vector3Int> otherSafeCells = new HashSet<Vector3Int>();
        foreach (var island in islands)
            if (island != mainIsland)
                foreach (var c in island) otherSafeCells.Add(c);

        // BFS: mainIsland kenarından başla, hazard hücreleri üzerinden ilerle
        Queue<Vector3Int> bfs = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();

        foreach (var cell in mainIsland)
        {
            Vector3Int[] offs = (cell.y % 2 != 0) ? evenOffsets : oddOffsets;
            foreach (var off in offs)
            {
                Vector3Int nb = cell + off;
                if (hazardCells.Contains(nb) && !cameFrom.ContainsKey(nb))
                {
                    bfs.Enqueue(nb);
                    cameFrom[nb] = cell; // parent is a safe cell (sentinel)
                }
            }
        }

        Vector3Int bridgeEnd = Vector3Int.zero;
        bool found = false;
        while (bfs.Count > 0 && !found)
        {
            Vector3Int curr = bfs.Dequeue();
            Vector3Int[] offs = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
            foreach (var off in offs)
            {
                Vector3Int nb = curr + off;
                if (otherSafeCells.Contains(nb)) { bridgeEnd = curr; found = true; break; }
                if (hazardCells.Contains(nb) && !cameFrom.ContainsKey(nb))
                {
                    cameFrom[nb] = curr;
                    bfs.Enqueue(nb);
                }
            }
        }

        if (found)
        {
            // Yolu geri izle, sadece hazard olan hücreleri topla
            Vector3Int step = bridgeEnd;
            while (cameFrom.ContainsKey(step) && hazardCells.Contains(step))
            {
                result.Add(step);
                step = cameFrom[step];
            }
        }

        return result;
    }

    /// <summary>Güvenli (hazard/scaffold olmayan) hücrelerin bağlı bileşen sayısını döner.</summary>
    private int CountSafeComponents()
    {
        HashSet<Vector3Int> unvisited = new HashSet<Vector3Int>();
        foreach (var c in validCells)
        {
            if (!hazardCells.Contains(c) && !scaffoldCells.Contains(c))
                unvisited.Add(c);
        }

        int components = 0;
        while (unvisited.Count > 0)
        {
            components++;
            Vector3Int start = unvisited.First();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;
                foreach (var off in offsets)
                {
                    Vector3Int neighbor = curr + off;
                    if (unvisited.Contains(neighbor))
                    {
                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
        return components;
    }

    private void CleanUpDisconnectedIslands()
    {
        if (validCells.Count == 0) return;

        List<List<Vector3Int>> allIslands = new List<List<Vector3Int>>();
        HashSet<Vector3Int> unvisited = new HashSet<Vector3Int>(validCells);

        while (unvisited.Count > 0)
        {
            Vector3Int startCell = unvisited.First();
            List<Vector3Int> currentIsland = new List<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();

            queue.Enqueue(startCell);
            unvisited.Remove(startCell);
            currentIsland.Add(startCell);

            while (queue.Count > 0)
            {
                Vector3Int curr = queue.Dequeue();
                Vector3Int[] offsets = (curr.y % 2 != 0) ? evenOffsets : oddOffsets;

                foreach (var off in offsets)
                {
                    Vector3Int neighbor = curr + off;
                    if (unvisited.Contains(neighbor))
                    {
                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                        currentIsland.Add(neighbor);
                    }
                }
            }
            allIslands.Add(currentIsland);
        }

        List<Vector3Int> largestIsland = allIslands[0];
        foreach (var island in allIslands)
        {
            if (island.Count > largestIsland.Count)
            {
                largestIsland = island;
            }
        }

        List<Vector3Int> toRemove = new List<Vector3Int>();
        foreach (var cell in validCells)
        {
            if (!largestIsland.Contains(cell))
            {
                groundMap.SetTile(cell, null);
                if (hazardMap != null) hazardMap.SetTile(cell, null); 
                if (scaffoldMap != null) scaffoldMap.SetTile(cell, null); 
                toRemove.Add(cell);
            }
        }

        foreach (var c in toRemove)
        {
            validCells.Remove(c);
            hazardCells.Remove(c);
            scaffoldCells.Remove(c);
        }
    }

    private int HexDistance(Vector3Int a, Vector3Int b)
    {
        int ax = a.x - (a.y - (a.y & 1)) / 2;
        int az = a.y;
        int ay = -ax - az;
        int bx = b.x - (b.y - (b.y & 1)) / 2;
        int bz = b.y;
        int by = -bx - bz;
        return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(ay - by), Mathf.Abs(az - bz));
    }

    // ═══════════════════════════════════════════
    // SHOP ARENA — Büyük hexagon, dealer arkada, itemler önünde
    // ═══════════════════════════════════════════

    /// <summary>
    /// Generates a large hexagon arena (radius 3) for the shop scene.
    /// Dealer (capsule) sits at the back (top). 3 item hexes are in front of dealer.
    /// Exit hex is on the right edge. Player spawns at the bottom.
    /// </summary>
    public ShopDealer GenerateShopArena()
    {
        groundMap.ClearAllTiles();
        if (backgroundMap != null) backgroundMap.ClearAllTiles();
        if (hazardMap != null) hazardMap.ClearAllTiles();
        if (scaffoldMap != null) scaffoldMap.ClearAllTiles();
        if (ScaffoldManager.instance != null) ScaffoldManager.instance.ClearAll();

        validCells.Clear();
        hazardCells.Clear();
        scaffoldCells.Clear();

        // Destroy all enemies (shop has none)
        if (TurnManager.instance != null)
        {
            foreach (var enemy in TurnManager.instance.enemies)
                if (enemy != null) Destroy(enemy.gameObject);
            TurnManager.instance.enemies.Clear();
        }

        // ─── Big hexagon (radius 3) ───
        int shopRadius = 3;
        for (int x = -shopRadius; x <= shopRadius; x++)
        {
            for (int y = -shopRadius; y <= shopRadius; y++)
            {
                if (Mathf.Abs(x + y) <= shopRadius)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    groundMap.SetTile(cell, groundTile);
                    groundMap.SetColor(cell, Color.white);
                    validCells.Add(cell);
                }
            }
        }

        GenerateColumns();

        // ─── Key cells ───
        // Dealer sits at top-center (visual only — remove from walkable)
        Vector3Int dealerVisualCell = new Vector3Int(0, 3, 0);

        // Item hexes: row in front of dealer (row y=2, side by side)
        Vector3Int[] itemCells = new Vector3Int[]
        {
            new Vector3Int(-1, 2, 0),  // Item 0 (left)
            new Vector3Int(0, 2, 0),   // Item 1 (center)
            new Vector3Int(1, 2, 0),   // Item 2 (right)
        };

        // Exit hex: right edge of the hexagon
        Vector3Int exitCell = new Vector3Int(3, 0, 0);

        // Player spawns at bottom
        Vector3Int playerCell = new Vector3Int(0, -3, 0);

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.transform.position = groundMap.GetCellCenterWorld(playerCell);
            TurnManager.instance.player.StartKnockbackMovement(playerCell);
            TurnManager.instance.isPlayerTurn = true;
            TurnManager.instance.player.UpdateHighlights();
        }

        // ─── Spawn dealer (capsule) at top ───
        if (ShopDealer.instance != null)
            Destroy(ShopDealer.instance.gameObject);

        Vector3 dealerWorldPos = groundMap.GetCellCenterWorld(dealerVisualCell);

        GameObject dealerGO;
        if (shopDealerPrefab != null)
        {
            dealerGO = Instantiate(shopDealerPrefab, dealerWorldPos, Quaternion.identity);
        }
        else
        {
            // Capsule placeholder dealer
            dealerGO = new GameObject("ShopDealer");
            dealerGO.transform.position = dealerWorldPos;
            SpriteRenderer sr = dealerGO.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCapsuleSprite();
            sr.color = new Color(1f, 0.85f, 0.2f, 1f);
            sr.sortingOrder = 5;
        }

        ShopDealer dealer = dealerGO.GetComponent<ShopDealer>();
        if (dealer == null)
            dealer = dealerGO.AddComponent<ShopDealer>();

        dealer.dealerCell = dealerVisualCell;
        dealer.SetupShopArena(itemCells, exitCell);

        return dealer;
    }

    /// <summary>
    /// Creates a capsule-shaped sprite for the dealer NPC placeholder.
    /// </summary>
    private Sprite CreateCapsuleSprite()
    {
        int w = 24, h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[w * h];
        float halfW = w / 2f;
        float capRadius = w / 2f; // Semicircle radius = half width

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inside = false;

                if (y < capRadius) // Bottom semicircle
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(halfW - 0.5f, capRadius));
                    inside = dist <= capRadius;
                }
                else if (y >= h - capRadius) // Top semicircle
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(halfW - 0.5f, h - capRadius - 1));
                    inside = dist <= capRadius;
                }
                else // Middle rectangle
                {
                    inside = x >= 0 && x < w;
                }

                pixels[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>
    /// Creates a simple white circle sprite for placeholder NPCs.
    /// </summary>
    private Sprite CreatePlaceholderSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        Vector2 center = new Vector2(15.5f, 15.5f);
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % 32;
            int y = i / 32;
            float dist = Vector2.Distance(new Vector2(x, y), center);
            pixels[i] = dist < 14f ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }
}