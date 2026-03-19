using UnityEngine;
using System.Collections.Generic;

public class RunManager : MonoBehaviour
{
    public static RunManager instance;

    [Header("Run Progression")]
    public int currentLevel = 1; // Kacinci odadayiz?

    [Header("Map Progression")]
    public int currentLayerIndex = 0;
    public MapNodeType currentNodeType = MapNodeType.Combat;

    [Header("Run Stats")]

    public int currentGold = 0;
    public int playerMaxHealth = 3;
    public int playerCurrentHealth = 3;
    public int baseDiceCount = 2;
    public int maxTurns = 1;
    public int collectibleSlots = 3;
    public float armorChance = 0f; // Knight's Plating icin (%15 hasar engelleme ihtimali)
    public int bonusGoldPerKill = 0; // Bounty Hunter icin

    [Header("Perk Degiskenleri")]
    public int bonusGold = 0;        // Bounty Hunter icin
    public float dodgeChance = 0f;   // Knight's Plating icin
    public bool hasBioBarrier = false; // Bio-Barrier kalkani icin
    public int skipBonusGold = 0;    // Mercenary's Rest icin
    public int luckyCloverLevel = 0; // Lucky Clover -- rarity sansini esitler

    [Header("Reroll Stack")]
    public int shopRerollStack = 0; // Her shop reroll'da +1, zarlarin base degerine kalici eklenir

    [Header("Combat Stats")]
    public float criticalChance = 0.10f; // 0.0 to 1.0
    public float criticalDamageMultiplier = 2.0f;

    [Header("Active Perks")]
    public const int MAX_ACTIVE_PERKS = 6;
    public Transform perkUIContainer; // Assign a Horizontal Layout Group UI panel here!
    public List<BasePerk> activePerks = new List<BasePerk>();

    [Header("Inventory Perks (Stash)")]
    public List<BasePerk> inventoryPerks = new List<BasePerk>();

    [Header("Item Buff'lari (Tek Kullanimlik)")]
    public int bonusDiceNextCombat = 0;
    public bool doubleGoldNextKill = false;
    public bool doubleDamageNextCombat = false;
    public bool cleaveNextCombat = false;
    public bool surgeBootNextTurn = false;
    [HideInInspector] public bool surgeBootActive = false;
    public bool hasPerkReroll = false; // Bu tur 2 hex hareket edebilir mi?

    [Header("Silah Seçimi")]
    public WeaponType selectedWeapon = WeaponType.Greatsword;

    [Header("Hiz Ayari")]
    public bool fastMode = false;

    [Header("Legendary Stats")]
    public int extraMovesPerTurn = 0; // Swift Action ile artacak (Normalde 0)
    public int remainingMoves;       // O tur icindeki kalan hamle hakki

    [Header("Run Statistics")]
    public int totalEnemiesKilled = 0;
    public int totalDamageDealt = 0;
    public int totalDamageReceived = 0;
    public int totalTurnsPlayed = 0;
    public int totalDiceRolled = 0;
    public int totalGoldEarned = 0;
    public int totalLevelsPlayed = 0;

    // Best run (PlayerPrefs ile kalici)
    public static int BestKills      => PlayerPrefs.GetInt("best_kills", 0);
    public static int BestDamage     => PlayerPrefs.GetInt("best_damage", 0);
    public static int BestTurns      => PlayerPrefs.GetInt("best_turns", 0);
    public static int BestDice       => PlayerPrefs.GetInt("best_dice", 0);
    public static int BestGold       => PlayerPrefs.GetInt("best_gold", 0);
    public static int BestLevels     => PlayerPrefs.GetInt("best_levels", 0);

    public void SaveBestRun()
    {
        if (totalEnemiesKilled > BestKills)   PlayerPrefs.SetInt("best_kills",   totalEnemiesKilled);
        if (totalDamageDealt   > BestDamage)  PlayerPrefs.SetInt("best_damage",  totalDamageDealt);
        if (totalTurnsPlayed   > BestTurns)   PlayerPrefs.SetInt("best_turns",   totalTurnsPlayed);
        if (totalDiceRolled    > BestDice)    PlayerPrefs.SetInt("best_dice",    totalDiceRolled);
        if (totalGoldEarned    > BestGold)    PlayerPrefs.SetInt("best_gold",    totalGoldEarned);
        if (totalLevelsPlayed  > BestLevels)  PlayerPrefs.SetInt("best_levels",  totalLevelsPlayed);
        PlayerPrefs.Save();
    }

    void Awake()
    {
        // The legendary Singleton pattern for cross-scene persistence
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // If the perk container is a child of this object, it survives too!
            if (perkUIContainer != null)
                DontDestroyOnLoad(perkUIContainer.root.gameObject);

        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        // ActivePerkBar yoksa otomatik olustur
        if (ActivePerkBar.instance == null)
        {
            ActivePerkBar.CreateFromCode();
        }

        // Silah seçimi kaldırıldı — her zaman Greatsword
        selectedWeapon = WeaponType.Greatsword;
    }

