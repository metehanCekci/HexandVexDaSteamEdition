using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Map-node Shop Manager — full-screen perk-card-style shop canvas.
/// Opens when player walks to the Dealer NPC in the shop arena.
/// Items display as large cards with all info visible (no hover tooltips).
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

    // ─── Legacy Inspector References (kept so Inspector doesn't break) ───
    [Header("Legacy (no longer used — hidden at runtime)")]
    public Transform shopSlotContainer;
    public GameObject shopSlotPrefab;
    public TMP_Text coinDisplayText;
    public Button rerollButton;
    public TMP_Text rerollPriceText;
    public GameObject continueButton;
    [HideInInspector] public GameObject shopCanvas; // old field

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
    private Image goldCoinIcon;
    private Button codeRerollButton;
    private TMP_Text codeRerollPriceText;
    private Button codeContinueButton;
    private TMP_Text shopTitleText;

    private static bool shopUIBuilt = false;
    private int hoveredCardIndex = -1;

    // Cached coin sprite
    private Sprite cachedCoinSprite;

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

        // Cache coin sprite
        cachedCoinSprite = GetCoinSprite();

        // HIDE legacy scene UI so it doesn't overlap
        HideLegacyUI();

        BuildShopUI();
        CloseShop();
    }

    /// <summary>
    /// Hides the old shop panel, reroll button, coin text, etc. that may
    /// still exist in the scene from the legacy setup.
    /// </summary>
    private void HideLegacyUI()
    {
        if (shopSlotContainer != null && shopSlotContainer.parent != null)
            shopSlotContainer.parent.gameObject.SetActive(false);

        if (rerollButton != null)
            rerollButton.gameObject.SetActive(false);

        if (coinDisplayText != null)
            coinDisplayText.transform.parent?.gameObject.SetActive(false);

        if (rerollPriceText != null)
            rerollPriceText.gameObject.SetActive(false);

        if (continueButton != null)
            continueButton.SetActive(false);
    }

    private Sprite GetCoinSprite()
    {
        if (TurnManager.instance != null && TurnManager.instance.coinSprite != null)
            return TurnManager.instance.coinSprite;
        var vfx = Object.FindFirstObjectByType<CoinDropVFX>();
        if (vfx != null) return vfx.coinSprite;
        return null;
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
        canvas.sortingOrder = 92;
        var scaler = shopCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        shopCanvasGO.AddComponent<GraphicRaycaster>();

        // ─── Full-screen panel ───
        shopPanel = MakeUIObj("ShopPanel", shopCanvasGO.transform);
        StretchFull(shopPanel.GetComponent<RectTransform>());
        Image panelBG = shopPanel.AddComponent<Image>();
        panelBG.color = new Color(0f, 0f, 0f, 1f);
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

        // ─── Gold Display (top-right) — HorizontalLayout for icon + text ───
        GameObject goldRowGO = MakeUIObj("GoldRow", shopPanel.transform);
        RectTransform goldRowRT = goldRowGO.GetComponent<RectTransform>();
        goldRowRT.anchorMin = new Vector2(1f, 1f);
        goldRowRT.anchorMax = new Vector2(1f, 1f);
        goldRowRT.pivot = new Vector2(1f, 1f);
        goldRowRT.anchoredPosition = new Vector2(-30f, -35f);
        goldRowRT.sizeDelta = new Vector2(200f, 50f);

        HorizontalLayoutGroup goldHLG = goldRowGO.AddComponent<HorizontalLayoutGroup>();
        goldHLG.spacing = 8f;
        goldHLG.childAlignment = TextAnchor.MiddleRight;
        goldHLG.childControlWidth = false;
        goldHLG.childControlHeight = false;
        goldHLG.childForceExpandWidth = false;
        goldHLG.childForceExpandHeight = false;

        // Coin Icon
        GameObject goldIconGO = MakeUIObj("CoinIcon", goldRowGO.transform);
        goldIconGO.layer = goldRowGO.layer;
        goldCoinIcon = goldIconGO.AddComponent<Image>();
        goldCoinIcon.preserveAspect = true;
        goldCoinIcon.raycastTarget = false;
        if (cachedCoinSprite != null) goldCoinIcon.sprite = cachedCoinSprite;
        RectTransform goldIconRT = goldIconGO.GetComponent<RectTransform>();
        goldIconRT.sizeDelta = new Vector2(36f, 36f);
        LayoutElement goldIconLE = goldIconGO.AddComponent<LayoutElement>();
        goldIconLE.preferredWidth = 36f;
        goldIconLE.preferredHeight = 36f;

        // Gold Text
        GameObject goldTxtGO = MakeUIObj("GoldText", goldRowGO.transform);
        goldTxtGO.layer = goldRowGO.layer;
        goldText = goldTxtGO.AddComponent<TextMeshProUGUI>();
        goldText.fontSize = 32;
        goldText.alignment = TextAlignmentOptions.Left;
        goldText.color = new Color(1f, 0.85f, 0.2f);
        goldText.fontStyle = FontStyles.Bold;
        goldText.raycastTarget = false;
        RectTransform goldTxtRT = goldTxtGO.GetComponent<RectTransform>();
        goldTxtRT.sizeDelta = new Vector2(120f, 40f);
        LayoutElement goldTxtLE = goldTxtGO.AddComponent<LayoutElement>();
        goldTxtLE.preferredWidth = 120f;
        goldTxtLE.preferredHeight = 40f;

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
            CreateCardUI(i, cardWidth, cardSpacing);

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

    private void CreateCardUI(int index, float cardWidth, float cardSpacing)
    {
        float totalW = shopSlotCount * cardWidth + (shopSlotCount - 1) * cardSpacing;
        float startX = -totalW / 2f + cardWidth / 2f;

        // ─── Card root ───
        GameObject cardGO = MakeUIObj($"ShopCard_{index}", cardContainer.transform);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = new Vector2(startX + index * (cardWidth + cardSpacing), 0f);
        cardRT.sizeDelta = new Vector2(cardWidth, 420f);

        // Grey card background with white outline
        Image cardBG = cardGO.AddComponent<Image>();
        cardBG.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);
        Outline cardOutline = cardGO.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cardOutline.effectDistance = new Vector2(3f, 3f);

        Button buyBtn = cardGO.AddComponent<Button>();
        int idx = index;
        buyBtn.onClick.AddListener(() => TryBuy(idx));
        ColorBlock cb = buyBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.95f, 0.7f);
        cb.pressedColor = new Color(0.8f, 0.75f, 0.5f);
        cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        buyBtn.colors = cb;

        EventTrigger trigger = cardGO.AddComponent<EventTrigger>();
        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => { hoveredCardIndex = idx; });
        trigger.triggers.Add(enterEntry);
        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => { if (hoveredCardIndex == idx) hoveredCardIndex = -1; });
        trigger.triggers.Add(exitEntry);

        var vlg = cardGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 14);
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        // ── [1] Item Name (top, italic like perk cards) ──
        TMP_Text nameText = MakeUIObj("ItemName", cardGO.transform).AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 24;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = new Color(0.6f, 0.85f, 1f);
        nameText.fontStyle = FontStyles.Bold | FontStyles.Italic;
        nameText.raycastTarget = false;
        nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        // ── [2] Big square icon (fills card width like perk card) ──
        Image iconImg = MakeUIObj("Icon", cardGO.transform).AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        var iconLE = iconImg.gameObject.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 240f;
        iconLE.preferredWidth = 240f;

        // ── [3] Description (below icon, like perk cards) ──
        TMP_Text descText = MakeUIObj("Description", cardGO.transform).AddComponent<TextMeshProUGUI>();
        descText.fontSize = 16;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = new Color(0.85f, 0.85f, 0.85f);
        descText.enableWordWrapping = true;
        descText.raycastTarget = false;
        var descLE = descText.gameObject.AddComponent<LayoutElement>();
        descLE.preferredHeight = 60f; descLE.flexibleHeight = 1f;

        // ── Sold Out overlay ──
        GameObject soldOutGO = MakeUIObj("SoldOut", cardGO.transform);
        soldOutGO.AddComponent<LayoutElement>().ignoreLayout = true;
        StretchFull(soldOutGO.GetComponent<RectTransform>());
        Image soldOutBG = soldOutGO.AddComponent<Image>();
        soldOutBG.color = new Color(0f, 0f, 0f, 0.7f);
        soldOutBG.raycastTarget = false;

        TMP_Text soldOutTxt = MakeUIObj("SoldOutText", soldOutGO.transform).AddComponent<TextMeshProUGUI>();
        StretchFull(soldOutTxt.GetComponent<RectTransform>());
        soldOutTxt.text = "SOLD";
        soldOutTxt.fontSize = 36;
        soldOutTxt.alignment = TextAlignmentOptions.Center;
        soldOutTxt.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        soldOutTxt.fontStyle = FontStyles.Bold;
        soldOutTxt.raycastTarget = false;
        soldOutGO.SetActive(false);

        // ── [5] Price row — bottom-right, outside layout ──
        GameObject priceRowGO = MakeUIObj("PriceRow", cardGO.transform);
        priceRowGO.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform priceRowRT = priceRowGO.GetComponent<RectTransform>();
        priceRowRT.anchorMin = new Vector2(1f, 0f);
        priceRowRT.anchorMax = new Vector2(1f, 0f);
        priceRowRT.pivot = new Vector2(1f, 0f);
        priceRowRT.anchoredPosition = new Vector2(-8f, 8f);
        priceRowRT.sizeDelta = new Vector2(100f, 30f);

        HorizontalLayoutGroup priceHLG = priceRowGO.AddComponent<HorizontalLayoutGroup>();
        priceHLG.spacing = 4f;
        priceHLG.childAlignment = TextAnchor.MiddleRight;
        priceHLG.childControlWidth = false;
        priceHLG.childControlHeight = false;
        priceHLG.childForceExpandWidth = false;
        priceHLG.childForceExpandHeight = false;

        Image coinImg = MakeUIObj("CoinIcon", priceRowGO.transform).AddComponent<Image>();
        coinImg.preserveAspect = true;
        coinImg.raycastTarget = false;
        coinImg.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);
        var coinLE = coinImg.gameObject.AddComponent<LayoutElement>();
        coinLE.preferredWidth = 22f; coinLE.preferredHeight = 22f;
        if (cachedCoinSprite != null) coinImg.sprite = cachedCoinSprite;

        TMP_Text priceText = MakeUIObj("PriceText", priceRowGO.transform).AddComponent<TextMeshProUGUI>();
        priceText.fontSize = 20;
        priceText.alignment = TextAlignmentOptions.Left;
        priceText.color = new Color(1f, 0.85f, 0.2f);
        priceText.fontStyle = FontStyles.Bold;
        priceText.raycastTarget = false;
        priceText.GetComponent<RectTransform>().sizeDelta = new Vector2(50f, 26f);
        var pLE = priceText.gameObject.AddComponent<LayoutElement>();
        pLE.preferredWidth = 50f; pLE.preferredHeight = 26f;

        cardUIs.Add(new ShopCardUI
        {
            root = cardGO, background = cardBG, button = buyBtn,
            nameText = nameText, iconImage = iconImg,
            descriptionText = descText, priceText = priceText,
            coinImage = coinImg, soldOutOverlay = soldOutGO
        });
    }

    // ═══════════════════════════════════════════
    // SHOP OPEN / CLOSE
    // ═══════════════════════════════════════════

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

    public bool IsShopOpen => isShopOpen;

    // ═══════════════════════════════════════════
    // ANIMATIONS
    // ═══════════════════════════════════════════

    private IEnumerator ShopOpenAnimation()
    {
        if (shopCanvasGroup != null) shopCanvasGroup.alpha = 0f;

        for (int i = 0; i < cardUIs.Count; i++)
            if (i < currentItems.Count && cardUIs[i].root.activeSelf)
                cardUIs[i].root.transform.localScale = Vector3.zero;

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

        for (int i = 0; i < cardUIs.Count; i++)
        {
            if (i >= currentItems.Count || !cardUIs[i].root.activeSelf) continue;
            yield return StartCoroutine(CardPopIn(cardUIs[i].root.transform));
            if (i < cardUIs.Count - 1) yield return new WaitForSecondsRealtime(0.12f);
        }

        for (int i = 0; i < cardUIs.Count; i++)
            if (i < currentItems.Count && cardUIs[i].root.activeSelf)
                StartCoroutine(CardIdleBounce(cardUIs[i].root.transform, i));
    }

    private IEnumerator ShopCloseAnimation()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (shopCanvasGroup != null)
                shopCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed * elapsed / (duration * duration));
            yield return null;
        }

        CloseShop();

        if (MapManager.instance != null)
            MapManager.instance.OnNodeComplete();
    }

    private IEnumerator CardPopIn(Transform card)
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayCard();
        float dur = 0.25f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float s = Mathf.Lerp(0f, 1.08f, 1f - (1f - t) * (1f - t));
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        dur = 0.1f; elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(1.08f, 1f, Mathf.Clamp01(elapsed / dur));
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
            float s = Mathf.MoveTowards(card.localScale.x, target, Time.unscaledDeltaTime * 3f);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }

    // ═══════════════════════════════════════════
    // LEGACY HOOKS
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

        foreach (var perk in RunManager.instance.activePerks)
            if (perk != null) perk.OnShopReroll();

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

        int secretSlotIndex = -1;
        bool guaranteeSecret = (RunManager.instance != null && RunManager.instance.currentLevel >= 6
                                && !hasBoughtSecretItem && rerollCount == 0);
        if (secretItem != null && !hasBoughtSecretItem && (guaranteeSecret || Random.value < secretItemChance))
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
                int poolIdx, safety = 0;
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
        if (item.icon != null) { card.iconImage.sprite = item.icon; card.iconImage.color = Color.white; card.iconImage.enabled = true; }
        else { card.iconImage.sprite = null; card.iconImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); }
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

        if (item.itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
        { StartCoroutine(FlashText(goldText)); return; }

        if (RunManager.instance.currentGold < item.price)
        { StartCoroutine(FlashText(goldText)); return; }

        RunManager.instance.currentGold -= item.price;
        if (AudioManager.instance != null) AudioManager.instance.PlayPurchase();

        if (item.itemType == ItemType.Instant)
        {
            if (!item.Use()) { RunManager.instance.currentGold += item.price; RefreshCoinDisplay(); return; }
        }
        else
        {
            GameEvents.ItemPurchased(item);
        }

        if (secretItem != null && item.itemName == secretItem.itemName)
            hasBoughtSecretItem = true;

        purchased[index] = true;
        if (index < cardUIs.Count) { cardUIs[index].button.interactable = false; cardUIs[index].soldOutOverlay.SetActive(true); }

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
            if (canAfford && currentItems[i].itemType == ItemType.Consumable && InventoryManager.instance != null && !InventoryManager.instance.HasEmptySlot())
                canAfford = false;
            cardUIs[i].button.interactable = canAfford;
        }
    }

    private void RefreshRerollButton()
    {
        if (codeRerollPriceText != null)
            codeRerollPriceText.text = "REROLL  <color=#FFD933>" + currentRerollCost + "</color>";
        if (codeRerollButton != null && RunManager.instance != null)
            codeRerollButton.interactable = RunManager.instance.currentGold >= currentRerollCost;
    }

    // ═══════════════════════════════════════════
    // EXTRA SLOT (Perk-driven)
    // ═══════════════════════════════════════════

    public void AddSingleExtraSlot()
    {
        if (itemPool.Count == 0) return;
        List<int> usedIndices = new List<int>();
        foreach (var ci in currentItems)
            if (ci != null) { int p = itemPool.IndexOf(ci); if (p != -1) usedIndices.Add(p); }
        if (itemPool.Count <= usedIndices.Count) return;

        int idx, safety = 0;
        do { idx = Random.Range(0, itemPool.Count); if (++safety > 100) break; }
        while (usedIndices.Contains(idx));

        BaseItem newItem = itemPool[idx];
        currentItems.Add(newItem);
        purchased.Add(false);

        if (cardUIs.Count > currentItems.Count - 1)
        {
            int ni = currentItems.Count - 1;
            PopulateCard(cardUIs[ni], newItem);
            cardUIs[ni].root.SetActive(true);
            cardUIs[ni].soldOutOverlay.SetActive(false);
            cardUIs[ni].button.interactable = true;
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
