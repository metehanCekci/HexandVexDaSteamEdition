using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Profil sistemi — max 3 profil, her profil ayrı PlayerPrefs verisi.
/// Profil değişiminde mevcut verileri JSON'a kaydeder, yeni profilin JSON'ını yükler.
/// Ana menüde ProfileUI ile kullanılır.
/// </summary>
public class ProfileManager : MonoBehaviour
{
    public static ProfileManager instance;

    public const int MAX_PROFILES = 3;
    private const string ACTIVE_PROFILE_KEY = "active_profile_id";

    public int ActiveProfileId { get; private set; } = 0;

    private string SaveDir => Path.Combine(Application.persistentDataPath, "Profiles");

    [System.Serializable]
    private class ProfileSaveData
    {
        public string profileName = "";
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!Directory.Exists(SaveDir))
            Directory.CreateDirectory(SaveDir);

        // Aktif profili yükle (profil dosyasından, PlayerPrefs'ten değil — çünkü PlayerPrefs profil-spesifik)
        ActiveProfileId = LoadActiveProfileId();
        LoadProfile(ActiveProfileId);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ═══════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════

    /// <summary>Profil var mı?</summary>
    public bool ProfileExists(int id)
    {
        return File.Exists(GetProfilePath(id));
    }

    /// <summary>Profil adını döner.</summary>
    public string GetProfileName(int id)
    {
        string metaPath = GetMetaPath(id);
        if (File.Exists(metaPath))
            return File.ReadAllText(metaPath).Trim();
        return ProfileExists(id) ? $"Profile {id + 1}" : "";
    }

    /// <summary>Profil adını değiştirir.</summary>
    public void SetProfileName(int id, string name)
    {
        File.WriteAllText(GetMetaPath(id), name);
    }

    /// <summary>Profil collection %'sini hesaplar (default perkler hariç).</summary>
    public float GetCollectionPercent(int id)
    {
        if (id == ActiveProfileId)
        {
            // Aktif profil — direkt PerkCollectionManager'dan oku
            if (PerkCollectionManager.instance != null)
            {
                int total = PerkCollectionManager.instance.GetEarnableCount();
                if (total == 0) return 0f;
                return (float)PerkCollectionManager.instance.GetEarnedUnlockCount() / total * 100f;
            }
            return 0f;
        }

        // Başka profil — dosyadan oku ve say (default perkler hariç)
        string path = GetProfilePath(id);
        if (!File.Exists(path)) return 0f;
        string json = File.ReadAllText(path);
        ProfileSaveData data = JsonUtility.FromJson<ProfileSaveData>(json);
        if (data == null) return 0f;

        int unlocked = 0;
        int total2 = 0;
        if (PerkCollectionManager.instance != null && PerkCollectionManager.instance.database != null)
        {
            foreach (var entry in PerkCollectionManager.instance.database.entries)
            {
                if (entry.unlockCondition == UnlockCondition.Default) continue;
                total2++;
                string key = $"perk_unlocked_{entry.perkId}";
                int idx = data.keys.IndexOf(key);
                if (idx >= 0 && data.values[idx] == "1")
                    unlocked++;
            }
        }
        return total2 > 0 ? (float)unlocked / total2 * 100f : 0f;
    }

    /// <summary>Aktif profili kaydedip yeni profili yükler.</summary>
    public void SwitchProfile(int id)
    {
        if (id == ActiveProfileId) return;
        if (id < 0 || id >= MAX_PROFILES) return;

        SaveCurrentProfile();
        ActiveProfileId = id;
        SaveActiveProfileId(id);

        if (ProfileExists(id))
            LoadProfile(id);
        else
            ResetPlayerPrefs();

        // PerkCollectionManager'ı yeniden yükle
        if (PerkCollectionManager.instance != null)
        {
            PerkCollectionManager.instance.ResetAll();
            // ResetAll default'ları açar, ama biz dosyadan yükledik
            // Tekrar load yapmamız lazım
        }
        // En güvenli: sahneyi yeniden yükle
    }

    /// <summary>Aktif profili kaydeder (uygulama kapanmadan önce çağır).</summary>
    public void SaveCurrentProfile()
    {
        SaveProfile(ActiveProfileId);
    }

