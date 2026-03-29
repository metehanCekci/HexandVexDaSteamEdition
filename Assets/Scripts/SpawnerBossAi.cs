using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SpawnerBossAI : MonoBehaviour
{
    public static SpawnerBossAI instance;

    [Header("Boss Ayarları")]
    public int activeTotems = 4;
    public bool isShielded = true;
    public GameObject shieldVisual;      // Prefab veya sahne objesi — her ikisi de çalışır
    private GameObject shieldInstance;   // Runtime'da spawn edilen instance

    // ==========================================
    // YENİ: YILDIRIM VEYA VURUŞ EFEKTİ PREFABI
    // ==========================================
    [Header("Vuruş Efektleri")]
    public GameObject lightningStrikePrefab; 

    [Header("AoE Saldırı Ayarları (4 Adımlı Döngü)")]
    public int aoeCycleStep = 0; 
    public bool isAoEWarningActive = false; 
    public bool readyToExplodeThisTurn = false; // YENİ: Patlamayı bir tur bekleten kilit!
    private List<Vector3Int> aoeWarningCells = new List<Vector3Int>();

    [Header("Summon Ayarları")]
    public List<EnemyMovement> summonedMinions = new List<EnemyMovement>();
    
    private bool isTransitioning = false; 
    private bool isSummoning = false; 
    private int previousHP; 

    private Tilemap groundMap;
    private Tilemap bossWarningMap;
    private int arenaRadius;
    
    [Header("BOSS'A ÖZEL UYARI KAROSU (BUNU ATAMALISIN!)")]
    public TileBase warningTile; 
    
    private EnemyMovement myEnemyMovement;

    void Awake() { instance = this; }

    void Start()
    {
        myEnemyMovement = GetComponent<EnemyMovement>();
        groundMap = LevelGenerator.instance.groundMap;
        arenaRadius = LevelGenerator.instance.baseMapRadius + 1 + (RunManager.instance.currentLevel / 10);

        GameObject warnObj = GameObject.Find("BossWarningMap");
        if (warnObj != null) 
        {
            bossWarningMap = warnObj.GetComponent<Tilemap>();
        }
        else
        {
            if (TurnManager.instance != null) bossWarningMap = TurnManager.instance.warningMap;
        }

        if (warningTile == null && myEnemyMovement != null)
            warningTile = myEnemyMovement.warningTile;
        if (warningTile == null && TurnManager.instance != null)
            warningTile = TurnManager.instance.warningTile;

        activeTotems = 4;
        isShielded = true;
        if (shieldVisual != null)
        {
            if (shieldVisual.scene.name == null || shieldVisual.scene.name == "")
            {
                shieldInstance = Instantiate(shieldVisual, transform.position, Quaternion.identity, transform);
                shieldInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                shieldInstance = shieldVisual;
                shieldInstance.SetActive(true);
            }
            shieldInstance.transform.localScale = Vector3.one * 3f;
            shieldInstance.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            StartCoroutine(ShieldPulseLoop());
        }

        previousHP = myEnemyMovement.health.maxHP;

        StartCoroutine(InitialSpawnDelay());
    }

    private IEnumerator InitialSpawnDelay()
    {
        isTransitioning = true; 
        yield return new WaitForSeconds(0.5f); 
        int desiredMinionCount = GetMaxMinionLimit();
        yield return StartCoroutine(SummonMinions(desiredMinionCount));
        isTransitioning = false; 
    }

    void Update()
    {
        if (isShielded)
        {
            if (myEnemyMovement.health.currentHP < myEnemyMovement.health.maxHP)
            {
                myEnemyMovement.health.currentHP = myEnemyMovement.health.maxHP;
                myEnemyMovement.health.updateHealth();
            }
        }
        else
        {
            if (myEnemyMovement.health.currentHP > 0 && myEnemyMovement.health.currentHP < previousHP && !isTransitioning)
            {
                previousHP = myEnemyMovement.health.currentHP;
                TriggerHitSpawn();
                StartCoroutine(HitAndTeleportSequence());
            }
        }
    }

    private IEnumerator HitAndTeleportSequence()
    {
        isTransitioning = true;
        if (AudioManager.instance != null) AudioManager.instance.PlayBossGrunt();

        if (isAoEWarningActive)
        {
            isAoEWarningActive = false;
            readyToExplodeThisTurn = false;
            aoeCycleStep = 0; 
            
            if (bossWarningMap != null)
            {
                foreach (var c in aoeWarningCells) bossWarningMap.SetTile(c, null);
            }
            aoeWarningCells.Clear();
        }

        yield return StartCoroutine(BossTeleportFade(1f, 0f, 0.25f));

        List<Vector3Int> farCells = new List<Vector3Int>();
        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        
        int radius = arenaRadius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int c = new Vector3Int(x, y, 0);
                if (groundMap.HasTile(c) && !TurnManager.instance.IsEnemyAtCell(c) && myEnemyMovement.Distance(c, playerCell) >= 4f && !LevelGenerator.instance.hazardCells.Contains(c))
                {
                    farCells.Add(c);
                }
            }
        }

        if (farCells.Count == 0)
        {
            for (int x = -radius; x <= radius; x++) {
                for (int y = -radius; y <= radius; y++) {
                    Vector3Int c = new Vector3Int(x, y, 0);
                    if (groundMap.HasTile(c) && !TurnManager.instance.IsEnemyAtCell(c) && myEnemyMovement.Distance(c, playerCell) >= 3f && !LevelGenerator.instance.hazardCells.Contains(c)) {
                        farCells.Add(c);
                    }
                }
            }
        }

        if (farCells.Count > 0)
        {
            Vector3Int randomCell = farCells[Random.Range(0, farCells.Count)];
            myEnemyMovement.TeleportTo(randomCell);
        }

        yield return StartCoroutine(BossTeleportFade(0f, 1f, 0.25f));

        if (!isShielded) TriggerHitSpawn();

        isTransitioning = false; 
    }

    private IEnumerator BossTeleportFade(float startAlpha, float endAlpha, float duration)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;

        Color c = sr.color;
        float elapsed = 0f;

        Vector3 normalScale = Vector3.one;
        Vector3 stretchedScale = new Vector3(0.5f, 1.5f, 1f); 
        Vector3 startScale = startAlpha > endAlpha ? normalScale : stretchedScale;
        Vector3 endScale = startAlpha > endAlpha ? stretchedScale : normalScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            c.a = Mathf.Lerp(startAlpha, endAlpha, t);
            sr.color = c;
            
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            yield return null;
        }

        c.a = endAlpha;
        sr.color = c;
        transform.localScale = normalScale;
    }

    public IEnumerator ExecuteBossTurn()
    {
        if (myEnemyMovement.skipTurns > 0)
        {
            readyToExplodeThisTurn = false;

            // Stun yediyse AoE uyarı döngüsünü de sıfırla
            if (isAoEWarningActive)
            {
                isAoEWarningActive = false;
                aoeCycleStep = 0;
                if (myEnemyMovement.animator != null) myEnemyMovement.animator.SetBool("IsCharging", false);
                if (bossWarningMap != null)
                {
                    foreach (var c in aoeWarningCells) bossWarningMap.SetTile(c, null);
                }
                aoeWarningCells.Clear();
            }

            yield break;
        }
        if (isTransitioning)
        {
            if (aoeCycleStep < 2) aoeCycleStep++;
            yield break;
        }

        if (aoeCycleStep == 0 || aoeCycleStep == 1)
        {
            aoeCycleStep++;
        }
        else if (aoeCycleStep == 2)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayCharge();
            if (myEnemyMovement.animator != null) myEnemyMovement.animator.SetBool("IsCharging", true);
            ShowCheckerboardWarning();
            aoeCycleStep = 3; 
        }
        else if (aoeCycleStep == 3)
        {
            readyToExplodeThisTurn = true; 
        }
    }

    // ========================================================
    // YENİ: BOSS KAÇINCI SEVİYEDE İSE ONA GÖRE MİNYON SINIRI BELİRLE
    // ========================================================
    private int GetMaxMinionLimit()
    {
        if (RunManager.instance == null) return 1;
        
        int level = RunManager.instance.currentLevel;
        if (level <= 5) return 1;       // İlk boss: Maks 1 minyon
        if (level <= 10) return 2;      // İkinci boss: Maks 2 minyon
        return 3;                       // Üçüncü ve sonrası bosslar: Maks 3 minyon
    }

    private void TriggerHitSpawn()
    {
        if (isTransitioning) return; 
        if (isShielded) return; 
        
        summonedMinions.RemoveAll(m => m == null || m.health.currentHP <= 0);
        int currentMinions = summonedMinions.Count;
        
        // Dinamik sınırı al (Örn: İlk bosssa x = 1)
        int maxLimit = GetMaxMinionLimit();
        int toSpawn = 0;

        if (currentMinions < maxLimit) 
            toSpawn = maxLimit - currentMinions; // Eksiği tamamla
        else 
            toSpawn = 1; // Sınıra ulaşıldıysa veya geçildiyse bile en az 1 tane +1 yap

        StartCoroutine(SummonMinions(toSpawn));
    }

    private void ShowCheckerboardWarning()
    {
        isAoEWarningActive = true;
        aoeWarningCells.Clear();

        int radius = arenaRadius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (Mathf.Abs(x + y) % 2 == 0 && groundMap.HasTile(cell))
                {
                    aoeWarningCells.Add(cell);
                }
            }
        }

        if (bossWarningMap != null)
        {
            foreach (var wCell in aoeWarningCells)
            {
                StartCoroutine(SmoothBossWarningFadeIn(wCell));
            }
        }
    }

    private IEnumerator SmoothBossWarningFadeIn(Vector3Int cell)
    {
        bossWarningMap.SetTile(cell, warningTile);
        bossWarningMap.SetTileFlags(cell, TileFlags.None); 

        Color startColor = new Color(0f, 0.5f, 1f, 0f);   
        Color endColor = new Color(0f, 0.5f, 1f, 0.8f); 

        bossWarningMap.SetColor(cell, startColor);

        float duration = 0.3f; 
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);

            bossWarningMap.SetColor(cell, Color.Lerp(startColor, endColor, t));
            yield return null;
        }
        bossWarningMap.SetColor(cell, endColor);
    }

    public IEnumerator ExecuteCheckerboardAoE()
    {
        if (myEnemyMovement.animator != null) myEnemyMovement.animator.SetBool("IsCharging", false);
        
        readyToExplodeThisTurn = false;
        aoeCycleStep = 0; 
        isAoEWarningActive = false;

        List<Vector3Int> cellsToExplode = new List<Vector3Int>(aoeWarningCells);
        aoeWarningCells.Clear();

        if (bossWarningMap != null && cellsToExplode.Count > 0)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayLightning();
            Color intenseBright = new Color(0.2f, 0.8f, 1f, 1f);

            foreach (var c in cellsToExplode)
            {
                if (bossWarningMap.HasTile(c)) bossWarningMap.SetColor(c, intenseBright);
            }

            yield return new WaitForSeconds(0.1f);

            // Impact: hafif hitstop + yumuşak shake
            if (HitstopManager.instance != null) HitstopManager.instance.TriggerHitstop();
            CameraController.ShakeLighter();

            if (lightningStrikePrefab != null)
            {
                foreach (var c in cellsToExplode)
                {
                    Vector3 strikePos = groundMap.GetCellCenterWorld(c);
                    strikePos.z = 0f;
                    
                    GameObject lightning = Instantiate(lightningStrikePrefab, strikePos, Quaternion.identity);
                    
                    Destroy(lightning, 3f);
                }
            }

            // DAMAGE VERMEK: Visual başladığında HEMEN ver
            Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
            if (cellsToExplode.Contains(playerCell))
            {
                bool dodged = Random.value < RunManager.instance.dodgeChance;
                if (dodged)
                {
                    if (TurnManager.instance.dodgeEffectPrefab != null)
                        Instantiate(TurnManager.instance.dodgeEffectPrefab, TurnManager.instance.player.transform.position, Quaternion.identity);
                }
                else if (RunManager.instance.hasBioBarrier)
                {
                    foreach (var perk in RunManager.instance.activePerks) if (perk is BioBarrierPerk aegis) { aegis.BreakShield(); break; }
                    RunManager.instance.hasBioBarrier = false;
                }
                else
                {
                    TurnManager.instance.PlayerTakeDamage(2);
                }
            }

            float fadeDur = 0.5f; 
            float elapsed = 0f;
            Color startFadeColor = intenseBright;
            Color endFadeColor = new Color(0f, 0.5f, 1f, 0f); 

            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDur;
                t = t * t * (3f - 2f * t); 
                
                Color current = Color.Lerp(startFadeColor, endFadeColor, t);
                
                foreach (var c in cellsToExplode)
                {
                    if (bossWarningMap.HasTile(c)) bossWarningMap.SetColor(c, current);
                }
                yield return null;
            }
        }

        foreach (var c in cellsToExplode)
        {
            if (bossWarningMap != null && bossWarningMap.HasTile(c))
            {
                bossWarningMap.SetTile(c, null);
            }
        }

        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator SummonMinions(int countToSummon)
    {
        if (isSummoning) yield break; 
        isSummoning = true;

        Vector3Int playerCell = TurnManager.instance.player.GetCurrentCellPosition();
        Vector3Int bossCell = myEnemyMovement.GetCurrentCellPosition();
        List<Vector3Int> occupiedCells = new List<Vector3Int> { playerCell, bossCell };
        foreach (var e in TurnManager.instance.enemies)
        {
            if (e != null && e.health.currentHP > 0) occupiedCells.Add(e.GetCurrentCellPosition());
        }

        List<Vector3Int> spawnedThisRound = new List<Vector3Int>();
        int radius = arenaRadius;
        int maxAttempts = Mathf.Max(50, countToSummon * 20); 
        int attempts = 0;

        while (spawnedThisRound.Count < countToSummon && attempts < maxAttempts)
        {
            attempts++;
            
            Vector3Int cell = new Vector3Int(
                Random.Range(-radius, radius + 1),
                Random.Range(-radius, radius + 1),
                0
            );

            if (!groundMap.HasTile(cell) || 
                occupiedCells.Contains(cell) || 
                myEnemyMovement.Distance(cell, playerCell) < 2f ||  
                LevelGenerator.instance.hazardCells.Contains(cell))
                continue;

            bool tooCloseToSpawned = false;
            foreach (var spawnedCell in spawnedThisRound)
            {
                if (myEnemyMovement.Distance(cell, spawnedCell) < 2f)
                {
                    tooCloseToSpawned = true;
                    break;
                }
            }
            if (tooCloseToSpawned) continue;

            var bossSpawnTs = LevelGenerator.instance?.GetActiveTileSet();
            GameObject prefab = null;
            if (bossSpawnTs != null && bossSpawnTs.enemies != null && bossSpawnTs.enemies.Length > 0)
                prefab = bossSpawnTs.enemies[Random.Range(0, bossSpawnTs.enemies.Length)].prefab;
            if (prefab == null) continue;
            Vector3 spawnPos = groundMap.GetCellCenterWorld(cell);
            GameObject minionObj = Instantiate(prefab, spawnPos, Quaternion.identity);

            EnemyMovement minionAI = minionObj.GetComponent<EnemyMovement>();
            minionAI.groundMap = this.groundMap;

            float randomMultiplier = Random.Range(0.8f, 1.25f);

            // LevelGenerator'daki düşman HP hesaplaması ile aynı logic
            float minionBaseHealth = LevelGenerator.instance.CurrentEnemyHealth;
            int minionHP = Mathf.RoundToInt(minionBaseHealth * randomMultiplier);
            minionAI.health.maxHP = Mathf.Max(1, minionHP);
            minionAI.health.currentHP = minionAI.health.maxHP;
            minionAI.health.updateHealth();

            summonedMinions.Add(minionAI);
            spawnedThisRound.Add(cell); 
            StartCoroutine(minionAI.FadeSpawnCoroutine());
        }

        yield return new WaitForSeconds(0.5f);
        isSummoning = false; 
    }

    private bool totemSequenceRunning = false;
    private int pendingTotemDeaths = 0;

    public void OnTotemDestroyed()
    {
        activeTotems--;
        pendingTotemDeaths++;

        if (!totemSequenceRunning)
            StartCoroutine(TotemDestroyLoop());
    }

    /// <summary>
    /// Kuyruk sistemi — hızlı ardışık totem ölümlerini tek tek işler.
    /// Bir sekans bitmeden ölen totemler kuyrukta birikir, sırayla oynatılır.
    /// </summary>
    private IEnumerator TotemDestroyLoop()
    {
        totemSequenceRunning = true;

        while (pendingTotemDeaths > 0)
        {
            // Biriken tüm ölümleri tek seferde tüket
            int batchCount = pendingTotemDeaths;
            pendingTotemDeaths = 0;

            yield return StartCoroutine(TotemDestroySequence(batchCount));
        }

        totemSequenceRunning = false;
    }

    private IEnumerator TotemDestroySequence(int destroyedCount)
    {
        isTransitioning = true;
        // isSummoning'i sıfırla — önceki spawn yarım kalmışsa kilitlemesin
        isSummoning = false;

        yield return new WaitForSeconds(0.1f);

        bool lastTotem = activeTotems <= 0;

        if (lastTotem)
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayLightning();
            if (TurnManager.instance != null && TurnManager.instance.explosionPrefab != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.4f, 0.4f), 0f);
                    GameObject fx = Instantiate(TurnManager.instance.explosionPrefab, transform.position + offset, Quaternion.identity);
                    StartCoroutine(FadeAndDestroyExplosion(fx));
                    yield return new WaitForSeconds(0.12f);
                }
            }
            StartCoroutine(CameraShake(0.8f, 0.22f));
        }
        else
        {
            if (AudioManager.instance != null) AudioManager.instance.PlayExplosion();
            if (TurnManager.instance != null && TurnManager.instance.explosionPrefab != null)
            {
                Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f), 0f);
                GameObject fx = Instantiate(TurnManager.instance.explosionPrefab, transform.position + offset, Quaternion.identity);
                StartCoroutine(FadeAndDestroyExplosion(fx));
            }
            StartCoroutine(CameraShake(0.4f, 0.14f));
        }

        // Tüm minionları öldür — zaten ölmekte olan (isDead) minionları atla
        foreach (var minion in summonedMinions)
        {
            if (minion != null && !minion.health.IsDead && minion.health.currentHP > 0)
                minion.health.currentHP = 0;
        }
        foreach (var minion in summonedMinions)
        {
            if (minion != null && !minion.health.IsDead)
                StartCoroutine(minion.FadeDieCoroutine());
        }
        summonedMinions.Clear();

        yield return new WaitForSeconds(lastTotem ? 1.1f : 0.55f);

        int maxLimit = GetMaxMinionLimit();
        int countToSpawn = 0;

        if (lastTotem)
        {
            isShielded = false;
            StartCoroutine(ShatterShieldVisual());
            previousHP = myEnemyMovement.health.currentHP;

            // Kalkan kırıldığında can barını hemen mavi renge güncelle
            myEnemyMovement.health.updateHealth();

            countToSpawn = maxLimit;
        }
        else
        {
            int currentMinionCount = summonedMinions.Count(m => m != null && m.health.currentHP > 0);

            if (currentMinionCount < maxLimit)
                countToSpawn = maxLimit - currentMinionCount;
            else
                countToSpawn = 1;
        }

        yield return StartCoroutine(SummonMinions(countToSpawn));

        isTransitioning = false;
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        // CameraController üzerinden shake yap — kendi konum yönetimini bozmasın
        CameraController controller = FindFirstObjectByType<CameraController>();
        if (controller != null)
            controller.Shake(duration, magnitude);
        yield return new WaitForSeconds(duration);
    }

    private IEnumerator ShieldPulseLoop()
    {
        if (shieldInstance == null) yield break;
        Vector3 baseScale = shieldInstance.transform.localScale;
        float t = 0f;
        while (shieldInstance != null && isShielded)
        {
            t += Time.deltaTime * 1.2f; 
            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.04f; 
            shieldInstance.transform.localScale = baseScale * pulse;
            yield return null;
        }
        if (shieldInstance != null)
            shieldInstance.transform.localScale = baseScale;
    }

    private IEnumerator ShatterShieldVisual()
    {
        if (shieldInstance == null) yield break;

        if (AudioManager.instance != null) AudioManager.instance.PlayShieldBreak();

        if (TurnManager.instance != null && TurnManager.instance.explosionPrefab != null)
        {
            GameObject fx = Instantiate(TurnManager.instance.explosionPrefab, transform.position, Quaternion.identity);
            StartCoroutine(FadeAndDestroyExplosion(fx));
        }

        SpriteRenderer[] shieldSrs = shieldInstance.GetComponentsInChildren<SpriteRenderer>();
        Vector3 startScale = shieldInstance.transform.localScale;
        Vector3 targetScale = startScale * 2.5f;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            shieldInstance.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            foreach (var sr in shieldSrs)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }
            yield return null;
        }

        Destroy(shieldInstance);
        shieldInstance = null;
    }

    private IEnumerator FadeAndDestroyExplosion(GameObject fx)
    {
        SpriteRenderer[] renderers = fx.GetComponentsInChildren<SpriteRenderer>();
        Vector3 startScale = Vector3.one * 0.5f; 
        Vector3 endScale = Vector3.one * 3f;    
        
        float duration = 0.3f; 
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            fx.transform.localScale = Vector3.Lerp(startScale, endScale, t);

            foreach (var sr in renderers)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.8f, 0f, t); 
                sr.color = c;
            }
            yield return null;
        }
        
        Destroy(fx);
    }

    public void OnBossDied()
    {
        isAoEWarningActive = false;
        readyToExplodeThisTurn = false;
        aoeWarningCells.Clear();

        if (bossWarningMap != null) bossWarningMap.ClearAllTiles();

        // Tüm hayatta kalan düşmanları temizle (minionlar, totemler ve diğerleri)
        foreach (var e in TurnManager.instance.enemies.ToList())
        {
            if (e == null) continue;
            // Boss'un kendisini atla
            if (e == myEnemyMovement) continue;
            if (e.health.IsDead || e.health.currentHP <= 0)
            {
                // Zaten ölü ama sprite kalabilir — zorla yok et
                if (e.gameObject != null) Destroy(e.gameObject);
                continue;
            }
            e.health.currentHP = 0;
            if (e.gameObject.activeInHierarchy)
                TurnManager.instance.StartCoroutine(ForceKillEnemy(e));
        }

        // summonedMinions listesinde olup enemies'te olmayan düşmanları da temizle
        foreach (var minion in summonedMinions)
        {
            if (minion == null) continue;
            if (minion.health.currentHP <= 0 && minion.gameObject != null)
            {
                Destroy(minion.gameObject);
                continue;
            }
            minion.health.currentHP = 0;
            if (minion.gameObject.activeInHierarchy)
                TurnManager.instance.StartCoroutine(ForceKillEnemy(minion));
        }
    }

    private IEnumerator ForceKillEnemy(EnemyMovement enemy)
    {
        if (enemy == null) yield break;
        var visuals = enemy.GetComponent<EnemyVisuals>();

        // fadeDieStarted zaten true ise (burn vb.) direkt destroy
        if (visuals != null && visuals.fadeDieStarted)
        {
            yield return new WaitForSeconds(0.5f);
            if (enemy != null && enemy.gameObject != null)
                Destroy(enemy.gameObject);
            yield break;
        }

        // Normal fade die
        yield return TurnManager.instance.StartCoroutine(enemy.FadeDieCoroutine());
    }

    public bool IsCellTargetedByBoss(Vector3Int cell)
    {
        return isAoEWarningActive && aoeWarningCells.Contains(cell);
    }
}