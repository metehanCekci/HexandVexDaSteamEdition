using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Perk koleksiyonunun unlock durumlarını takip eden merkezi singleton.
/// PlayerPrefs ile kalıcı, GameEvents'e subscribe olarak mevcut scriptlere dokunmadan çalışır.
/// MainMenu sahnesinde yaşar, DontDestroyOnLoad ile run boyunca da hayatta kalır.
/// </summary>
public class PerkCollectionManager : MonoBehaviour
{
    public static PerkCollectionManager instance;

    [Header("Database")]
    public PerkCollectionDatabase database;

    // Runtime: perkId → unlock durumu cache
    private Dictionary<string, bool> unlockCache = new Dictionary<string, bool>();
    // Runtime: perkId → progress cache
    private Dictionary<string, int> progressCache = new Dictionary<string, int>();

    // Yeni unlock'lar (toast/notification için)
    private Queue<PerkCollectionData> pendingUnlockNotifications = new Queue<PerkCollectionData>();

    // ═══════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllProgress();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnEnable()
    {
        GameEvents.OnEnemyKilledTotal += OnEnemyKilledTotal;
        GameEvents.OnRunCompleted += OnRunCompleted;
        GameEvents.OnBossDefeated += OnBossDefeated;
        GameEvents.OnPerkAcquired += OnPerkAcquired;
        GameEvents.OnTotalGoldLifetime += OnTotalGoldLifetime;
        GameEvents.OnLevelCleared += OnLevelCleared;
        GameEvents.OnEnemyPushedIntoSpike += OnEnemyPushedIntoSpike;
        GameEvents.OnKillsBeforeBossUpdated += OnKillsBeforeBossUpdated;
        GameEvents.OnGoldChanged += OnGoldChanged;
        GameEvents.OnPlayerMoved += OnPlayerMoved;
        GameEvents.OnSkipTurnPerformed += OnSkipTurnPerformed;
    }

