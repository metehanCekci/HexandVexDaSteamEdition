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
    public int warlockStartLevel = 6; // İlk bosstan sonra (level 6+)
    [Range(0f, 1f)] public float warlockSpawnChance = 0.10f;
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
    public int aoeStartLevel = 3;

    private List<Vector3Int> validCells = new List<Vector3Int>();
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
        int currentRadius = baseMapRadius + (RunManager.instance.currentLevel / 6);
        int enemyCountToSpawn = 3 + (RunManager.instance.currentLevel / 3);

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

                        // Merkeze asla diken koyma
                        if (roll < scaffoldSpawnChance + 0.10f && cell != Vector3Int.zero)
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
            EnemyAI enemyAI = newEnemyObj.GetComponent<EnemyAI>();
            enemyAI.groundMap = this.groundMap;

            float randomMultiplier = Random.Range(0.8f, 1.25f);

            // ========================================================
            // ELITE DÜŞMAN GÖRSELLİĞİ: "Altın Aura" ve Büyüme
            // ========================================================
            if (Random.value < 0.10f && RunManager.instance.currentLevel >= 6)
            {
                randomMultiplier *= 2.0f;
                newEnemyObj.name = "ELITE " + newEnemyObj.name;
                
                SpriteRenderer eliteSpriteRenderer = newEnemyObj.GetComponent<SpriteRenderer>();
                if (eliteSpriteRenderer == null) eliteSpriteRenderer = newEnemyObj.GetComponentInChildren<SpriteRenderer>();

                if (eliteSpriteRenderer != null)
                {
                    // 1. Karakterin kendisini parlat (Sarı/Altın Tonu)
                    eliteSpriteRenderer.color = new Color(1f, 0.85f, 0.2f, 1f);                 
                    
                }
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

        int arenaRadius = baseMapRadius + 1 + (RunManager.instance.currentLevel / 10);

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
                        if (roll < 0.10f && Vector3Int.zero != cell)
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

        EnemyAI spawnedBossAI = null;
        if (bossPrefab != null && availableSpawnCells.Count > 0)
        {
            Vector3Int bossCell = availableSpawnCells[0];
            Vector3 bossPos = groundMap.GetCellCenterWorld(bossCell);

            GameObject bossObj = Instantiate(bossPrefab, bossPos, Quaternion.identity);
            EnemyAI bossAI = bossObj.GetComponent<EnemyAI>();

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
                EnemyAI totemAI = totemObj.GetComponent<EnemyAI>();

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

    private IEnumerator DelayedBossIntro(EnemyAI boss)
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
        List<Vector3Int> safeCells = validCells.Where(c => !hazardCells.Contains(c)).ToList();
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
    // SHOP ARENA — Elmas şekli (1-2-3-2-1), ortada dealer
    // ═══════════════════════════════════════════

    /// <summary>
    /// Generates a diamond-shaped hex arena for the shop scene.
    /// Layout (top to bottom): 1-2-3-2-1 tiles.
    /// Player spawns at the bottom single tile.
    /// Center of the 3-tile middle row is reserved for the dealer NPC.
    /// Returns the dealer cell world position.
    /// </summary>
    public Vector3 GenerateShopArena()
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

        // Diamond shape: 1-2-3-2-1 (top to bottom)
        // Row y=+2 (top):    1 tile  → (0,2)
        // Row y=+1:          2 tiles → (0,1), (1,1)   [y=1 is odd row]
        // Row y= 0 (middle): 3 tiles → (-1,0), (0,0), (1,0)
        // Row y=-1:          2 tiles → (-1,-1), (0,-1) [y=-1 is odd row]
        // Row y=-2 (bottom): 1 tile  → (0,-2)

        Vector3Int[] diamondCells = new Vector3Int[]
        {
            // Top (1)
            new Vector3Int(0, 2, 0),
            // Row 1 (2)
            new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            // Middle (3) — dealer at center (0,0)
            new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0),
            // Row -1 (2)
            new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0),
            // Bottom (1) — player spawn
            new Vector3Int(0, -2, 0),
        };

        foreach (var cell in diamondCells)
        {
            groundMap.SetTile(cell, groundTile);
            groundMap.SetColor(cell, Color.white);
            validCells.Add(cell);
        }

        GenerateColumns();

        // Player spawns at bottom single tile
        Vector3Int playerCell = new Vector3Int(0, -2, 0);

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.transform.position = groundMap.GetCellCenterWorld(playerCell);
            TurnManager.instance.player.StartKnockbackMovement(playerCell);
            TurnManager.instance.isPlayerTurn = true;
            TurnManager.instance.player.UpdateHighlights();
        }

        // Spawn dealer at center of the 3-tile middle row
        Vector3Int dealerCell = Vector3Int.zero;
        Vector3 dealerWorldPos = groundMap.GetCellCenterWorld(dealerCell);

        // Destroy old dealer if any
        if (ShopDealer.instance != null)
            Destroy(ShopDealer.instance.gameObject);

        GameObject dealerGO;
        if (shopDealerPrefab != null)
        {
            dealerGO = Instantiate(shopDealerPrefab, dealerWorldPos, Quaternion.identity);
        }
        else
        {
            // Placeholder dealer — simple colored sprite
            dealerGO = new GameObject("ShopDealer");
            dealerGO.transform.position = dealerWorldPos;
            SpriteRenderer sr = dealerGO.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePlaceholderSprite();
            sr.color = new Color(0.2f, 0.8f, 1f, 1f);
            sr.sortingOrder = 5;
        }

        ShopDealer dealer = dealerGO.GetComponent<ShopDealer>();
        if (dealer == null)
            dealer = dealerGO.AddComponent<ShopDealer>();
        dealer.dealerCell = dealerCell;

        return dealerWorldPos;
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