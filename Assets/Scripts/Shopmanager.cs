using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Map-node Shop Manager — full-screen perk-card-style shop canvas.
/// Opens when player walks to dealer NPC in the shop arena, or via OpenAsMapNode().
/// Items display as large cards (like LevelUpManager perk cards) with all info visible.
/// Communicates purchases through GameEvents.
/// </summary>
public class Shopmanager : MonoBehaviour
{
    public static Shopmanager instance;

    [Header("Item Pool")]
    public List<BaseItem> itemPool = new List<BaseItem>();

    [Header("Secret Item")]
    public BaseItem secretItem;
    [Range(0f, 1f)] public float secretItemChance = 0.001f;

    [Header("Shop Config")]
    public int shopSlotCount = 3;

    [Header("Reroll Settings")]
    public float rerollBaseCost = 10f;
    public float rerollMultiplier = 1.5f;

    // ─── Legacy Inspector References (kept for backward compat) ───
    [Header("Legacy (auto-created if null)")]
    public Transform shopSlotContainer;
    public GameObject shopSlotPrefab;
    public TMP_Text coinDisplayText;
    public Button rerollButton;
    public TMP_Text rerollPriceText;
    public GameObject continueButton;

    // ─── Internal State ───
    private List<BaseItem> currentItems = new List<BaseItem>();
    private List<bool> purchased = new List<bool>();

    private int rerollCount = 0;
    private int currentRerollCost;

    public static bool hasBoughtSecretItem = false;

    private bool isShopOpen = false;

    // ─── Code-Built UI ───
    private GameObject shopCanvasGO;
    private CanvasGroup shopCanvasGroup;
    private GameObject shopPanel;
    private GameObject cardContainer;
    private List<ShopCardUI> cardUIs = new List<ShopCardUI>();
    private TMP_Text goldText;
    private Button codeRerollButton;
    private TMP_Text codeRerollPriceText;
    private Button codeContinueButton;
    private TMP_Text shopTitleText;

    private static bool shopUIBuilt = false;

    // ─── Card hover state ───
    private int hoveredCardIndex = -1;

    // ═══════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);

        if (RunManager.instance != null && RunManager.instance.currentLevel <= 1)
            hasBoughtSecretItem = false;

