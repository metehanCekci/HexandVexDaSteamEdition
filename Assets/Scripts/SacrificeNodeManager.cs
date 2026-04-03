using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sacrifice Node — perk feda ederek yeni perk alma ekranı.
///
/// 7 slot: 2 Common, 2 Rare, 1 Epic, 1 Legendary, 1 Secret
/// Maliyetler: Common=2, Rare=3, Epic=5, Legendary=8, Secret=15
/// Reroll maliyeti: 1 perk feda
///
/// Havuz run boyunca kalıcıdır. Alınan slot sold-out olur.
/// İptal edilirse seçilen perkler iade edilir.
/// </summary>
public class SacrificeNodeManager : MonoBehaviour
{
    public static SacrificeNodeManager instance;

    // ─── Feda Maliyetleri ───
    public static readonly Dictionary<PerkRarity, int> SacrificeCost = new Dictionary<PerkRarity, int>
    {
        { PerkRarity.Common,    2 },
        { PerkRarity.Rare,      3 },
        { PerkRarity.Epic,      5 },
        { PerkRarity.Legendary, 8 },
        { PerkRarity.Secret,   15 },
    };

    // ─── Slot Dağılımı ───
    // 2 Common, 2 Rare, 1 Epic, 1 Legendary, 1 Secret = 7 slot
    private static readonly PerkRarity[] slotRarities = new PerkRarity[]
    {
        PerkRarity.Common, PerkRarity.Common,
        PerkRarity.Rare,   PerkRarity.Rare,
        PerkRarity.Epic,
        PerkRarity.Legendary,
        PerkRarity.Secret,
    };

    [Header("UI Panel")]
    public GameObject panel;
    public CanvasGroup canvasGroup;

    [Header("Reward Slots (7 adet, Inspector'dan bağla)")]
    public List<SacrificeRewardSlot> rewardSlots = new List<SacrificeRewardSlot>();

    [Header("Sacrifice Perk Listesi (sol panel)")]
    public Transform sacrificeListContainer;   // ScrollView Content
    public GameObject sacrificePerkEntryPrefab;  // SacrificePerkEntry prefab

    [Header("Seçim Bilgisi")]
    public TMP_Text sacrificeCountText;   // "3 / 5 seçildi"
    public TMP_Text targetInfoText;       // "Hedef: Epic — 5 perk gerekli"
    public Button confirmButton;
    public Button cancelButton;

    [Header("Reroll")]
    public Button rerollButton;
    public TMP_Text rerollInfoText;       // "Reroll: 1 perk feda"

    [Header("Continue")]
    public Button continueButton;

    // ─── Run-kalıcı havuz ───
    // Her run başında Generate() ile doldurulur, run boyunca değişmez.
    [HideInInspector] public List<SacrificeSlotData> persistentSlots = new List<SacrificeSlotData>();
    private bool poolGenerated = false;

    // ─── Anlık seçim state'i ───
    private int targetSlotIndex = -1;          // Hangi reward slot'u hedefliyoruz
    private List<BasePerk> selectedForSacrifice = new List<BasePerk>();  // Feda edilecek perkler
    private List<SacrificePerkEntry> spawnedEntries = new List<SacrificePerkEntry>();

