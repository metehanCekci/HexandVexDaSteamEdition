using System;
using UnityEngine;
using Random = UnityEngine.Random;
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

    private Action onCloseCallback;
    private bool openedAfterCombat;

    // ═══════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[MergedShop] Duplicate instance destroyed!");
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Referanslar kopuksa runtime'da otomatik bul
        if (panel == null) AutoWireReferences();

        Debug.Log($"[MergedShop] Awake: panel={panel != null}, perkSlots={perkSlots.Count}, itemSlots={itemSlots.Count}, canvasGroup={canvasGroup != null}, continueButton={continueButton != null}, rerollButton={perkRerollButton != null}");
        if (panel != null) panel.SetActive(false);

        // Gold event'ine abone ol — satış, F7 vb. gold değişince shop coin text güncellensin
        GameEvents.OnGoldChanged += OnGoldChangedHandler;
        UIColors.OnChanged       += OnColorsChanged;
        GameKeywords.OnChanged   += OnColorsChanged;

        // Kartlara ShopCardHover ekle (editor'den bake edilen eski EventTrigger'lari guncelle)
        UpgradeCardHovers();

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

        // Shop canvas: MapUI (90) üstünde, PerkInventoryUI (100) altında
        shopCanvas.sortingOrder = 95;

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

    // ─── Coin Icon Helper (text child olarak, text ile birlikte kayar) ───

    private RectTransform goldCoinRT;
    private RectTransform rerollCoinRT;

    private Sprite FindCoinSprite()
    {
        foreach (var s in perkSlots)
        {
            if (s.root == null) continue;
            var ci = s.root.transform.Find("CoinIcon");
            if (ci != null) { var img = ci.GetComponent<Image>(); if (img != null && img.sprite != null) return img.sprite; }
        }
        foreach (var s in itemSlots)
        {
            if (s.root == null) continue;
            var ci = s.root.transform.Find("CoinIcon");
            if (ci != null) { var img = ci.GetComponent<Image>(); if (img != null && img.sprite != null) return img.sprite; }
        }
        return null;
    }

    private RectTransform EnsureCoinChild(TMP_Text parent, string childName)
    {
        if (parent == null) return null;
        Transform existing = parent.transform.Find(childName);
        if (existing != null) return existing.GetComponent<RectTransform>();

        var go = new GameObject(childName, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20f, 20f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        Sprite coinSprite = FindCoinSprite();
        var icon = go.AddComponent<Image>();
        if (coinSprite != null) { icon.sprite = coinSprite; icon.color = Color.white; }
        else icon.color = new Color(1f, 0.85f, 0.2f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return rt;
    }

    private void EnsureAllCoinIcons()
    {
        goldCoinRT = EnsureCoinChild(goldText, "GoldCoinIcon");
        rerollCoinRT = EnsureCoinChild(perkRerollPriceText, "RerollCoinIcon");
    }

    private static void PositionCoinIcon(RectTransform coinRT, TMP_Text text)
    {
        if (coinRT == null || text == null) return;
        float halfW = text.preferredWidth * 0.5f;
        coinRT.anchoredPosition = new Vector2(halfW + 4f + coinRT.sizeDelta.x * 0.5f, 0f);
    }

    private static bool IsDisabledPerk(GameObject prefab)
    {
        if (prefab == null) return true;
        var bp = prefab.GetComponent<BasePerk>();
        if (bp == null) return false;
        return bp is SeismicStepPerk;
    }

    private void AutoPopulatePerkPools()
    {
        // Önce LevelUpManager'dan dene
        if (LevelUpManager.instance != null && LevelUpManager.instance.commonPerks.Count > 0)
        {
            commonPerks    = LevelUpManager.instance.commonPerks.FindAll(p => !IsDisabledPerk(p));
            rarePerks      = LevelUpManager.instance.rarePerks.FindAll(p => !IsDisabledPerk(p));
            epicPerks      = LevelUpManager.instance.epicPerks.FindAll(p => !IsDisabledPerk(p));
            legendaryPerks = LevelUpManager.instance.legendaryPerks.FindAll(p => !IsDisabledPerk(p));
            Debug.Log($"[MergedShop] Perk pools from LevelUpManager: C={commonPerks.Count} R={rarePerks.Count} E={epicPerks.Count} L={legendaryPerks.Count}");

            // Also feed SacrificeNodeManager while we have valid perk lists
            if (SacrificeNodeManager.instance != null)
                SacrificeNodeManager.instance.InjectPerkLists(rarePerks, epicPerks, legendaryPerks, GetSecretPerkPool());

            return;
        }

        // LevelUpManager yoksa: projedeki tüm BasePerk prefab'larını tara
        var allPerks = Resources.FindObjectsOfTypeAll<BasePerk>();
        foreach (var perk in allPerks)
        {
            // Scene objeleri değil, sadece prefab asset'leri al
            if (perk.gameObject.scene.IsValid()) continue;
            if (perk is SeismicStepPerk) continue;
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

        // Feed SacrificeNodeManager from fallback-populated lists too
        if (SacrificeNodeManager.instance != null && rarePerks.Count > 0)
            SacrificeNodeManager.instance.InjectPerkLists(rarePerks, epicPerks, legendaryPerks, GetSecretPerkPool());
    }

    private List<GameObject> GetSecretPerkPool()
    {
        if (secretItem is SecretPerkOrb orb && orb.secretPerkPool != null && orb.secretPerkPool.Count > 0)
            return orb.secretPerkPool;
        return null;
    }

    private void AutoPopulateItemPool()
    {
        var allItems = Resources.FindObjectsOfTypeAll<BaseItem>();
        foreach (var item in allItems)
        {
            if (item == null) continue;
            if (item is SecretPerkOrb)
            {
                if (secretItem == null) secretItem = item;
                continue;
            }
            if (item is LuckyClover) continue; // Lucky Clover pooldan çıkarıldı
            itemPool.Add(item);
        }

        // SecretPerkOrb FindObjectsOfTypeAll ile bulunamazsa Resources.Load dene
        if (secretItem == null)
        {
            var loaded = Resources.Load<SecretPerkOrb>("SecretPerkOrb");
            if (loaded != null) secretItem = loaded;
        }

        Debug.Log($"[MergedShop] AutoPopulate itemPool: {itemPool.Count} items, secretItem={secretItem != null}");
    }

    /// <summary>
    /// Sahnede zaten var olan kartlara ShopCardHover ekler ve
    /// EventTrigger callback'lerini yeni sisteme baglar.
    /// </summary>
    private void UpgradeCardHovers()
    {
        var allRoots = new List<GameObject>();
        foreach (var s in perkSlots) if (s.root != null) allRoots.Add(s.root);
        foreach (var s in itemSlots) if (s.root != null) allRoots.Add(s.root);

        foreach (var go in allRoots)
        {
            // ShopCardHover yoksa ekle
            if (go.GetComponent<ShopCardHover>() == null)
                go.AddComponent<ShopCardHover>();

            // EventTrigger varsa callback'leri guncelle
            var trigger = go.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger != null)
            {
                trigger.triggers.Clear();
                var enter = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                var capturedGo = go;
                enter.callback.AddListener((_) =>
                {
                    var hover = capturedGo.GetComponent<ShopCardHover>();
                    if (hover != null) hover.HoverIn();
                });
                trigger.triggers.Add(enter);

                var exit = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener((_) =>
                {
                    var hover = capturedGo.GetComponent<ShopCardHover>();
                    if (hover != null) hover.HoverOut();
                });
                trigger.triggers.Add(exit);
            }
        }
    }

    private static void AddRuntimeHoverScale(GameObject go)
    {
        if (go.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null) return;
        var trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((_) =>
        {
            var hover = go.GetComponent<ShopCardHover>();
            if (hover != null) hover.HoverIn();
            else go.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
        });
        trigger.triggers.Add(enter);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener((_) =>
        {
            var hover = go.GetComponent<ShopCardHover>();
            if (hover != null) hover.HoverOut();
            else go.transform.localScale = Vector3.one;
        });
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

    /// <summary>
    /// Combat (normal/elite/boss) bittikten sonra otomatik açılır.
    /// Ekran zaten siyah durumda. Shop kapandığında onClose callback çağrılır.
    /// </summary>
    public void OpenAfterCombat(Action onClose)
    {
        Debug.Log("[MergedShop] OpenAfterCombat called");
        openedAfterCombat = true;
        onCloseCallback = onClose;

        // PerkInventoryUI'ı shop'un üstünde tut — tıklanabilir olsun
        EnsurePerkPanelAboveShop();

        if (commonPerks.Count == 0 && LevelUpManager.instance != null)
        {
            commonPerks    = LevelUpManager.instance.commonPerks.FindAll(p => !IsDisabledPerk(p));
            rarePerks      = LevelUpManager.instance.rarePerks.FindAll(p => !IsDisabledPerk(p));
            epicPerks      = LevelUpManager.instance.epicPerks.FindAll(p => !IsDisabledPerk(p));
            legendaryPerks = LevelUpManager.instance.legendaryPerks.FindAll(p => !IsDisabledPerk(p));
        }

        // Always try to inject — perks may have been cached earlier via fallback
        if (SacrificeNodeManager.instance != null && rarePerks != null && rarePerks.Count > 0)
            SacrificeNodeManager.instance.InjectPerkLists(rarePerks, epicPerks, legendaryPerks, GetSecretPerkPool());

        perkRerollCount         = 0;
        currentPerkRerollCost   = perkRerollBaseCost;

        // Mutation Catalyst: ilk reroll bedava + sayaç sıfırdan başlar
        if (RunManager.instance != null && RunManager.instance.pendingRerollReset)
        {
            currentPerkRerollCost = 0f;
            RunManager.instance.pendingRerollReset = false;
        }

        shownItemNames.Clear();

        EnsureAllCoinIcons();
        GeneratePerkChoices();
        GenerateItemChoices();
        RefreshGold();
        RefreshRerollButton();

        if (panel != null)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            Canvas parentCanvas = panel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null && !parentCanvas.gameObject.activeSelf)
                parentCanvas.gameObject.SetActive(true);
            panel.SetActive(true);
        }

        // Combat sonrası hotbar (quick item menü) göster
        if (HotbarUI.instance != null)
            HotbarUI.instance.SetVisible(true);

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
        // Önce tüm kartları scale 0 yap (flash önleme)
        List<GameObject> allCards = new List<GameObject>();
        foreach (var s in perkSlots) if (s.root != null && s.root.activeSelf) allCards.Add(s.root);
        foreach (var s in itemSlots) if (s.root != null && s.root.activeSelf) allCards.Add(s.root);

        foreach (var card in allCards)
        {
            card.transform.localScale = Vector3.zero;
            var tilt = card.GetComponent<CardTilt3D>();
            if (tilt != null) tilt.scaleOverridden = true;
            var hover = card.GetComponent<ShopCardHover>();
            if (hover != null) hover.scaleOverridden = true;
        }

        // Combat sonrası açılıyorsa ekranın fade-in'ini bekle
        if (openedAfterCombat)
            yield return new WaitForSecondsRealtime(0.1f);

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

        // Pop-in: soldan sağa, üstten alta — her kart için ses efekti
        float stagger = 0.18f;
        for (int i = 0; i < allCards.Count; i++)
        {
            if (allCards[i] == null) continue;
            if (AudioManager.instance != null) AudioManager.instance.PlayCard();
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
        var tilt = card.GetComponent<CardTilt3D>();
        if (tilt != null) tilt.RefreshBaseScale();
        var hover = card.GetComponent<ShopCardHover>();
        if (hover != null) hover.RefreshBaseScale();
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

        if (openedAfterCombat && onCloseCallback != null)
        {
            // Combat sonrası açıldıysa callback ile devam et
            var cb = onCloseCallback;
            onCloseCallback = null;
            openedAfterCombat = false;
            cb.Invoke();
        }
        else if (MapManager.instance != null)
        {
            MapManager.instance.OnNodeComplete();
        }
    }

    // ═══════════════════════════════════════════
    // PERK SECTION
    // ═══════════════════════════════════════════

    private void GeneratePerkChoices()
    {
        currentPerkChoices.Clear();

        if (commonPerks.Count == 0 && rarePerks.Count == 0 && epicPerks.Count == 0 && legendaryPerks.Count == 0)
        { Debug.LogWarning("[MergedShop] All perk pools empty — hiding perk section"); HidePerkSection(); return; }

        // Shop'ta sadece sahip olunmayan perkler gösterilir — upgrade sadece kamp ateşinde yapılır
        bool allOwned = AreAllPerksOwnedOrMaxed();
        if (allOwned) { Debug.LogWarning("[MergedShop] All perks owned/maxed — hiding perk section"); HidePerkSection(); return; }

        if (perkSection != null) perkSection.SetActive(true);

        // openedAfterCombat: savaş sonrası açılan shop normal rarity kullanır (boss dahil)
        bool isBossReward = false;

        for (int i = 0; i < 3; i++)
        {
            GameObject pick = null;
            int safety = 0;
            while (pick == null || currentPerkChoices.Contains(pick) || IsPerkOwned(pick))
            {
                pick = GetRandomPerkByRarity(isBossReward);
                if (++safety > 50) { pick = GetAnyUnownedFallback(); break; }
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
                // Yeterli sahip olunmayan perk yok — slotu "sold out" olarak göster
                if (perkSlots[i].root != null) perkSlots[i].root.SetActive(true);
                if (perkSlots[i].soldOutOverlay != null) perkSlots[i].soldOutOverlay.SetActive(true);
                if (perkSlots[i].button != null) perkSlots[i].button.interactable = false;
                if (perkSlots[i].nameText != null) perkSlots[i].nameText.text = "SOLD OUT";
                if (perkSlots[i].descriptionText != null) perkSlots[i].descriptionText.text = "";
                if (perkSlots[i].levelText != null) perkSlots[i].levelText.text = "";
                if (perkSlots[i].rarityText != null) perkSlots[i].rarityText.text = "";
                if (perkSlots[i].priceText != null) perkSlots[i].priceText.text = "";
                if (perkSlots[i].iconImage != null) { perkSlots[i].iconImage.sprite = null; perkSlots[i].iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.3f); }
                Debug.Log($"[MergedShop]   slot[{i}] sold out (not enough unowned perks)");
            }
        }
    }

    public void SelectPerk(int index)
    {
        if (index >= currentPerkChoices.Count) return;
        if (index >= perkSlots.Count) return;
        GameObject perkGO = currentPerkChoices[index];
        if (perkGO == null) return;

        // Kapasite kontrolü — aktif + stash doluysa ve bu perk upgrade değilse alma
        if (RunManager.instance != null)
        {
            BasePerk prefabScript = perkGO.GetComponent<BasePerk>();
            bool isUpgrade = false;
            if (prefabScript != null)
            {
                System.Type perkType = prefabScript.GetType();
                isUpgrade = RunManager.instance.activePerks.Exists(p => p != null && p.GetType() == perkType)
                         || RunManager.instance.inventoryPerks.Exists(p => p != null && p.GetType() == perkType);
            }
            if (!isUpgrade
                && RunManager.instance.activePerks.Count >= RunManager.MAX_ACTIVE_PERKS
                && RunManager.instance.inventoryPerks.Count >= RunManager.MAX_INVENTORY_PERKS)
            {
                Debug.LogWarning("[MergedShop] Perk slots full (active + stash)! Cannot buy.");
                FlashStashFull();
                return;
            }
        }

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

        bool slotsFull = RunManager.instance.activePerks.Count >= RunManager.MAX_ACTIVE_PERKS
                      && RunManager.instance.inventoryPerks.Count >= RunManager.MAX_INVENTORY_PERKS;

        for (int i = 0; i < perkSlots.Count; i++)
        {
            if (perkSlots[i].button == null || !perkSlots[i].button.interactable) continue;
            bool canAfford = RunManager.instance.currentGold >= perkSlots[i].price;

            if (perkSlots[i].priceText != null)
                perkSlots[i].priceText.color = UIColors.GetAffordabilityColor(canAfford);
        }
    }

    private void TryReroll()
    {
        Debug.Log($"[MergedShop] >>> TryReroll CALLED on {this.GetType().Name}");
        if (RunManager.instance == null) { Debug.Log("[MergedShop] TryReroll: RunManager null!"); return; }

        // Mutation Catalyst shop açıkken kullanıldıysa: sayaç sıfırlanır, bu reroll bedava
        if (RunManager.instance.pendingRerollReset)
        {
            perkRerollCount = 0;
            currentPerkRerollCost = 0f;
            RunManager.instance.pendingRerollReset = false;
        }

        // Mutation Catalyst: bedava reroll (tek seferlik)
        bool freeReroll = RunManager.instance.hasPerkReroll;
        int goldBefore = RunManager.instance.currentGold;
        Debug.Log($"[MergedShop] TryReroll: hasPerkReroll={freeReroll}, baseCost={perkRerollBaseCost}, increment={perkRerollIncrement}, rerollCount={perkRerollCount}, currentCost={Mathf.RoundToInt(currentPerkRerollCost)}, gold={goldBefore}");
        if (!freeReroll)
        {
            int cost = Mathf.RoundToInt(currentPerkRerollCost);
            if (RunManager.instance.currentGold < cost) { StartCoroutine(FlashText(goldText)); return; }
            RunManager.instance.currentGold -= cost;
            Debug.Log($"[MergedShop] TryReroll CHARGED: {cost} gold. Before={goldBefore}, After={RunManager.instance.currentGold}");
            GameEvents.GoldChanged(RunManager.instance.currentGold);
        }
        else
        {
            RunManager.instance.hasPerkReroll = false;
            // Bedava reroll maliyeti artırmasın
        }

        if (!freeReroll)
        {
            perkRerollCount++;
            currentPerkRerollCost = perkRerollBaseCost + perkRerollIncrement * perkRerollCount;
        }

        GeneratePerkChoices();
        GenerateItemChoices();
        RefreshGold();
        RefreshRerollButton();
    }

    private void RefreshRerollButton()
    {
        if (RunManager.instance == null) return;
        bool free = RunManager.instance.hasPerkReroll;
        int cost = free ? 0 : Mathf.RoundToInt(currentPerkRerollCost);
        bool canAfford = free || RunManager.instance.currentGold >= cost;
        if (perkRerollPriceText != null)
            perkRerollPriceText.text = $"REROLL  <color=#{UIColors.GetAffordabilityHex(canAfford)}>{cost}</color>";
        PositionCoinIcon(rerollCoinRT, perkRerollPriceText);
        if (perkRerollButton != null)
            perkRerollButton.interactable = canAfford;
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
        foreach (var item in itemPool)
            if (!shownItemNames.Contains(item.itemName) && !IsItemInInventory(item)) available++;
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
                || picked is LuckyClover
                || IsItemInInventory(picked));

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
            bool canAfford = currentItems[i] != null && RunManager.instance.currentGold >= currentItems[i].price;
            bool can = canAfford;
            if (can && currentItems[i].itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
                can = false;
            itemSlots[i].button.interactable = can;
            if (itemSlots[i].priceText != null)
                itemSlots[i].priceText.color = UIColors.GetAffordabilityColor(canAfford);
        }
    }

    /// <summary>Oyuncunun envanterinde (hotbar) zaten bu item var mı?</summary>
    private bool IsItemInInventory(BaseItem item)
    {
        if (item == null || InventoryManager.instance == null) return false;
        // Sadece consumable itemleri filtrele — instant itemler envanterde tutulmaz
        if (item.itemType != ItemType.Consumable) return false;
        for (int i = 0; i < InventoryManager.instance.SlotCount; i++)
        {
            BaseItem slot = InventoryManager.instance.GetItem(i);
            if (slot != null && slot.itemName == item.itemName) return true;
        }
        return false;
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
        PositionCoinIcon(goldCoinRT, goldText);
        RefreshRerollButton();
        RefreshItemAffordability();
        RefreshPerkAffordability();
    }

    private void OnGoldChangedHandler(int newGold)
    {
        RefreshGold();
    }

    void OnDestroy()
    {
        GameEvents.OnGoldChanged -= OnGoldChangedHandler;
        UIColors.OnChanged       -= OnColorsChanged;
        GameKeywords.OnChanged   -= OnColorsChanged;
    }

    private void OnColorsChanged()
    {
        if (panel != null && panel.activeSelf) RefreshGold();
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

    /// <summary>
    /// Oyuncunun zaten sahip olduğu (herhangi bir seviyede) perk mi?
    /// Shop'ta sahip olunan perkler gösterilmez — upgrade sadece kamp ateşinde yapılır.
    /// </summary>
    private bool IsPerkOwned(GameObject perkGO)
    {
        if (perkGO == null) return false;
        if (RunManager.instance == null) return false;
        BasePerk perkScript = perkGO.GetComponent<BasePerk>();
        if (perkScript == null) return false;

        System.Type perkType = perkScript.GetType();
        if (RunManager.instance.activePerks.Exists(p => p != null && p.GetType() == perkType)) return true;
        if (RunManager.instance.inventoryPerks.Exists(p => p != null && p.GetType() == perkType)) return true;
        return false;
    }

    private GameObject GetRandomPerkByRarity(bool forceLegendary)
    {
        if (forceLegendary && legendaryPerks.Count > 0)
            return legendaryPerks[Random.Range(0, legendaryPerks.Count)];

        // Sabit dağılım: Common %70 / Rare %15 / Epic %10 / Legendary %5
        float legendaryChance = 0.05f;
        float epicChance      = 0.10f;
        float rareChance      = 0.15f;

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

    /// <summary>Havuzdaki tüm perkler zaten sahip mi veya maxed mi?</summary>
    private bool AreAllPerksOwnedOrMaxed()
    {
        List<GameObject> all = new List<GameObject>();
        all.AddRange(commonPerks);
        all.AddRange(rarePerks);
        all.AddRange(epicPerks);
        all.AddRange(legendaryPerks);

        foreach (var p in all)
        {
            if (p == null) continue;
            if (!IsPerkOwned(p)) return false;
        }
        return true;
    }

    /// <summary>Sahip olunmayan herhangi bir perk döndürür (fallback).</summary>
    private GameObject GetAnyUnownedFallback()
    {
        List<GameObject> all = new List<GameObject>();
        all.AddRange(commonPerks);
        all.AddRange(rarePerks);
        all.AddRange(epicPerks);
        all.AddRange(legendaryPerks);
        foreach (var p in all) if (p != null && !IsPerkOwned(p)) return p;
        return null;
    }

    // ═══════════════════════════════════════════
    // PERK PANEL ERIŞIMI
    // ═══════════════════════════════════════════

    /// <summary>
    /// Shop açıkken PerkInventoryUI'ın tıklanabilir olmasını garanti eder.
    /// Canvas sorting order'ını shop'un üstüne çeker ve interaction'ı açar.
    /// </summary>
    private void EnsurePerkPanelAboveShop()
    {
        if (PerkInventoryUI.instance == null) return;
        if (!PerkInventoryUI.instance.IsOpen)
            PerkInventoryUI.instance.Show();

        // Shop canvas'ının sorting order'ını bul
        int shopOrder = 95;
        if (panel != null)
        {
            Canvas shopCanvas = panel.GetComponentInParent<Canvas>(true);
            if (shopCanvas != null) shopOrder = shopCanvas.sortingOrder;
        }

        // PerkInventoryUI canvas'ını shop'un üstüne çek
        Canvas perkCanvas = PerkInventoryUI.instance.canvasGO != null
            ? PerkInventoryUI.instance.canvasGO.GetComponent<Canvas>()
            : null;
        if (perkCanvas != null)
        {
            int targetOrder = Mathf.Max(shopOrder + 10, 100);
            if (perkCanvas.sortingOrder <= shopOrder)
                perkCanvas.sortingOrder = targetOrder;
        }

        // GraphicRaycaster olduğundan emin ol
        if (PerkInventoryUI.instance.canvasGO != null
            && PerkInventoryUI.instance.canvasGO.GetComponent<GraphicRaycaster>() == null)
            PerkInventoryUI.instance.canvasGO.AddComponent<GraphicRaycaster>();
    }

    // ═══════════════════════════════════════════
    // UTIL
    // ═══════════════════════════════════════════

    private IEnumerator FlashText(TMP_Text t)
    {
        if (t == null) yield break;
        Color orig = t.color;
        t.color = UIColors.CantAffordColor;
        yield return new WaitForSecondsRealtime(0.3f);
        t.color = orig;
    }

    private void FlashStashFull()
    {
        if (PerkInventoryUI.instance != null && PerkInventoryUI.instance.stashTitleText != null)
            StartCoroutine(ShakeAndFlashText(PerkInventoryUI.instance.stashTitleText));
    }

    private IEnumerator ShakeAndFlashText(TMP_Text t)
    {
        if (t == null) yield break;
        Color orig = t.color;
        RectTransform rt = t.GetComponent<RectTransform>();
        Vector2 origPos = rt != null ? rt.anchoredPosition : Vector2.zero;

        // Kırmızı flash + titreşim
        t.color = UIColors.CantAffordColor;
        float duration = 0.4f;
        float elapsed = 0f;
        float shakeIntensity = 6f;

        while (elapsed < duration)
        {
            if (rt != null)
            {
                float x = Random.Range(-shakeIntensity, shakeIntensity);
                float y = Random.Range(-shakeIntensity * 0.3f, shakeIntensity * 0.3f);
                rt.anchoredPosition = origPos + new Vector2(x, y);
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Geri yükle
        if (rt != null) rt.anchoredPosition = origPos;
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

        // ── Önce tüm visual state'i temizle ──
        if (soldOutOverlay != null) soldOutOverlay.SetActive(false);
        if (background != null) background.color = new Color(0f, 0.02f, 0.047f, 1f);
        if (button != null)
        {
            button.interactable = true;
            // Button ColorBlock'un disabled tint kalıntısını temizle
            var cb = button.colors;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = cb;
        }

        if (nameText        != null) nameText.text        = perk.perkName.ToUpperInvariant();
        perk.RebuildDescription();
        if (descriptionText != null) descriptionText.text = perk.description;

        // Rarity rengi
        Color col = GetRarityColor(perk.rarity);
        if (rarityText != null) { rarityText.text = perk.rarity.ToString().ToUpperInvariant(); rarityText.color = col; }
        if (nameText   != null) nameText.color = col;

        // Level — shop'ta sadece sahip olunmayan perkler gösterilir, her zaman Lv 1
        if (levelText != null) levelText.text = "Lv 1";

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

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => manager.SelectPerk(index));
        }
    }

    public static int GetRarityPrice(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Common:    return 15;
            case PerkRarity.Rare:      return 20;
            case PerkRarity.Epic:      return 30;
            case PerkRarity.Legendary: return 40;
            case PerkRarity.Secret:    return 50;
            default:                   return 10;
        }
    }

    private Color GetRarityColor(PerkRarity r) => UIColors.GetRarityColor(r);
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

    public void Setup(BaseItem item, Action onBuy)
    {
        // ── Önce tüm visual state'i temizle ──
        if (soldOutOverlay != null) soldOutOverlay.SetActive(false);
        if (background != null) background.color = new Color(0f, 0.02f, 0.047f, 1f);
        if (button != null)
        {
            button.interactable = true;
            var cb = button.colors;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = cb;
        }

        if (nameText        != null) nameText.text        = item.itemName.ToUpperInvariant();
        item.RebuildDescription();
        if (descriptionText != null) descriptionText.text = item.description;
        if (priceText       != null) priceText.text       = item.price.ToString();

        if (iconImage != null)
        {
            if (item.icon != null) { iconImage.sprite = item.icon; iconImage.color = Color.white; iconImage.enabled = true; }
            else { iconImage.sprite = null; iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onBuy?.Invoke());
        }
    }
}
