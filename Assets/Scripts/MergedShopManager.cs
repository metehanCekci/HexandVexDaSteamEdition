using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Birleşik Shop — Perk Section + Item Section tek canvas'ta.
/// Shop ve PerkSelection node'larının ikisini de yönetir.
///
/// PERK SECTION: 3 perk kartı + reroll (paralı)
/// ITEM SECTION: 3 item kartı (reroll yok)
/// İkisi ayrı section'da, aynı panelde.
/// </summary>
public class MergedShopManager : MonoBehaviour
{
    public static MergedShopManager instance;

    // ═══════════════════════════════════════════
    // PANEL
    // ═══════════════════════════════════════════

    [Header("Ana Panel")]
    public GameObject panel;
    public CanvasGroup canvasGroup;

    [Header("Altın Göstergesi")]
    public TMP_Text goldText;

    [Header("Continue Butonu")]
    public Button continueButton;

    // ═══════════════════════════════════════════
    // PERK SECTION
    // ═══════════════════════════════════════════

    [Header("── Perk Section ──")]
    public GameObject perkSection;         // Section root — aktif/pasif et

    [Header("Perk Kartları (3 adet)")]
    public List<MergedShopPerkSlot> perkSlots = new List<MergedShopPerkSlot>();

    [Header("Perk Havuzları")]
    public List<GameObject> commonPerks;
    public List<GameObject> rarePerks;
    public List<GameObject> epicPerks;
    public List<GameObject> legendaryPerks;

    [Header("Perk Reroll")]
    public Button perkRerollButton;
    public TMP_Text perkRerollPriceText;
    public float perkRerollBaseCost = 10f;
    public float perkRerollIncrement = 5f;

    // ═══════════════════════════════════════════
    // ITEM SECTION
    // ═══════════════════════════════════════════

    [Header("── Item Section ──")]
    public GameObject itemSection;         // Section root — aktif/pasif et

    [Header("Item Kartları (3 adet)")]
    public List<MergedShopItemSlot> itemSlots = new List<MergedShopItemSlot>();

    [Header("Item Pool")]
    public List<BaseItem> itemPool = new List<BaseItem>();
    public BaseItem secretItem;
    [Range(0f, 1f)] public float secretItemChance = 0.0005f;

    // ═══════════════════════════════════════════
    // İÇ STATE
    // ═══════════════════════════════════════════

    private List<GameObject>  currentPerkChoices  = new List<GameObject>();
    private List<BaseItem>    currentItems        = new List<BaseItem>();
    private List<bool>        itemPurchased       = new List<bool>();
    private HashSet<string>   shownItemNames      = new HashSet<string>();

    private int   perkRerollCount;
    private float currentPerkRerollCost;
    private int   hoveredPerkIndex = -1;
    private int   hoveredItemIndex = -1;

    public static bool hasBoughtSecretItem = false;

    // ═══════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance == null) instance = this;

        // Referanslar kopuksa runtime'da otomatik bul
        if (panel == null) AutoWireReferences();

        Debug.Log($"[MergedShop] Awake: panel={panel != null}, perkSlots={perkSlots.Count}, itemSlots={itemSlots.Count}, canvasGroup={canvasGroup != null}, continueButton={continueButton != null}, rerollButton={perkRerollButton != null}");
        if (panel != null) panel.SetActive(false);

        // Perk listeleri boşsa runtime'da prefab'lardan doldur
        if (commonPerks.Count == 0)
            AutoPopulatePerkPools();