    void Awake()
    {
        if (instance == null) instance = this;
        if (panel != null) panel.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // POOL GENERATION (Run başında bir kez)
    // ═══════════════════════════════════════════

    public void GeneratePool()
    {
        persistentSlots.Clear();
        poolGenerated = true;

        LevelUpManager lum = LevelUpManager.instance;
        if (lum == null) return;

        foreach (PerkRarity rarity in slotRarities)
        {
            GameObject perkGO = PickRandomPerk(rarity, lum, persistentSlots);
            persistentSlots.Add(new SacrificeSlotData
            {
                rarity    = rarity,
                perkPrefab = perkGO,
                purchased = false,
            });
        }
    }

    private GameObject PickRandomPerk(PerkRarity rarity, LevelUpManager lum, List<SacrificeSlotData> alreadyPicked)
    {
        List<GameObject> pool = GetPool(rarity, lum);
        if (pool == null || pool.Count == 0) return null;

        // Zaten seçilmiş perk prefab'larını hariç tut
        HashSet<GameObject> used = new HashSet<GameObject>(alreadyPicked.Select(s => s.perkPrefab));
        List<GameObject> candidates = pool.Where(p => p != null && !used.Contains(p)).ToList();
        if (candidates.Count == 0) candidates = pool.Where(p => p != null).ToList();
        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private List<GameObject> GetPool(PerkRarity rarity, LevelUpManager lum)
    {
        switch (rarity)
        {
            case PerkRarity.Common:    return lum.commonPerks;
            case PerkRarity.Rare:      return lum.rarePerks;
            case PerkRarity.Epic:      return lum.epicPerks;
            case PerkRarity.Legendary: return lum.legendaryPerks;
            case PerkRarity.Secret:    return lum.legendaryPerks; // Secret için legendary havuzundan al, gerekirse değiştirilebilir
            default:                   return null;
        }
    }

    // ═══════════════════════════════════════════
    // SHOW / HIDE
    // ═══════════════════════════════════════════

    public void Show()
    {
        if (!poolGenerated) GeneratePool();

        ResetSelectionState();
        BuildRewardSlots();
        BuildSacrificePerkList();
        RefreshUI();

        if (panel != null) panel.SetActive(true);
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancel);
        }
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnReroll);
        }

        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    private void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.25f;
            canvasGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════
    // REWARD SLOTS
    // ═══════════════════════════════════════════

    private void BuildRewardSlots()
    {
        for (int i = 0; i < rewardSlots.Count && i < persistentSlots.Count; i++)
        {
            SacrificeSlotData data = persistentSlots[i];
            SacrificeRewardSlot slot = rewardSlots[i];

            slot.gameObject.SetActive(true);
            slot.Setup(data, i, this);
        }
        // Fazla slot varsa gizle
        for (int i = persistentSlots.Count; i < rewardSlots.Count; i++)
            rewardSlots[i].gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // SACRIFICE PERK LIST (sol panel)
    // ═══════════════════════════════════════════

    private void BuildSacrificePerkList()
    {
        // Önceki entry'leri temizle
        foreach (var e in spawnedEntries) if (e != null) Destroy(e.gameObject);
        spawnedEntries.Clear();

        if (sacrificeListContainer == null || sacrificePerkEntryPrefab == null) return;

        List<BasePerk> allPerks = GetAllSacrificablePerks();
        foreach (BasePerk perk in allPerks)
        {
            GameObject go = Instantiate(sacrificePerkEntryPrefab, sacrificeListContainer);
            SacrificePerkEntry entry = go.GetComponent<SacrificePerkEntry>();
            if (entry != null)
            {
                entry.Setup(perk, this);
                spawnedEntries.Add(entry);
            }
        }
    }

    private List<BasePerk> GetAllSacrificablePerks()
    {
        List<BasePerk> all = new List<BasePerk>();
        if (RunManager.instance == null) return all;
        all.AddRange(RunManager.instance.activePerks.Where(p => p != null));
        all.AddRange(RunManager.instance.inventoryPerks.Where(p => p != null));
        return all;
    }

    // ═══════════════════════════════════════════
    // TARGET SLOT SEÇİMİ
    // ═══════════════════════════════════════════

    public void OnRewardSlotClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= persistentSlots.Count) return;
        if (persistentSlots[slotIndex].purchased) return;

        // Aynı slota tekrar tıklanırsa seçimi iptal et
        if (targetSlotIndex == slotIndex)
        {
            targetSlotIndex = -1;
            ClearSacrificeSelection();
        }
        else
        {
            targetSlotIndex = slotIndex;
            ClearSacrificeSelection();
        }

        RefreshUI();
    }

    // ═══════════════════════════════════════════
    // PERK SEÇİMİ (feda listesinden)
    // ═══════════════════════════════════════════

    public void OnPerkEntryClicked(BasePerk perk)
    {
        if (targetSlotIndex < 0) return;  // Önce hedef slot seç

        if (selectedForSacrifice.Contains(perk))
        {
            selectedForSacrifice.Remove(perk);
        }
        else
        {
            int required = GetRequiredSacrificeCount();
            if (selectedForSacrifice.Count >= required) return;  // Fazla seçme
            selectedForSacrifice.Add(perk);
        }

        RefreshUI();
    }

    // ═══════════════════════════════════════════
    // CONFIRM / CANCEL
    // ═══════════════════════════════════════════

    private void OnConfirm()
    {
        if (targetSlotIndex < 0) return;
        if (!CanConfirm()) return;

        SacrificeSlotData data = persistentSlots[targetSlotIndex];

        // Perkleri kaldır
        foreach (BasePerk perk in selectedForSacrifice)
            RemovePerkFromRun(perk);

        // Yeni perki ver
        if (data.perkPrefab != null && RunManager.instance != null)
            RunManager.instance.AddPerk(data.perkPrefab);

        // Slotu sold-out yap
        data.purchased = true;

        // Entry'leri yenile
        foreach (var e in spawnedEntries) if (e != null) Destroy(e.gameObject);
        spawnedEntries.Clear();

        ResetSelectionState();
        BuildRewardSlots();
        BuildSacrificePerkList();
        RefreshUI();
    }

    private void OnCancel()
    {
        // Seçimi sıfırla — perkler iade edildi (hiç kaldırılmadı zaten)
        ResetSelectionState();
        RefreshUI();
    }

    private void OnContinue()
    {
        Hide();
        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    // ═══════════════════════════════════════════
    // REROLL
    // ═══════════════════════════════════════════

    private void OnReroll()
    {
        // Reroll maliyeti: 1 perk feda
        // Önce 1 perk seçilmeli (reroll için ayrı seçim mekanizması — targetSlotIndex = -1 iken)
        if (selectedForSacrifice.Count < 1) return;

        BasePerk sacrificed = selectedForSacrifice[0];
        RemovePerkFromRun(sacrificed);

        // Tüm bought olmayan slotları yeniden çek
        LevelUpManager lum = LevelUpManager.instance;
        if (lum != null)
        {
            for (int i = 0; i < persistentSlots.Count; i++)
            {
                if (!persistentSlots[i].purchased)
                {
                    GameObject newPerk = PickRandomPerk(persistentSlots[i].rarity, lum, persistentSlots);
                    persistentSlots[i].perkPrefab = newPerk;
                }
            }
        }

        ResetSelectionState();
        BuildRewardSlots();
        BuildSacrificePerkList();
        RefreshUI();
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    private void RemovePerkFromRun(BasePerk perk)
    {
        if (RunManager.instance == null || perk == null) return;

        if (RunManager.instance.activePerks.Contains(perk))
        {
            perk.OnUnequip();
            RunManager.instance.activePerks.Remove(perk);
        }
        else if (RunManager.instance.inventoryPerks.Contains(perk))
        {
            RunManager.instance.inventoryPerks.Remove(perk);
        }

        Destroy(perk.gameObject);
    }

    private int GetRequiredSacrificeCount()
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= persistentSlots.Count) return 0;
        return SacrificeCost[persistentSlots[targetSlotIndex].rarity];
    }

    private bool CanConfirm()
    {
        if (targetSlotIndex < 0) return false;
        if (persistentSlots[targetSlotIndex].purchased) return false;
        return selectedForSacrifice.Count >= GetRequiredSacrificeCount();
    }

    private void ClearSacrificeSelection()
    {
        selectedForSacrifice.Clear();
        foreach (var e in spawnedEntries)
            if (e != null) e.SetSelected(false);
    }

    private void ResetSelectionState()
    {
        targetSlotIndex = -1;
        selectedForSacrifice.Clear();
    }

    // ═══════════════════════════════════════════
    // UI REFRESH
    // ═══════════════════════════════════════════

    private void RefreshUI()
    {
        // Hedef bilgisi
        if (targetInfoText != null)
        {
            if (targetSlotIndex >= 0 && targetSlotIndex < persistentSlots.Count)
            {
                PerkRarity r = persistentSlots[targetSlotIndex].rarity;
                int cost = SacrificeCost[r];
                targetInfoText.text = $"Hedef: <color={RarityColor(r)}>{r}</color> — {cost} perk gerekli";
            }
            else
            {
                targetInfoText.text = "Bir reward seç";
            }
        }

        // Seçim sayacı
        int required = GetRequiredSacrificeCount();
        if (sacrificeCountText != null)
        {
            if (targetSlotIndex >= 0)
                sacrificeCountText.text = $"{selectedForSacrifice.Count} / {required} seçildi";
            else
                sacrificeCountText.text = "";
        }

        // Confirm butonu
        if (confirmButton != null)
        {
            confirmButton.interactable = CanConfirm();
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }

        // Cancel butonu — sadece hedef seçiliyse aktif
        if (cancelButton != null)
            cancelButton.interactable = targetSlotIndex >= 0;

        // Reroll butonu — en az 1 perk seçiliyse ve target yok iken
        if (rerollButton != null)
            rerollButton.interactable = (targetSlotIndex < 0 && selectedForSacrifice.Count >= 1)
                                     || (targetSlotIndex >= 0 && selectedForSacrifice.Count >= 1);

        if (rerollInfoText != null)
            rerollInfoText.text = "Reroll: 1 perk feda";

        // Reward slot'larını güncelle
        for (int i = 0; i < rewardSlots.Count && i < persistentSlots.Count; i++)
            rewardSlots[i].RefreshVisual(i == targetSlotIndex, selectedForSacrifice.Count, GetRequiredSacrificeCount());

        // Perk entry'lerini güncelle
        foreach (var e in spawnedEntries)
            if (e != null) e.RefreshVisual(selectedForSacrifice, targetSlotIndex >= 0);
    }

    private string RarityColor(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Common:    return "#CCCCCC";
            case PerkRarity.Rare:      return "#4488FF";
            case PerkRarity.Epic:      return "#AA44FF";
            case PerkRarity.Legendary: return "#FFAA00";
            case PerkRarity.Secret:    return "#FF4444";
            default:                   return "#FFFFFF";
        }
    }

    // ─── Run başında pool sıfırla ───
    public void ResetForNewRun()
    {
        persistentSlots.Clear();
        poolGenerated = false;
    }
}

// ─── Kalıcı slot verisi ───
[System.Serializable]
public class SacrificeSlotData
{
    public PerkRarity    rarity;
    public GameObject    perkPrefab;
    public bool          purchased;
}