    void OnDisable()
    {
        GameEvents.OnEnemyKilledTotal -= OnEnemyKilledTotal;
        GameEvents.OnRunCompleted -= OnRunCompleted;
        GameEvents.OnBossDefeated -= OnBossDefeated;
        GameEvents.OnPerkAcquired -= OnPerkAcquired;
        GameEvents.OnTotalGoldLifetime -= OnTotalGoldLifetime;
        GameEvents.OnLevelCleared -= OnLevelCleared;
        GameEvents.OnEnemyPushedIntoSpike -= OnEnemyPushedIntoSpike;
        GameEvents.OnKillsBeforeBossUpdated -= OnKillsBeforeBossUpdated;
        GameEvents.OnGoldChanged -= OnGoldChanged;
        GameEvents.OnPlayerMoved -= OnPlayerMoved;
        GameEvents.OnSkipTurnPerformed -= OnSkipTurnPerformed;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ═══════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════

    /// <summary>Perk prefab'ının koleksiyonda açık olup olmadığını kontrol eder.</summary>
    public static bool IsUnlocked(GameObject perkPrefab)
    {
        if (instance == null || instance.database == null) return true; // Manager yoksa hepsini aç
        var entry = instance.GetEntry(perkPrefab);
        if (entry == null) return true; // Database'de tanımlı değilse açık say
        return instance.IsUnlockedById(entry.perkId);
    }

    /// <summary>Perk ID ile unlock durumunu kontrol eder.</summary>
    public bool IsUnlockedById(string perkId)
    {
        if (unlockCache.TryGetValue(perkId, out bool val)) return val;
        // Cache'de yoksa PlayerPrefs'ten oku
        bool unlocked = PlayerPrefs.GetInt($"perk_unlocked_{perkId}", 0) == 1;
        unlockCache[perkId] = unlocked;
        return unlocked;
    }

    /// <summary>Perk ID için mevcut progress'i döner.</summary>
    public int GetProgress(string perkId)
    {
        if (progressCache.TryGetValue(perkId, out int val)) return val;
        int progress = PlayerPrefs.GetInt($"perk_progress_{perkId}", 0);
        progressCache[perkId] = progress;
        return progress;
    }

    /// <summary>Toplam açık perk sayısını döner.</summary>
    public int GetUnlockedCount()
    {
        if (database == null) return 0;
        int count = 0;
        foreach (var entry in database.entries)
            if (IsUnlockedById(entry.perkId)) count++;
        return count;
    }

    /// <summary>Toplam perk sayısını döner.</summary>
    public int GetTotalCount()
    {
        return database != null ? database.entries.Count : 0;
    }

    /// <summary>Bekleyen unlock notification'ı varsa döner, yoksa null.</summary>
    public PerkCollectionData DequeueUnlockNotification()
    {
        return pendingUnlockNotifications.Count > 0 ? pendingUnlockNotifications.Dequeue() : null;
    }

    /// <summary>Bekleyen notification var mı?</summary>
    public bool HasPendingNotification => pendingUnlockNotifications.Count > 0;

    /// <summary>Belirli bir perki zorla aç (debug/cheat).</summary>
    public void ForceUnlock(string perkId)
    {
        UnlockPerk(perkId);
    }

    /// <summary>Tüm perkleri aç (debug).</summary>
    public void UnlockAll()
    {
        if (database == null) return;
        foreach (var entry in database.entries)
            UnlockPerk(entry.perkId, silent: true);
        PlayerPrefs.Save();
    }

    /// <summary>Tüm unlock'ları sıfırla (debug).</summary>
    public void ResetAll()
    {
        if (database == null) return;
        foreach (var entry in database.entries)
        {
            PlayerPrefs.DeleteKey($"perk_unlocked_{entry.perkId}");
            PlayerPrefs.DeleteKey($"perk_progress_{entry.perkId}");
            unlockCache[entry.perkId] = false;
            progressCache[entry.perkId] = 0;
        }
        // Default olanları geri aç
        foreach (var entry in database.entries)
        {
            if (entry.unlockCondition == UnlockCondition.Default)
                UnlockPerk(entry.perkId, silent: true);
        }
        PlayerPrefs.Save();
    }

    // ═══════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════

    private void OnEnemyKilledTotal(int totalKills)
    {
        CheckCondition(UnlockCondition.KillEnemies, totalKills);
    }

    private void OnRunCompleted(bool victory)
    {
        // Toplam run sayısı
        int totalRuns = PlayerPrefs.GetInt("total_runs_completed", 0) + 1;
        PlayerPrefs.SetInt("total_runs_completed", totalRuns);

        int totalWins = PlayerPrefs.GetInt("total_runs_won", 0);
        if (victory)
        {
            totalWins++;
            PlayerPrefs.SetInt("total_runs_won", totalWins);
        }
        PlayerPrefs.Save();

        CheckCondition(UnlockCondition.CompleteRuns, totalRuns);
        CheckCondition(UnlockCondition.WinRuns, totalWins);

        // Tek run kill check
        if (RunManager.instance != null)
            CheckCondition(UnlockCondition.KillEnemiesSingleRun, RunManager.instance.totalEnemiesKilled);
    }

    private void OnBossDefeated()
    {
        CheckCondition(UnlockCondition.DefeatBoss, 1);
    }

    private void OnPerkAcquired(string perkTypeName)
    {
        CheckConditionWithParam(UnlockCondition.UseSpecificPerk, perkTypeName);
    }

    private void OnTotalGoldLifetime(int totalGold)
    {
        CheckCondition(UnlockCondition.CollectGold, totalGold);
    }

    private void OnLevelCleared(int totalLevels)
    {
        CheckCondition(UnlockCondition.ClearLevels, totalLevels);
    }

    private void OnEnemyPushedIntoSpike(int totalCount)
    {
        CheckCondition(UnlockCondition.PushEnemyIntoSpike, totalCount);
    }

    private void OnKillsBeforeBossUpdated(int killCount)
    {
        CheckCondition(UnlockCondition.KillBeforeBoss, killCount);
    }

    private void OnGoldChanged(int currentGold)
    {
        CheckCondition(UnlockCondition.HoldGold, currentGold);
    }

    private void OnPlayerMoved(UnityEngine.Vector3Int cell)
    {
        int totalMoves = PlayerPrefs.GetInt("total_tiles_moved", 0) + 1;
        PlayerPrefs.SetInt("total_tiles_moved", totalMoves);
        CheckCondition(UnlockCondition.MoveTiles, totalMoves);
    }

    private void OnSkipTurnPerformed(int totalSkips)
    {
        CheckCondition(UnlockCondition.SkipTurns, totalSkips);
    }

    // ═══════════════════════════════════════════════════════
    //  INTERNAL LOGIC
    // ═══════════════════════════════════════════════════════

    private void LoadAllProgress()
    {
        if (database == null) return;

        foreach (var entry in database.entries)
        {
            bool unlocked = PlayerPrefs.GetInt($"perk_unlocked_{entry.perkId}", 0) == 1;
            int progress = PlayerPrefs.GetInt($"perk_progress_{entry.perkId}", 0);

            // Default koşullu olanları otomatik aç
            if (entry.unlockCondition == UnlockCondition.Default && !unlocked)
            {
                PlayerPrefs.SetInt($"perk_unlocked_{entry.perkId}", 1);
                unlocked = true;
            }

            unlockCache[entry.perkId] = unlocked;
            progressCache[entry.perkId] = progress;
        }
        PlayerPrefs.Save();
    }

    private void CheckCondition(UnlockCondition condition, int currentAmount)
    {
        if (database == null) return;

        // "High-water mark" koşulları — progress sadece artmalı, düşmemeli
        // (ör: HoldGold = elde tuttuğun en yüksek gold, KillEnemiesSingleRun = tek run en iyi kill)
        bool isHighWaterMark = condition == UnlockCondition.HoldGold
                            || condition == UnlockCondition.KillEnemiesSingleRun
                            || condition == UnlockCondition.KillBeforeBoss;

        foreach (var entry in database.entries)
        {
            if (entry.unlockCondition != condition) continue;
            if (IsUnlockedById(entry.perkId)) continue;

            // Progress'i güncelle — high-water mark ise sadece artır
            int prev = GetProgress(entry.perkId);
            int newVal = isHighWaterMark ? Mathf.Max(prev, currentAmount) : currentAmount;
            progressCache[entry.perkId] = newVal;
            PlayerPrefs.SetInt($"perk_progress_{entry.perkId}", newVal);

            if (newVal >= entry.requiredAmount)
                UnlockPerk(entry.perkId);
        }
        PlayerPrefs.Save();
    }

    private void CheckConditionWithParam(UnlockCondition condition, string param)
    {
        if (database == null) return;

        foreach (var entry in database.entries)
        {
            if (entry.unlockCondition != condition) continue;
            if (IsUnlockedById(entry.perkId)) continue;
            if (!string.IsNullOrEmpty(entry.conditionParam) && entry.conditionParam != param) continue;

            UnlockPerk(entry.perkId);
        }
    }

    private void UnlockPerk(string perkId, bool silent = false)
    {
        if (IsUnlockedById(perkId)) return;

        unlockCache[perkId] = true;
        PlayerPrefs.SetInt($"perk_unlocked_{perkId}", 1);
        PlayerPrefs.Save();

        var entry = database.entries.Find(e => e.perkId == perkId);
        if (entry != null)
        {
            Debug.Log($"<color=#00FF00>[COLLECTION]</color> Perk unlocked: {entry.perkId}");

            if (!silent)
            {
                pendingUnlockNotifications.Enqueue(entry);
                GameEvents.PerkUnlocked(perkId);
            }

            // UnlockOtherPerks koşulunu kontrol et
            CheckCondition(UnlockCondition.UnlockOtherPerks, GetUnlockedCount());
        }
    }

    private PerkCollectionData GetEntry(GameObject perkPrefab)
    {
        if (database == null || perkPrefab == null) return null;
        return database.entries.Find(e => e.perkPrefab == perkPrefab);
    }
}