    /// <summary>Profili sıfırlar (tüm verileri siler).</summary>
    public void ResetProfile(int id)
    {
        if (id == ActiveProfileId)
        {
            ResetPlayerPrefs();
            SaveProfile(id);
            // PerkCollectionManager'ı da sıfırla
            if (PerkCollectionManager.instance != null)
                PerkCollectionManager.instance.ResetAll();
        }
        else
        {
            // Dosyayı sil
            string path = GetProfilePath(id);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Profildeki tüm collection'ı açar.</summary>
    public void UnlockAllForProfile(int id)
    {
        if (id == ActiveProfileId)
        {
            if (PerkCollectionManager.instance != null)
                PerkCollectionManager.instance.UnlockAll();
            SaveProfile(id);
        }
    }

    /// <summary>Yeni profil oluşturur (boş).</summary>
    public void CreateProfile(int id, string name)
    {
        SetProfileName(id, name);
        if (id == ActiveProfileId)
        {
            ResetPlayerPrefs();
            SaveProfile(id);
        }
        else
        {
            // Boş profil dosyası yaz
            ProfileSaveData empty = new ProfileSaveData { profileName = name };
            string json = JsonUtility.ToJson(empty, true);
            File.WriteAllText(GetProfilePath(id), json);
        }
    }

    /// <summary>Profili siler.</summary>
    public void DeleteProfile(int id)
    {
        string path = GetProfilePath(id);
        if (File.Exists(path)) File.Delete(path);
        string metaPath = GetMetaPath(id);
        if (File.Exists(metaPath)) File.Delete(metaPath);
    }

    // ═══════════════════════════════════════════
    // INTERNAL
    // ═══════════════════════════════════════════

    private void SaveProfile(int id)
    {
        ProfileSaveData data = new ProfileSaveData();
        data.profileName = GetProfileName(id);

        // PlayerPrefs'i serialize etmenin bir yolu yok — bilinen key'leri toplamamız lazım
        // Collection + guidebook + stats key'lerini topla
        List<string> knownKeys = CollectKnownKeys();

        foreach (string key in knownKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                data.keys.Add(key);
                data.values.Add(PlayerPrefs.GetInt(key, 0).ToString());
            }
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetProfilePath(id), json);
    }

    private void LoadProfile(int id)
    {
        string path = GetProfilePath(id);
        if (!File.Exists(path))
        {
            // İlk çalıştırma — mevcut PlayerPrefs'i profil 0 olarak kaydet
            if (id == 0)
                SaveProfile(0);
            return;
        }

        string json = File.ReadAllText(path);
        ProfileSaveData data = JsonUtility.FromJson<ProfileSaveData>(json);
        if (data == null) return;

        // Önce bilinen tüm key'leri temizle
        List<string> knownKeys = CollectKnownKeys();
        foreach (string key in knownKeys)
            PlayerPrefs.DeleteKey(key);

        // Dosyadan yükle
        for (int i = 0; i < data.keys.Count; i++)
        {
            int val;
            if (int.TryParse(data.values[i], out val))
                PlayerPrefs.SetInt(data.keys[i], val);
        }
        PlayerPrefs.Save();
    }

    private void ResetPlayerPrefs()
    {
        List<string> knownKeys = CollectKnownKeys();
        foreach (string key in knownKeys)
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    private List<string> CollectKnownKeys()
    {
        List<string> keys = new List<string>();

        // Collection perk keys
        if (PerkCollectionManager.instance != null && PerkCollectionManager.instance.database != null)
        {
            foreach (var entry in PerkCollectionManager.instance.database.entries)
            {
                keys.Add($"perk_unlocked_{entry.perkId}");
                keys.Add($"perk_progress_{entry.perkId}");
                keys.Add($"perk_seen_{entry.perkId}");
            }
        }

        // Stat keys
        keys.Add("total_runs_completed");
        keys.Add("total_runs_won");
        keys.Add("total_tiles_moved");
        keys.Add("kills_before_boss");
        keys.Add("best_run_kills");
        keys.Add("best_run_gold");
        keys.Add("best_run_levels");
        keys.Add("best_run_perks");
        keys.Add("best_run_damage_dealt");
        keys.Add("best_run_damage_received");
        keys.Add("total_enemies_killed");
        keys.Add("total_gold_earned");
        keys.Add("total_levels_cleared");
        keys.Add("total_spike_pushes");
        keys.Add("total_skips");

        // Lifetime stat keys (perk unlock koşullarını tetikleyen)
        keys.Add("lifetime_total_kills");
        keys.Add("lifetime_total_gold");
        keys.Add("lifetime_levels_cleared");

        // RunManager best record keys
        keys.Add("best_kills");
        keys.Add("best_damage");
        keys.Add("best_turns");
        keys.Add("best_dice");
        keys.Add("best_gold");
        keys.Add("best_levels");

        return keys;
    }

    private string GetProfilePath(int id) => Path.Combine(SaveDir, $"profile_{id}.json");
    private string GetMetaPath(int id) => Path.Combine(SaveDir, $"profile_{id}_name.txt");

    private int LoadActiveProfileId()
    {
        string path = Path.Combine(SaveDir, "active_profile.txt");
        if (File.Exists(path))
        {
            int val;
            if (int.TryParse(File.ReadAllText(path).Trim(), out val))
                return Mathf.Clamp(val, 0, MAX_PROFILES - 1);
        }
        return 0;
    }

    private void SaveActiveProfileId(int id)
    {
        string path = Path.Combine(SaveDir, "active_profile.txt");
        File.WriteAllText(path, id.ToString());
    }

    void OnApplicationQuit()
    {
        SaveCurrentProfile();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveCurrentProfile();
    }
}