        BuildShopUI();
        CloseShop();
    }

    // ═══════════════════════════════════════════
    // UI CONSTRUCTION (Perk-Card Style)
    // ═══════════════════════════════════════════

    private void BuildShopUI()
    {
        if (shopUIBuilt) return;
        shopUIBuilt = true;

        // ─── Canvas ───
        shopCanvasGO = new GameObject("ShopCanvas");
        Canvas canvas = shopCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 92; // Above map (90), below rest (95)
        var scaler = shopCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        shopCanvasGO.AddComponent<GraphicRaycaster>();

        // ─── Full-screen panel (dark background) ───
        shopPanel = MakeUIObj("ShopPanel", shopCanvasGO.transform);
        StretchFull(shopPanel.GetComponent<RectTransform>());
        Image panelBG = shopPanel.AddComponent<Image>();
        panelBG.color = new Color(0.03f, 0.03f, 0.06f, 0.95f);
        shopCanvasGroup = shopPanel.AddComponent<CanvasGroup>();

        // ─── Title ───
        GameObject titleGO = MakeUIObj("ShopTitle", shopPanel.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta = new Vector2(500f, 70f);
        shopTitleText = titleGO.AddComponent<TextMeshProUGUI>();
        shopTitleText.text = "SHOP";
        shopTitleText.fontSize = 48;
        shopTitleText.alignment = TextAlignmentOptions.Center;
        shopTitleText.color = new Color(1f, 0.85f, 0.3f);
        shopTitleText.fontStyle = FontStyles.Bold;

        // ─── Gold Display (top-right) ───
        GameObject goldGO = MakeUIObj("GoldDisplay", shopPanel.transform);
        RectTransform goldRT = goldGO.GetComponent<RectTransform>();
        goldRT.anchorMin = new Vector2(1f, 1f);
        goldRT.anchorMax = new Vector2(1f, 1f);
        goldRT.pivot = new Vector2(1f, 1f);
        goldRT.anchoredPosition = new Vector2(-40f, -40f);
        goldRT.sizeDelta = new Vector2(200f, 50f);
        goldText = goldGO.AddComponent<TextMeshProUGUI>();
        goldText.fontSize = 32;
        goldText.alignment = TextAlignmentOptions.Right;
        goldText.color = new Color(1f, 0.85f, 0.2f);
        goldText.fontStyle = FontStyles.Bold;

        // ─── Card Container (centered) ───
        cardContainer = MakeUIObj("CardContainer", shopPanel.transform);
        RectTransform cardContRT = cardContainer.GetComponent<RectTransform>();
        cardContRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardContRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardContRT.pivot = new Vector2(0.5f, 0.5f);
        cardContRT.anchoredPosition = new Vector2(0f, 20f);

        float cardWidth = 280f;
        float cardSpacing = 30f;
        float totalW = shopSlotCount * cardWidth + (shopSlotCount - 1) * cardSpacing;
        cardContRT.sizeDelta = new Vector2(totalW, 420f);

        // ─── Create Item Cards ───
        cardUIs.Clear();
        for (int i = 0; i < shopSlotCount; i++)
        {
            CreateCardUI(i, cardWidth, cardSpacing);
        }

        // ─── Reroll Button (bottom-left) ───
        GameObject rerollGO = MakeUIObj("RerollBtn", shopPanel.transform);
        RectTransform rerollRT = rerollGO.GetComponent<RectTransform>();
        rerollRT.anchorMin = new Vector2(0.5f, 0f);
        rerollRT.anchorMax = new Vector2(0.5f, 0f);
        rerollRT.pivot = new Vector2(1f, 0f);
        rerollRT.anchoredPosition = new Vector2(-20f, 40f);
        rerollRT.sizeDelta = new Vector2(220f, 60f);
        Image rerollBG = rerollGO.AddComponent<Image>();
        rerollBG.color = new Color(0.25f, 0.2f, 0.4f, 0.9f);
        codeRerollButton = rerollGO.AddComponent<Button>();
        codeRerollButton.onClick.AddListener(TryReroll);
        ColorBlock rcb = codeRerollButton.colors;
        rcb.highlightedColor = new Color(0.4f, 0.3f, 0.6f);
        rcb.pressedColor = new Color(0.2f, 0.15f, 0.3f);
        rcb.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        codeRerollButton.colors = rcb;

        GameObject rerollTxtGO = MakeUIObj("RerollText", rerollGO.transform);
        StretchFull(rerollTxtGO.GetComponent<RectTransform>());
        codeRerollPriceText = rerollTxtGO.AddComponent<TextMeshProUGUI>();
        codeRerollPriceText.fontSize = 22;
        codeRerollPriceText.alignment = TextAlignmentOptions.Center;
        codeRerollPriceText.color = Color.white;
        codeRerollPriceText.richText = true;

        // ─── Continue Button (bottom-right) ───
        GameObject contGO = MakeUIObj("ContinueBtn", shopPanel.transform);
        RectTransform contRT = contGO.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0.5f, 0f);
        contRT.anchorMax = new Vector2(0.5f, 0f);
        contRT.pivot = new Vector2(0f, 0f);
        contRT.anchoredPosition = new Vector2(20f, 40f);
        contRT.sizeDelta = new Vector2(220f, 60f);
        Image contBG = contGO.AddComponent<Image>();
        contBG.color = new Color(0.2f, 0.5f, 0.8f, 0.9f);
        codeContinueButton = contGO.AddComponent<Button>();
        codeContinueButton.onClick.AddListener(CloseMapNodeShop);
        ColorBlock ccb = codeContinueButton.colors;
        ccb.highlightedColor = new Color(0.3f, 0.6f, 0.9f);
        ccb.pressedColor = new Color(0.15f, 0.35f, 0.6f);
        codeContinueButton.colors = ccb;

        GameObject contTxtGO = MakeUIObj("ContText", contGO.transform);
        StretchFull(contTxtGO.GetComponent<RectTransform>());
        TMP_Text contTxt = contTxtGO.AddComponent<TextMeshProUGUI>();
        contTxt.text = "CONTINUE";
        contTxt.fontSize = 24;
        contTxt.alignment = TextAlignmentOptions.Center;
        contTxt.color = Color.white;
        contTxt.fontStyle = FontStyles.Bold;

        shopCanvasGO.SetActive(false);
    }

    // ─── Individual Card (perk-card style) ───
    private void CreateCardUI(int index, float cardWidth, float cardSpacing)
    {
        float totalW = shopSlotCount * cardWidth + (shopSlotCount - 1) * cardSpacing;
        float startX = -totalW / 2f + cardWidth / 2f;

        GameObject cardGO = MakeUIObj($"ShopCard_{index}", cardContainer.transform);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = new Vector2(startX + index * (cardWidth + cardSpacing), 0f);
        cardRT.sizeDelta = new Vector2(cardWidth, 400f);

        // Card background
        Image cardBG = cardGO.AddComponent<Image>();
        cardBG.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        // Buy button (entire card is clickable)
        Button buyBtn = cardGO.AddComponent<Button>();
        int idx = index;
        buyBtn.onClick.AddListener(() => TryBuy(idx));
        ColorBlock cb = buyBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.95f, 0.7f);
        cb.pressedColor = new Color(0.8f, 0.75f, 0.5f);
        cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        buyBtn.colors = cb;

        // Hover events for scale animation
        EventTrigger trigger = cardGO.AddComponent<EventTrigger>();
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((_) => { hoveredCardIndex = idx; });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((_) => { if (hoveredCardIndex == idx) hoveredCardIndex = -1; });
        trigger.triggers.Add(exitEntry);

        // ─── Card Content Layout ───
        var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 16, 16);
        vlg.spacing = 8;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        // Item Name
        GameObject nameGO = MakeUIObj("ItemName", cardGO.transform);
        nameGO.layer = cardGO.layer;
        TMP_Text nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 22;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.fontStyle = FontStyles.Bold;
        nameText.raycastTarget = false;
        LayoutElement nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 36f;

        // Separator
        GameObject sep1GO = MakeUIObj("Sep1", cardGO.transform);
        sep1GO.layer = cardGO.layer;
        Image sep1Img = sep1GO.AddComponent<Image>();
        sep1Img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        sep1Img.raycastTarget = false;
        LayoutElement sep1LE = sep1GO.AddComponent<LayoutElement>();
        sep1LE.preferredHeight = 2f;
        sep1LE.minHeight = 2f;

        // Icon
        GameObject iconGO = MakeUIObj("Icon", cardGO.transform);
        iconGO.layer = cardGO.layer;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 120f;
        iconLE.preferredWidth = 120f;

        // Description
        GameObject descGO = MakeUIObj("Description", cardGO.transform);
        descGO.layer = cardGO.layer;
        TMP_Text descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.fontSize = 15;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.85f);
        descText.enableWordWrapping = true;
        descText.raycastTarget = false;
        LayoutElement descLE = descGO.AddComponent<LayoutElement>();
        descLE.preferredHeight = 80f;
        descLE.flexibleHeight = 1f;

        // Separator 2
        GameObject sep2GO = MakeUIObj("Sep2", cardGO.transform);
        sep2GO.layer = cardGO.layer;
        Image sep2Img = sep2GO.AddComponent<Image>();
        sep2Img.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        sep2Img.raycastTarget = false;
        LayoutElement sep2LE = sep2GO.AddComponent<LayoutElement>();
        sep2LE.preferredHeight = 2f;
        sep2LE.minHeight = 2f;

        // Price Row
        GameObject priceRowGO = MakeUIObj("PriceRow", cardGO.transform);
        priceRowGO.layer = cardGO.layer;
        HorizontalLayoutGroup priceHLG = priceRowGO.AddComponent<HorizontalLayoutGroup>();
        priceHLG.spacing = 6f;
        priceHLG.childAlignment = TextAnchor.MiddleCenter;
        priceHLG.childControlWidth = false;
        priceHLG.childControlHeight = false;
        priceHLG.childForceExpandWidth = false;
        priceHLG.childForceExpandHeight = false;
        LayoutElement priceRowLE = priceRowGO.AddComponent<LayoutElement>();
        priceRowLE.preferredHeight = 36f;

        // Coin Icon in price row
        GameObject coinGO = MakeUIObj("CoinIcon", priceRowGO.transform);
        coinGO.layer = cardGO.layer;
        Image coinImg = coinGO.AddComponent<Image>();
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;
        RectTransform coinRT = coinGO.GetComponent<RectTransform>();
        coinRT.sizeDelta = new Vector2(26f, 26f);
        LayoutElement coinLE = coinGO.AddComponent<LayoutElement>();
        coinLE.preferredWidth = 26f;
        coinLE.preferredHeight = 26f;

        // Try to set coin sprite
        Sprite coinSpr = null;
        if (TurnManager.instance != null && TurnManager.instance.coinSprite != null)
            coinSpr = TurnManager.instance.coinSprite;
        if (coinSpr == null)
        {
            var vfx = Object.FindFirstObjectByType<CoinDropVFX>();
            if (vfx != null) coinSpr = vfx.coinSprite;
        }
        if (coinSpr != null) coinImg.sprite = coinSpr;

        // Price text
        GameObject priceTxtGO = MakeUIObj("PriceText", priceRowGO.transform);
        priceTxtGO.layer = cardGO.layer;
        TMP_Text priceText = priceTxtGO.AddComponent<TextMeshProUGUI>();
        priceText.fontSize = 24;
        priceText.alignment = TextAlignmentOptions.Left;
        priceText.color = new Color(1f, 0.85f, 0.2f);
        priceText.fontStyle = FontStyles.Bold;
        priceText.raycastTarget = false;
        RectTransform priceTxtRT = priceTxtGO.GetComponent<RectTransform>();
        priceTxtRT.sizeDelta = new Vector2(60f, 30f);
        LayoutElement priceTxtLE = priceTxtGO.AddComponent<LayoutElement>();
        priceTxtLE.preferredWidth = 60f;
        priceTxtLE.preferredHeight = 30f;

        // Sold Out overlay
        GameObject soldOutGO = MakeUIObj("SoldOut", cardGO.transform);
        soldOutGO.layer = cardGO.layer;
        // Remove from layout so it overlays
        soldOutGO.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform soldOutRT = soldOutGO.GetComponent<RectTransform>();
        StretchFull(soldOutRT);
        Image soldOutBG = soldOutGO.AddComponent<Image>();
        soldOutBG.color = new Color(0f, 0f, 0f, 0.7f);
        soldOutBG.raycastTarget = false;

        GameObject soldOutTxtGO = MakeUIObj("SoldOutText", soldOutGO.transform);
        StretchFull(soldOutTxtGO.GetComponent<RectTransform>());
        TMP_Text soldOutTxt = soldOutTxtGO.AddComponent<TextMeshProUGUI>();
        soldOutTxt.text = "SOLD";
        soldOutTxt.fontSize = 36;
        soldOutTxt.alignment = TextAlignmentOptions.Center;
        soldOutTxt.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        soldOutTxt.fontStyle = FontStyles.Bold;
        soldOutTxt.raycastTarget = false;

        soldOutGO.SetActive(false);

        ShopCardUI card = new ShopCardUI
        {
            root = cardGO,
            background = cardBG,
            button = buyBtn,
            nameText = nameText,
            iconImage = iconImg,
            descriptionText = descText,
            priceText = priceText,
            coinImage = coinImg,
            soldOutOverlay = soldOutGO
        };

        cardUIs.Add(card);
    }

    // ═══════════════════════════════════════════
    // SHOP OPEN / CLOSE
    // ═══════════════════════════════════════════

    /// <summary>
    /// Called by MapManager when player enters a Shop node.
    /// </summary>
    public void OpenAsMapNode()
    {
        rerollCount = 0;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);
        GenerateShopItems();
        RefreshCoinDisplay();

        if (shopCanvasGO != null)
            shopCanvasGO.SetActive(true);

        isShopOpen = true;
        hoveredCardIndex = -1;

        // Start card animations
        StopAllCoroutines();
        StartCoroutine(ShopOpenAnimation());

        GameEvents.ShopOpened();
    }

    private void CloseMapNodeShop()
    {
        StopAllCoroutines();
        StartCoroutine(ShopCloseAnimation());
    }

    public void CloseShop()
    {
        if (shopCanvasGO != null)
            shopCanvasGO.SetActive(false);

        if (isShopOpen)
        {
            isShopOpen = false;
            GameEvents.ShopClosed();
        }
    }

    // ═══════════════════════════════════════════
    // ANIMATIONS (matches LevelUpManager style)
    // ═══════════════════════════════════════════

    private IEnumerator ShopOpenAnimation()
    {
        // Fade in panel
        if (shopCanvasGroup != null) shopCanvasGroup.alpha = 0f;

        // Hide all cards initially
        for (int i = 0; i < cardUIs.Count; i++)
        {
            if (i < currentItems.Count && cardUIs[i].root.activeSelf)
                cardUIs[i].root.transform.localScale = Vector3.zero;
        }

        // Fade in background
        float fadeDur = 0.2f;
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            if (shopCanvasGroup != null)
                shopCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDur);
            yield return null;
        }
        if (shopCanvasGroup != null) shopCanvasGroup.alpha = 1f;

        // Pop in cards one by one
        for (int i = 0; i < cardUIs.Count; i++)
        {
            if (i >= currentItems.Count || !cardUIs[i].root.activeSelf) continue;
            yield return StartCoroutine(CardPopIn(cardUIs[i].root.transform));
            if (i < cardUIs.Count - 1)
                yield return new WaitForSecondsRealtime(0.12f);
        }

        // Start idle bounce on all cards
        for (int i = 0; i < cardUIs.Count; i++)
        {
            if (i < currentItems.Count && cardUIs[i].root.activeSelf)
                StartCoroutine(CardIdleBounce(cardUIs[i].root.transform, i));
        }
    }

    private IEnumerator ShopCloseAnimation()
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float ease = t * t;
            if (shopCanvasGroup != null)
                shopCanvasGroup.alpha = Mathf.Lerp(1f, 0f, ease);
            yield return null;
        }

        CloseShop();

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    private IEnumerator CardPopIn(Transform card)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayCard();
        float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float ease = 1f - (1f - t) * (1f - t);
            float s = Mathf.Lerp(0f, 1.08f, ease);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        dur = 0.1f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float s = Mathf.Lerp(1.08f, 1f, t);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private IEnumerator CardIdleBounce(Transform card, int cardIdx)
    {
        while (card != null && card.gameObject.activeSelf && isShopOpen)
        {
            float target = (hoveredCardIndex == cardIdx) ? 1.08f : 1f;
            float cur = card.localScale.x;
            float s = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * 3f);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════
    // COMBAT COMPLETION HOOKS (Legacy compat)
    // ═══════════════════════════════════════════

    public void OnDungeonCleared()
    {
        RefreshCoinDisplay();
        if (MapManager.instance != null) { MapManager.instance.OnNodeComplete(); return; }
        if (LevelUpManager.instance != null) LevelUpManager.instance.ShowLevelUpScreen();
        else { RunManager.instance.currentLevel++; LevelGenerator.instance.GenerateNextLevel(); }
    }

    public void OnBossCleared()
    {
        rerollCount = 0;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost);
        RefreshCoinDisplay();
        if (MapManager.instance != null) { MapManager.instance.OnNodeComplete(); return; }
        if (LevelUpManager.instance != null) LevelUpManager.instance.ShowLevelUpScreen();
        else { RunManager.instance.currentLevel++; LevelGenerator.instance.GenerateNextLevel(); }
    }

    // ═══════════════════════════════════════════
    // REROLL
    // ═══════════════════════════════════════════

    public void TryReroll()
    {
        if (RunManager.instance == null) return;
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        if (RunManager.instance.currentGold < currentRerollCost)
        {
            StartCoroutine(FlashText(goldText));
            return;
        }

        RunManager.instance.currentGold -= currentRerollCost;
        rerollCount++;
        currentRerollCost = Mathf.RoundToInt(rerollBaseCost * Mathf.Pow(rerollMultiplier, rerollCount));
        RunManager.instance.shopRerollStack++;

        GenerateShopItems();
        RefreshCoinDisplay();
        GameEvents.GoldChanged(RunManager.instance.currentGold);

        if (RunManager.instance != null)
            foreach (var perk in RunManager.instance.activePerks)
                if (perk != null) perk.OnShopReroll();

        // Re-animate cards
        hoveredCardIndex = -1;
        StopAllCoroutines();
        StartCoroutine(ShopOpenAnimation());
    }

    // ═══════════════════════════════════════════
    // ITEM GENERATION
    // ═══════════════════════════════════════════

    public void GenerateShopItems()
    {
        currentItems.Clear();
        purchased.Clear();

        if (itemPool.Count == 0) return;

        // Secret item injection
        int secretSlotIndex = -1;
        bool guaranteeSecret = (RunManager.instance != null && RunManager.instance.currentLevel >= 6
                                && !hasBoughtSecretItem && rerollCount == 0);
        bool rollSecret = Random.value < secretItemChance;

        if (secretItem != null && !hasBoughtSecretItem && (guaranteeSecret || rollSecret))
            secretSlotIndex = Random.Range(0, shopSlotCount);

        List<int> usedIndices = new List<int>();

        for (int i = 0; i < shopSlotCount; i++)
        {
            BaseItem selectedItem = null;

            if (i == secretSlotIndex)
            {
                selectedItem = secretItem;
            }
            else
            {
                if (itemPool.Count <= usedIndices.Count) break;

                int poolIdx;
                int safety = 0;
                do
                {
                    poolIdx = Random.Range(0, itemPool.Count);
                    selectedItem = itemPool[poolIdx];
                    if (++safety > 100) break;
                }
                while (usedIndices.Contains(poolIdx) ||
                       (selectedItem != null && secretItem != null && selectedItem.itemName == secretItem.itemName) ||
                       (RunManager.instance != null && RunManager.instance.hasPerkReroll && selectedItem is MutationCatalyst));

                usedIndices.Add(poolIdx);
            }

            if (selectedItem == null) continue;

            currentItems.Add(selectedItem);
            purchased.Add(false);
        }

        // Populate card UIs
        for (int i = 0; i < cardUIs.Count; i++)
        {
            if (i < currentItems.Count)
            {
                PopulateCard(cardUIs[i], currentItems[i]);
                cardUIs[i].root.SetActive(true);
                cardUIs[i].soldOutOverlay.SetActive(false);
                cardUIs[i].button.interactable = true;
            }
            else
            {
                cardUIs[i].root.SetActive(false);
            }
        }

        RefreshCoinDisplay();
    }

    private void PopulateCard(ShopCardUI card, BaseItem item)
    {
        card.nameText.text = item.itemName.ToUpper();

        if (item.icon != null)
        {
            card.iconImage.sprite = item.icon;
            card.iconImage.color = Color.white;
            card.iconImage.enabled = true;
        }
        else
        {
            card.iconImage.sprite = null;
            card.iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        }

        card.descriptionText.text = item.description;
        card.priceText.text = item.price.ToString();
    }

    // ═══════════════════════════════════════════
    // PURCHASING
    // ═══════════════════════════════════════════

    public void TryBuy(int index)
    {
        if (index >= purchased.Count || purchased[index]) return;
        if (RunManager.instance == null) return;
        if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive) return;
        if (SecretPerkCinematic.instance != null && SecretPerkCinematic.instance.IsPlaying) return;

        BaseItem item = currentItems[index];
        if (item == null) return;

        // Check inventory space for consumables
        if (item.itemType == ItemType.Consumable)
        {
            if (InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
            {
                StartCoroutine(FlashText(goldText));
                return;
            }
        }

        if (RunManager.instance.currentGold < item.price)
        {
            StartCoroutine(FlashText(goldText));
            return;
        }

        RunManager.instance.currentGold -= item.price;
        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        if (item.itemType == ItemType.Instant)
        {
            bool used = item.Use();
            if (!used)
            {
                RunManager.instance.currentGold += item.price;
                RefreshCoinDisplay();
                return;
            }
        }
        else
        {
            GameEvents.ItemPurchased(item);
        }

        // Secret item lock
        if (secretItem != null && item.itemName == secretItem.itemName)
            hasBoughtSecretItem = true;

        purchased[index] = true;

        if (index < cardUIs.Count)
        {
            cardUIs[index].button.interactable = false;
            cardUIs[index].soldOutOverlay.SetActive(true);
        }

        RefreshCoinDisplay();
        GameEvents.GoldChanged(RunManager.instance.currentGold);
    }

    // ═══════════════════════════════════════════
    // UI REFRESH
    // ═══════════════════════════════════════════

    public void RefreshCoinDisplay()
    {
        if (goldText != null && RunManager.instance != null)
            goldText.text = RunManager.instance.currentGold.ToString();

        // Also update legacy text if bound
        if (coinDisplayText != null && RunManager.instance != null)
            coinDisplayText.text = RunManager.instance.currentGold.ToString();

        RefreshRerollButton();
        RefreshAffordability();
    }

    public void RefreshAffordability()
    {
        if (RunManager.instance == null) return;
        for (int i = 0; i < cardUIs.Count && i < currentItems.Count; i++)
        {
            if (purchased.Count > i && purchased[i]) continue;
            if (cardUIs[i].button == null) continue;

            bool canAfford = currentItems[i] != null && RunManager.instance.currentGold >= currentItems[i].price;

            if (canAfford && currentItems[i] != null && currentItems[i].itemType == ItemType.Consumable)
            {
                if (InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
                    canAfford = false;
            }

            cardUIs[i].button.interactable = canAfford;
        }
    }

    private void RefreshRerollButton()
    {
        if (codeRerollPriceText != null)
            codeRerollPriceText.text = "REROLL  <color=#FFD933>" + currentRerollCost + "</color>";
        if (codeRerollButton != null && RunManager.instance != null)
            codeRerollButton.interactable = RunManager.instance.currentGold >= currentRerollCost;

        // Legacy
        if (rerollPriceText != null)
        {
            rerollPriceText.richText = true;
            rerollPriceText.text = "Reroll  <color=#FFD933>" + currentRerollCost + "</color>";
        }
        if (rerollButton != null && RunManager.instance != null)
            rerollButton.interactable = RunManager.instance.currentGold >= currentRerollCost;
    }

    // ═══════════════════════════════════════════
    // EXTRA SLOT (Perk-driven)
    // ═══════════════════════════════════════════

    public void AddSingleExtraSlot()
    {
        if (itemPool.Count == 0) return;

        List<int> usedIndices = new List<int>();
        foreach (var currentItem in currentItems)
        {
            if (currentItem != null)
            {
                int indexInPool = itemPool.IndexOf(currentItem);
                if (indexInPool != -1) usedIndices.Add(indexInPool);
            }
        }

        if (itemPool.Count <= usedIndices.Count) return;

        int idx;
        int safety = 0;
        do { idx = Random.Range(0, itemPool.Count); if (++safety > 100) break; }
        while (usedIndices.Contains(idx));

        BaseItem newItem = itemPool[idx];
        currentItems.Add(newItem);
        purchased.Add(false);

        // If we have a card UI slot available, populate it
        if (cardUIs.Count > currentItems.Count - 1)
        {
            int newIdx = currentItems.Count - 1;
            PopulateCard(cardUIs[newIdx], newItem);
            cardUIs[newIdx].root.SetActive(true);
            cardUIs[newIdx].soldOutOverlay.SetActive(false);
            cardUIs[newIdx].button.interactable = true;
        }

        RefreshAffordability();
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    private IEnumerator FlashText(TMP_Text t)
    {
        if (t == null) yield break;
        Color orig = t.color;
        t.color = Color.red;
        yield return new WaitForSecondsRealtime(0.3f);
        t.color = orig;
    }

    private GameObject MakeUIObj(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ═══════════════════════════════════════════
    // INTERNAL CARD DATA CLASS
    // ═══════════════════════════════════════════

    private class ShopCardUI
    {
        public GameObject root;
        public Image background;
        public Button button;
        public TMP_Text nameText;
        public Image iconImage;
        public TMP_Text descriptionText;
        public TMP_Text priceText;
        public Image coinImage;
        public GameObject soldOutOverlay;
    }
}