    // Called when the player selects a perk from the Level Up screen
    public void AddPerk(GameObject perkPrefab)
    {
        BasePerk prefabScript = perkPrefab.GetComponent<BasePerk>();

        // Hem activePerks hem inventoryPerks'te bu perk tipinden var mi kontrol et
        BasePerk existingActive = activePerks.Find(p => p.GetType() == prefabScript.GetType());
        BasePerk existingInventory = inventoryPerks.Find(p => p.GetType() == prefabScript.GetType());

        if (existingActive != null)
        {
            // Aktif slotlarda zaten varsa: sadece yukselt
            existingActive.Upgrade();
            RefreshPerkUI();
            return;
        }

        if (existingInventory != null)
        {
            // Envanterde (stash) varsa: orani yukselt
            existingInventory.Upgrade();
            RefreshPerkUI();
            return;
        }

        // Collection: perk ilk kez alındı event'i
        GameEvents.PerkAcquired(prefabScript.GetType().Name);

        // Ilk defa aliniyorsa: obje olarak yarat
        GameObject newPerkObj = Instantiate(perkPrefab, transform);
        BasePerk newPerk = newPerkObj.GetComponent<BasePerk>();

        // LevelUpManager listelerinden dogru rarity'i ata
        if (LevelUpManager.instance != null)
        {
            if (LevelUpManager.instance.legendaryPerks.Contains(perkPrefab))
                newPerk.rarity = PerkRarity.Legendary;
            else if (LevelUpManager.instance.epicPerks.Contains(perkPrefab))
                newPerk.rarity = PerkRarity.Epic;
            else if (LevelUpManager.instance.rarePerks.Contains(perkPrefab))
                newPerk.rarity = PerkRarity.Rare;
        }

        // Aktif slotlarda yer varsa aktife ekle, yoksa envantere
        if (activePerks.Count < MAX_ACTIVE_PERKS)
        {
            activePerks.Add(newPerk);
            newPerk.OnAcquire();
            newPerk.OnEquip();
        }
        else
        {
            inventoryPerks.Add(newPerk);
            newPerk.OnAcquire();
        }

        RefreshPerkUI();
    }

    /// <summary>Aktif slot ile envanter slotunu yer degistirir.</summary>
    public void SwapPerk(int activeIndex, int inventoryIndex)
    {
        if (activeIndex < 0 || activeIndex >= activePerks.Count) return;
        if (inventoryIndex < 0 || inventoryIndex >= inventoryPerks.Count) return;

        BasePerk activePerk = activePerks[activeIndex];
        BasePerk inventoryPerk = inventoryPerks[inventoryIndex];

        // Callback'leri cagir
        activePerk.OnUnequip();
        inventoryPerk.OnEquip();

        // Yer degistir
        activePerks[activeIndex] = inventoryPerk;
        inventoryPerks[inventoryIndex] = activePerk;

        RefreshPerkUI();
    }

    /// <summary>Envanterdeki perki aktif slotlara tasir (sadece yer varsa).</summary>
    public void MoveToActive(int inventoryIndex)
    {
        if (inventoryIndex < 0 || inventoryIndex >= inventoryPerks.Count) return;
        if (activePerks.Count >= MAX_ACTIVE_PERKS) return;

        BasePerk perk = inventoryPerks[inventoryIndex];
        inventoryPerks.RemoveAt(inventoryIndex);
        activePerks.Add(perk);
        perk.OnEquip();

        RefreshPerkUI();
    }

    /// <summary>Aktif perki envantere tasir.</summary>
    public void MoveToInventory(int activeIndex)
    {
        if (activeIndex < 0 || activeIndex >= activePerks.Count) return;

        BasePerk perk = activePerks[activeIndex];
        activePerks.RemoveAt(activeIndex);
        inventoryPerks.Add(perk);
        perk.OnUnequip();

        RefreshPerkUI();
    }

    /// <summary>Perk UI'larini yeniler (ActivePerkBar + PerkInventoryUI).</summary>
    public void RefreshPerkUI()
    {
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.RefreshBar();
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.RefreshUI();
    }

    public string GetStatsSummary()
    {
        return $"Levels Played: {totalLevelsPlayed}\n" +
               $"Dice Rolled: {totalDiceRolled}\n" +
               $"Damage Dealt: {totalDamageDealt}\n" +
               $"Enemies Killed: {totalEnemiesKilled}\n" +
               $"Gold Earned: {totalGoldEarned}";
    }

    public string GetPerksSummary()
    {
        if (activePerks.Count == 0 && inventoryPerks.Count == 0) return "None";
        var sb = new System.Text.StringBuilder();

        if (activePerks.Count > 0)
        {
            sb.AppendLine($"-- Active ({activePerks.Count}/{MAX_ACTIVE_PERKS}) --");
            foreach (var p in activePerks)
                sb.AppendLine($"{p.perkName}  Lv {p.currentLevel}");
        }

        if (inventoryPerks.Count > 0)
        {
            sb.AppendLine($"-- Stash ({inventoryPerks.Count}) --");
            foreach (var p in inventoryPerks)
                sb.AppendLine($"{p.perkName}  Lv {p.currentLevel}");
        }

        return sb.ToString().TrimEnd();
    }

}