        // Item pool boşsa runtime'da doldur
        if (itemPool.Count == 0)
            AutoPopulateItemPool();
    }

    private void AutoWireReferences()
    {
        // MergedShopCanvas'ı bul
        Canvas shopCanvas = null;
        foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (c.gameObject.name == "MergedShopCanvas" && c.gameObject.scene.IsValid())
            { shopCanvas = c; break; }
        }
        if (shopCanvas == null) { Debug.LogError("[MergedShop] AutoWire: MergedShopCanvas not found!"); return; }

        Transform canvasT = shopCanvas.transform;
        Transform panelT = canvasT.Find("ShopPanel");
        if (panelT == null) { Debug.LogError("[MergedShop] AutoWire: ShopPanel not found!"); return; }

        panel = panelT.gameObject;
        canvasGroup = panelT.GetComponent<CanvasGroup>();
        goldText = FindTMP(panelT, "GoldText");
        perkSection = FindChild(panelT, "PerkSection");
        itemSection = FindChild(panelT, "ItemSection");

        // Butonlar
        Transform rerollT = panelT.Find("RerollButton");
        if (rerollT != null)
        {
            perkRerollButton = rerollT.GetComponent<UnityEngine.UI.Button>();
            Transform rerollTextT = rerollT.Find("Text");
            if (rerollTextT != null) perkRerollPriceText = rerollTextT.GetComponent<TMPro.TMP_Text>();
            AddRuntimeHoverScale(rerollT.gameObject);
        }

        Transform contT = panelT.Find("ContinueButton");
        if (contT != null)
        {
            continueButton = contT.GetComponent<UnityEngine.UI.Button>();
            AddRuntimeHoverScale(contT.gameObject);
        }

        // Perk slots
        if (perkSection != null)
        {
            perkSlots.Clear();
            for (int i = 0; i < 3; i++)
            {
                Transform card = perkSection.transform.Find($"PerkCard_{i}");
                if (card == null) continue;
                var slot = new MergedShopPerkSlot();
                slot.root = card.gameObject;
                slot.background = card.GetComponent<UnityEngine.UI.Image>();
                slot.button = card.GetComponent<UnityEngine.UI.Button>();
                slot.nameText = FindTMP(card, "Name");
                slot.rarityText = FindTMP(card, "Rarity");
                slot.levelText = FindTMP(card, "Level");
                slot.descriptionText = FindTMP(card, "Description");
                slot.priceText = FindTMP(card, "PriceText");
                Transform iconT = card.Find("Icon");
                if (iconT != null) slot.iconImage = iconT.GetComponent<UnityEngine.UI.Image>();
                slot.soldOutOverlay = FindChild(card, "SoldOutOverlay");
                perkSlots.Add(slot);
            }
        }

        // Item slots
        if (itemSection != null)
        {
            itemSlots.Clear();
            for (int i = 0; i < 3; i++)
            {
                Transform card = itemSection.transform.Find($"ItemCard_{i}");
                if (card == null) continue;
                var slot = new MergedShopItemSlot();
                slot.root = card.gameObject;
                slot.background = card.GetComponent<UnityEngine.UI.Image>();
                slot.button = card.GetComponent<UnityEngine.UI.Button>();
                slot.nameText = FindTMP(card, "Name");
                slot.descriptionText = FindTMP(card, "Description");
                slot.priceText = FindTMP(card, "PriceText");
                Transform iconT = card.Find("Icon");
                if (iconT != null) slot.iconImage = iconT.GetComponent<UnityEngine.UI.Image>();
                slot.soldOutOverlay = FindChild(card, "SoldOut");
                itemSlots.Add(slot);
            }
        }

        Debug.Log($"[MergedShop] AutoWire done: panel={panel != null}, perkSlots={perkSlots.Count}, itemSlots={itemSlots.Count}");
    }

    private void AutoPopulatePerkPools()
    {
        // Önce LevelUpManager'dan dene
        if (LevelUpManager.instance != null && LevelUpManager.instance.commonPerks.Count > 0)
        {
            commonPerks    = new List<GameObject>(LevelUpManager.instance.commonPerks);
            rarePerks      = new List<GameObject>(LevelUpManager.instance.rarePerks);
            epicPerks      = new List<GameObject>(LevelUpManager.instance.epicPerks);
            legendaryPerks = new List<GameObject>(LevelUpManager.instance.legendaryPerks);
            Debug.Log($"[MergedShop] Perk pools from LevelUpManager: C={commonPerks.Count} R={rarePerks.Count} E={epicPerks.Count} L={legendaryPerks.Count}");
            return;
        }

        // LevelUpManager yoksa: projedeki tüm BasePerk prefab'larını tara
        var allPerks = Resources.FindObjectsOfTypeAll<BasePerk>();
        foreach (var perk in allPerks)
        {
            // Scene objeleri değil, sadece prefab asset'leri al
            if (perk.gameObject.scene.IsValid()) continue;
            GameObject prefab = perk.gameObject;
            switch (perk.rarity)
            {
                case PerkRarity.Common:    commonPerks.Add(prefab); break;
                case PerkRarity.Rare:      rarePerks.Add(prefab); break;
                case PerkRarity.Epic:      epicPerks.Add(prefab); break;
                case PerkRarity.Legendary: legendaryPerks.Add(prefab); break;
            }
        }
        Debug.Log($"[MergedShop] AutoPopulate perk pools (FindObjectsOfTypeAll): C={commonPerks.Count} R={rarePerks.Count} E={epicPerks.Count} L={legendaryPerks.Count}");
    }

    private void AutoPopulateItemPool()
    {
        var allItems = Resources.FindObjectsOfTypeAll<BaseItem>();
        foreach (var item in allItems)
        {
            if (item == null) continue;
            // MutationCatalyst = secret item
            if (item is MutationCatalyst)
            {
                if (secretItem == null) secretItem = item;
                continue;
            }
            itemPool.Add(item);
        }
        Debug.Log($"[MergedShop] AutoPopulate itemPool: {itemPool.Count} items, secretItem={secretItem != null}");
    }

    private static void AddRuntimeHoverScale(GameObject go)
    {
        if (go.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null) return;
        var trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) => go.transform.localScale = new Vector3(1.05f, 1.05f, 1f));
        trigger.triggers.Add(enter);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener((_) => go.transform.localScale = Vector3.one);
        trigger.triggers.Add(exit);
    }

    private static TMPro.TMP_Text FindTMP(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        return t != null ? t.GetComponent<TMPro.TMP_Text>() : null;
    }

    private static GameObject FindChild(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        return t != null ? t.gameObject : null;
    }

    // ═══════════════════════════════════════════
    // SHOW
    // ═══════════════════════════════════════════

    public void OpenAsMapNode()
    {
        Debug.Log($"[MergedShop] OpenAsMapNode called. panel={panel != null}, perkSection={perkSection != null}");
        Debug.Log($"[MergedShop] Perk pools: common={commonPerks.Count}, rare={rarePerks.Count}, epic={epicPerks.Count}, legendary={legendaryPerks.Count}");
        Debug.Log($"[MergedShop] PerkSlots count={perkSlots.Count}, LevelUpManager.instance={LevelUpManager.instance != null}");

        // Awake'de LevelUpManager henüz hazır olmamış olabilir, burada tekrar dene
        if (commonPerks.Count == 0 && LevelUpManager.instance != null)
        {
            commonPerks    = new List<GameObject>(LevelUpManager.instance.commonPerks);
            rarePerks      = new List<GameObject>(LevelUpManager.instance.rarePerks);
            epicPerks      = new List<GameObject>(LevelUpManager.instance.epicPerks);
            legendaryPerks = new List<GameObject>(LevelUpManager.instance.legendaryPerks);
            Debug.Log($"[MergedShop] Copied from LevelUpManager: common={commonPerks.Count}, rare={rarePerks.Count}, epic={epicPerks.Count}, legendary={legendaryPerks.Count}");
        }

        perkRerollCount         = 0;
        currentPerkRerollCost   = perkRerollBaseCost;
        shownItemNames.Clear();

        GeneratePerkChoices();
        GenerateItemChoices();
        RefreshGold();
        RefreshRerollButton();

        if (panel != null)
        {
            // Önce alpha 0 yap ki flash olmasın
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            // Parent canvas kapalıysa onu da aç
            Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null && !parentCanvas.gameObject.activeSelf)
                parentCanvas.gameObject.SetActive(true);
            panel.SetActive(true);
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
            AddRuntimeHoverScale(continueButton.gameObject);
        }
        if (perkRerollButton != null)
        {
            perkRerollButton.onClick.RemoveAllListeners();
            perkRerollButton.onClick.AddListener(TryReroll);
            AddRuntimeHoverScale(perkRerollButton.gameObject);
        }

        StopAllCoroutines();
        StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        // Panel fade in
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.2f;
                canvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }

        // Kartları sıfırla (scale 0) — önce perkler (üst), sonra itemler (alt)
        List<GameObject> allCards = new List<GameObject>();
        foreach (var s in perkSlots) if (s.root != null && s.root.activeSelf) allCards.Add(s.root);
        foreach (var s in itemSlots) if (s.root != null && s.root.activeSelf) allCards.Add(s.root);

        foreach (var card in allCards)
            card.transform.localScale = Vector3.zero;

        // Pop-in: soldan sağa, üstten alta
        if (AudioManager.instance != null) AudioManager.instance.PlayCard();
        float stagger = 0.18f;
        for (int i = 0; i < allCards.Count; i++)
        {
            StartCoroutine(PopInCard(allCards[i].transform));
            yield return new WaitForSecondsRealtime(stagger);
        }
    }

    private IEnumerator PopInCard(Transform card)
    {
        // 0 → 1.08 (0.15s)
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.15f;
            float s = Mathf.Lerp(0f, 1.08f, t);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        // 1.08 → 1.0 (0.08s)
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.08f;
            float s = Mathf.Lerp(1.08f, 1f, t);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private void OnContinue()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutAndClose());
    }

    private IEnumerator FadeOutAndClose()
    {
        if (canvasGroup != null)
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime / 0.2f;
                canvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }
        if (panel != null)
        {
            panel.SetActive(false);
            // Parent canvas'ı da kapat
            Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null && parentCanvas.gameObject != panel)
                parentCanvas.gameObject.SetActive(false);
        }
        if (MapManager.instance != null) MapManager.instance.OnNodeComplete();
    }

    // ═══════════════════════════════════════════
    // PERK SECTION
    // ═══════════════════════════════════════════

    private void GeneratePerkChoices()
    {
        currentPerkChoices.Clear();

        if (commonPerks.Count == 0 && rarePerks.Count == 0 && epicPerks.Count == 0 && legendaryPerks.Count == 0)
        { Debug.LogWarning("[MergedShop] All perk pools empty — hiding perk section"); HidePerkSection(); return; }

        bool allMaxed = AreAllPerksMaxed();
        if (allMaxed) { Debug.LogWarning("[MergedShop] All perks maxed — hiding perk section"); HidePerkSection(); return; }

        if (perkSection != null) perkSection.SetActive(true);

        bool isBossReward = RunManager.instance != null && RunManager.instance.currentNodeType == MapNodeType.Boss;

        for (int i = 0; i < 3; i++)
        {
            GameObject pick = null;
            int safety = 0;
            while (pick == null || currentPerkChoices.Contains(pick) || IsPerkMaxedOut(pick))
            {
                pick = GetRandomPerkByRarity(isBossReward);
                if (++safety > 50) { pick = GetAnyValidFallback(); break; }
            }
            if (pick != null) currentPerkChoices.Add(pick);
        }

        Debug.Log($"[MergedShop] GeneratePerkChoices done. choices={currentPerkChoices.Count}");
        for (int i = 0; i < currentPerkChoices.Count; i++)
            Debug.Log($"[MergedShop]   choice[{i}] = {currentPerkChoices[i]?.name ?? "NULL"}");

        PopulatePerkSlots();
    }

    private void PopulatePerkSlots()
    {
        Debug.Log($"[MergedShop] PopulatePerkSlots: perkSlots.Count={perkSlots.Count}, currentPerkChoices.Count={currentPerkChoices.Count}");
        for (int i = 0; i < perkSlots.Count; i++)
        {
            Debug.Log($"[MergedShop]   slot[{i}]: root={perkSlots[i].root != null}, root.name={perkSlots[i].root?.name ?? "NULL"}, button={perkSlots[i].button != null}");
            if (i < currentPerkChoices.Count && currentPerkChoices[i] != null)
            {
                perkSlots[i].Setup(currentPerkChoices[i], i, this);
                if (perkSlots[i].root != null) perkSlots[i].root.SetActive(true);
                Debug.Log($"[MergedShop]   slot[{i}] activated with {currentPerkChoices[i].name}");
            }
            else
            {
                if (perkSlots[i].root != null) perkSlots[i].root.SetActive(false);
                Debug.Log($"[MergedShop]   slot[{i}] hidden (no choice)");
            }
        }
    }

    public void SelectPerk(int index)
    {
        if (index >= currentPerkChoices.Count) return;
        if (index >= perkSlots.Count) return;
        GameObject perkGO = currentPerkChoices[index];
        if (perkGO == null) return;

        // Gold kontrolü
        int cost = perkSlots[index].price;
        if (RunManager.instance != null && RunManager.instance.currentGold < cost)
        {
            StartCoroutine(FlashText(goldText));
            return;
        }

        if (RunManager.instance != null)
        {
            RunManager.instance.currentGold -= cost;
            RunManager.instance.AddPerk(perkGO);
            GameEvents.GoldChanged(RunManager.instance.currentGold);
        }

        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        // Seçilen slot'u sold-out yap
        if (perkSlots[index].soldOutOverlay != null) perkSlots[index].soldOutOverlay.SetActive(true);
        if (perkSlots[index].button != null) perkSlots[index].button.interactable = false;

        RefreshGold();
        RefreshPerkAffordability();
    }

    private void RefreshPerkAffordability()
    {
        if (RunManager.instance == null) return;
        for (int i = 0; i < perkSlots.Count; i++)
        {
            if (perkSlots[i].button == null || !perkSlots[i].button.interactable) continue;
            bool canAfford = RunManager.instance.currentGold >= perkSlots[i].price;
            if (perkSlots[i].priceText != null)
                perkSlots[i].priceText.color = canAfford ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        }
    }

    private void TryReroll()
    {
        if (RunManager.instance == null) return;
        int cost = Mathf.RoundToInt(currentPerkRerollCost);
        if (RunManager.instance.currentGold < cost) { StartCoroutine(FlashText(goldText)); return; }

        RunManager.instance.currentGold -= cost;
        perkRerollCount++;
        currentPerkRerollCost = perkRerollBaseCost + perkRerollIncrement * perkRerollCount;

        GeneratePerkChoices();
        GenerateItemChoices();
        RefreshGold();
        RefreshRerollButton();
        GameEvents.GoldChanged(RunManager.instance.currentGold);
    }

    private void RefreshRerollButton()
    {
        if (RunManager.instance == null) return;
        int cost = Mathf.RoundToInt(currentPerkRerollCost);
        if (perkRerollPriceText != null)
            perkRerollPriceText.text = $"REROLL  <color=#FFD933>{cost}</color>";
        if (perkRerollButton != null)
            perkRerollButton.interactable = RunManager.instance.currentGold >= cost;
    }

    private void HidePerkSection()
    {
        if (perkSection != null) perkSection.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // ITEM SECTION
    // ═══════════════════════════════════════════

    private void GenerateItemChoices()
    {
        currentItems.Clear();
        itemPurchased.Clear();

        if (itemPool.Count == 0) { HideItemSection(); return; }
        if (itemSection != null) itemSection.SetActive(true);

        // Secret item kontrolü
        int secretSlotIndex = -1;
        bool secretAvailable = secretItem != null && secretItem is SecretPerkOrb orb && orb.HasAvailableSecrets();
        bool guaranteeSecret = RunManager.instance != null && RunManager.instance.currentLevel >= 6
                               && !hasBoughtSecretItem && perkRerollCount == 0;
        if (secretItem != null && !hasBoughtSecretItem && secretAvailable && (guaranteeSecret || Random.value < secretItemChance))
            secretSlotIndex = Random.Range(0, 3);

        // Havuz tükenirse shownItemNames sıfırla
        int available = 0;
        foreach (var item in itemPool) if (!shownItemNames.Contains(item.itemName)) available++;
        if (available < 3) shownItemNames.Clear();

        List<int> used = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            if (i == secretSlotIndex) { currentItems.Add(secretItem); itemPurchased.Add(false); continue; }

            BaseItem picked = null;
            int safety = 0;
            do
            {
                int idx = Random.Range(0, itemPool.Count);
                picked = itemPool[idx];
                if (++safety > 100) break;
            }
            while (used.Contains(itemPool.IndexOf(picked))
                || shownItemNames.Contains(picked.itemName)
                || (secretItem != null && picked.itemName == secretItem.itemName)
                || (RunManager.instance != null && RunManager.instance.hasPerkReroll && picked is MutationCatalyst));

            if (picked != null) { used.Add(itemPool.IndexOf(picked)); shownItemNames.Add(picked.itemName); }
            currentItems.Add(picked);
            itemPurchased.Add(false);
        }

        PopulateItemSlots();
    }

    private void PopulateItemSlots()
    {
        for (int i = 0; i < itemSlots.Count; i++)
        {
            if (i < currentItems.Count && currentItems[i] != null)
            {
                int idx = i;
                itemSlots[i].Setup(currentItems[i], () => TryBuyItem(idx));
                if (itemSlots[i].root != null) itemSlots[i].root.SetActive(true);
                if (itemSlots[i].soldOutOverlay != null) itemSlots[i].soldOutOverlay.SetActive(false);
                if (itemSlots[i].button != null) itemSlots[i].button.interactable = true;
            }
            else
            {
                if (itemSlots[i].root != null) itemSlots[i].root.SetActive(false);
            }
        }
        RefreshItemAffordability();
    }

    public void TryBuyItem(int index)
    {
        if (index >= currentItems.Count || itemPurchased[index]) return;
        if (RunManager.instance == null) return;

        BaseItem item = currentItems[index];
        if (item == null) return;

        if (item.itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
        { StartCoroutine(FlashText(goldText)); return; }

        if (RunManager.instance.currentGold < item.price)
        { StartCoroutine(FlashText(goldText)); return; }

        RunManager.instance.currentGold -= item.price;
        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        if (item.itemType == ItemType.Instant)
        {
            if (secretItem != null && item.itemName == secretItem.itemName) hasBoughtSecretItem = true;
            if (!item.Use()) { RunManager.instance.currentGold += item.price; RefreshGold(); return; }
        }
        else
        {
            GameEvents.ItemPurchased(item);
        }

        if (secretItem != null && item.itemName == secretItem.itemName) hasBoughtSecretItem = true;

        itemPurchased[index] = true;
        if (index < itemSlots.Count)
        {
            if (itemSlots[index].soldOutOverlay != null) itemSlots[index].soldOutOverlay.SetActive(true);
            if (itemSlots[index].button != null) itemSlots[index].button.interactable = false;
        }

        RefreshGold();
        GameEvents.GoldChanged(RunManager.instance.currentGold);
    }

    private void RefreshItemAffordability()
    {
        if (RunManager.instance == null) return;
        for (int i = 0; i < itemSlots.Count && i < currentItems.Count; i++)
        {
            if (itemPurchased.Count > i && itemPurchased[i]) continue;
            if (itemSlots[i].button == null) continue;
            bool can = currentItems[i] != null && RunManager.instance.currentGold >= currentItems[i].price;
            if (can && currentItems[i].itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
                can = false;
            itemSlots[i].button.interactable = can;
        }
    }

    private void HideItemSection()
    {
        if (itemSection != null) itemSection.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // GOLD
    // ═══════════════════════════════════════════

    public void RefreshGold()
    {
        if (goldText != null && RunManager.instance != null)
            goldText.text = RunManager.instance.currentGold.ToString();
        RefreshRerollButton();
        RefreshItemAffordability();
        RefreshPerkAffordability();
    }

    // ═══════════════════════════════════════════
    // PERK HELPERS (LevelUpManager'dan kopyalandı)
    // ═══════════════════════════════════════════

    private bool AreAllPerksMaxed()
    {
        List<GameObject> all = new List<GameObject>();
        all.AddRange(commonPerks);
        all.AddRange(rarePerks);
        all.AddRange(epicPerks);
        all.AddRange(legendaryPerks);

        foreach (var p in all)
        {
            if (p == null) continue;
            if (!IsPerkMaxedOut(p)) return false;
        }
        return true;
    }

    private bool IsPerkMaxedOut(GameObject perkGO)
    {
        if (perkGO == null) return true;
        BasePerk perkScript = perkGO.GetComponent<BasePerk>();
        if (perkScript == null) return true;

        BasePerk existing = RunManager.instance?.activePerks.Find(p => p != null && p.GetType() == perkScript.GetType());
        if (existing == null) existing = RunManager.instance?.inventoryPerks.Find(p => p != null && p.GetType() == perkScript.GetType());
        return existing != null && existing.currentLevel >= existing.maxLevel;
    }

    private GameObject GetRandomPerkByRarity(bool forceLegendary)
    {
        if (forceLegendary && legendaryPerks.Count > 0)
            return legendaryPerks[Random.Range(0, legendaryPerks.Count)];

        // Lucky Clover kontrolü
        bool luckyClover = false;
        int cloverLevel = 0;
        if (RunManager.instance != null)
        {
            foreach (var p in RunManager.instance.activePerks)
            {
                if (p is LuckyCloverPerk lcp) { luckyClover = true; cloverLevel = lcp.currentLevel; break; }
            }
        }

        int level = RunManager.instance?.currentLevel ?? 0;

        float legendaryChance;
        if (luckyClover && cloverLevel >= 3) legendaryChance = 0.25f;
        else if (level >= 16) legendaryChance = 0.08f + (luckyClover ? cloverLevel * 0.02f : 0f);
        else if (level >= 8)  legendaryChance = 0.06f + (luckyClover ? cloverLevel * 0.02f : 0f);
        else                  legendaryChance = 0.04f + (luckyClover ? cloverLevel * 0.02f : 0f);

        float epicChance;
        float rareChance;
        if (luckyClover)
        {
            epicChance = cloverLevel >= 3 ? 0.25f : cloverLevel == 2 ? 0.25f : cloverLevel == 1 ? 0.17f : 0.10f;
            rareChance = 0.33f;
        }
        else { epicChance = 0.10f; rareChance = 0.30f; }

        float roll = Random.value;
        List<GameObject> pool;

        if (roll < legendaryChance)           pool = legendaryPerks;
        else if (roll < legendaryChance + epicChance) pool = epicPerks;
        else if (roll < legendaryChance + epicChance + rareChance) pool = rarePerks;
        else                                  pool = commonPerks;

        if (pool == null || pool.Count == 0) pool = commonPerks;
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private GameObject GetAnyValidFallback()
    {
        List<GameObject> all = new List<GameObject>();
        all.AddRange(commonPerks);
        all.AddRange(rarePerks);
        all.AddRange(epicPerks);
        all.AddRange(legendaryPerks);
        foreach (var p in all) if (!IsPerkMaxedOut(p)) return p;
        return null;
    }

    // ═══════════════════════════════════════════
    // UTIL
    // ═══════════════════════════════════════════

    private IEnumerator FlashText(TMP_Text t)
    {
        if (t == null) yield break;
        Color orig = t.color;
        t.color = Color.red;
        yield return new WaitForSecondsRealtime(0.3f);
        t.color = orig;
    }
}

// ─── Perk Slot ───
[System.Serializable]
public class MergedShopPerkSlot
{
    public GameObject  root;
    public Image       background;
    public Button      button;
    public TMP_Text    nameText;
    public TMP_Text    rarityText;
    public TMP_Text    levelText;
    public TMP_Text    descriptionText;
    public TMP_Text    priceText;
    public Image       iconImage;
    public GameObject  soldOutOverlay;
    [System.NonSerialized] public int price;

    public void Setup(GameObject perkGO, int index, MergedShopManager manager)
    {
        BasePerk perk = perkGO.GetComponent<BasePerk>();
        if (perk == null) return;

        if (nameText        != null) nameText.text        = perk.perkName.ToUpperInvariant();
        if (descriptionText != null) descriptionText.text = perk.description;

        // Rarity rengi
        Color col = GetRarityColor(perk.rarity);
        if (rarityText != null) { rarityText.text = perk.rarity.ToString().ToUpperInvariant(); rarityText.color = col; }
        if (nameText   != null) nameText.color = col;

        // Level
        BasePerk existing = RunManager.instance?.activePerks.Find(p => p != null && p.GetType() == perk.GetType());
        if (existing == null) existing = RunManager.instance?.inventoryPerks.Find(p => p != null && p.GetType() == perk.GetType());
        int fromLv = existing != null ? existing.currentLevel : 0;
        int toLv   = fromLv + 1;
        if (levelText != null) levelText.text = $"Lv {fromLv} <color=#00FF00>→ Lv {toLv}</color>";

        // Icon
        if (iconImage != null)
        {
            if (perk.icon != null) { iconImage.sprite = perk.icon; iconImage.color = Color.white; }
            else { iconImage.sprite = null; iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
        }

        // Rarity visual efekt (PerkCardRarityEffect)
        if (root != null)
        {
            PerkCardRarityEffect fx = root.GetComponent<PerkCardRarityEffect>();
            if (fx == null) fx = root.AddComponent<PerkCardRarityEffect>();
            RectTransform iconRT = iconImage != null ? iconImage.GetComponent<RectTransform>() : null;
            fx.Setup(perk.rarity, iconRT);
        }

        // Fiyat (rarity'ye göre)
        price = GetRarityPrice(perk.rarity);
        if (priceText != null) priceText.text = price.ToString();

        if (soldOutOverlay != null) soldOutOverlay.SetActive(false);

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => manager.SelectPerk(index));
        }
    }

    public static int GetRarityPrice(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Common:    return 8;
            case PerkRarity.Rare:      return 15;
            case PerkRarity.Epic:      return 25;
            case PerkRarity.Legendary: return 40;
            case PerkRarity.Secret:    return 50;
            default:                   return 10;
        }
    }

    private Color GetRarityColor(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Common:    return new Color(0.8f, 0.8f, 0.8f);
            case PerkRarity.Rare:      return new Color(0.27f, 0.53f, 1f);
            case PerkRarity.Epic:      return new Color(0.67f, 0.27f, 1f);
            case PerkRarity.Legendary: return new Color(1f, 0.67f, 0f);
            case PerkRarity.Secret:    return new Color(1f, 0.27f, 0.27f);
            default:                   return Color.white;
        }
    }
}

// ─── Item Slot ───
[System.Serializable]
public class MergedShopItemSlot
{
    public GameObject  root;
    public Image       background;
    public Button      button;
    public TMP_Text    nameText;
    public TMP_Text    descriptionText;
    public TMP_Text    priceText;
    public Image       iconImage;
    public GameObject  soldOutOverlay;

    public void Setup(BaseItem item, System.Action onBuy)
    {
        if (nameText        != null) nameText.text        = item.itemName.ToUpperInvariant();
        if (descriptionText != null) descriptionText.text = item.description;
        if (priceText       != null) priceText.text       = item.price.ToString();

        if (iconImage != null)
        {
            if (item.icon != null) { iconImage.sprite = item.icon; iconImage.color = Color.white; iconImage.enabled = true; }
            else { iconImage.sprite = null; iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
        }

        if (soldOutOverlay != null) soldOutOverlay.SetActive(false);

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onBuy?.Invoke());
        }
    }
}
