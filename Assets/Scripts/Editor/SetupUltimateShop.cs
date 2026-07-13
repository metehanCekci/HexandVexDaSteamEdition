using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Tools > Setup Ultimate Shop
/// Perk menüsünün stilini baz alarak ShopCanvas oluşturur.
/// Layout/Outline bileşeni YOK — sahte outline beyaz Image ile yapılır.
/// Shopmanager'a otomatik bağlanır.
/// </summary>
public static class SetupUltimateShop
{
    // ── Sabitler ──
    private const int SLOT_COUNT = 3;
    private const int SORTING_ORDER = 92;
    private const string FONT_PATH = "Assets/alagard SDF.asset";

    // Card
    private const float CARD_W = 280f;
    private const float CARD_H = 400f;
    private const float CARD_SPACING = 40f;

    // Sahte outline çarpanları
    private const float CARD_OUTLINE_SCALE = 1.02f;
    private const float ICON_OUTLINE_SCALE = 1.05f;

    // Renkler — hepsi alpha 255
    private static readonly Color PanelBg     = new Color(0.02f, 0.02f, 0.06f, 1f);
    private static readonly Color CardBg      = new Color(0.06f, 0.06f, 0.14f, 1f);
    private static readonly Color GoldColor   = new Color(1f, 0.82f, 0.2f, 1f);
    private static readonly Color BtnRerollBg = new Color(0.18f, 0.12f, 0.32f, 1f);
    private static readonly Color BtnContBg   = new Color(0.08f, 0.25f, 0.50f, 1f);
    private static readonly Color SoldBg      = new Color(0f, 0f, 0f, 1f);
    private static readonly Color SoldTextCol = new Color(0.9f, 0.25f, 0.2f, 1f);

    // Buton ColorBlock — #00050C, #000841, #0093BC, #00050C, #00050C
    private static readonly Color BtnNormal      = new Color32(0x00, 0x05, 0x0C, 0xFF);
    private static readonly Color BtnHighlighted = new Color32(0x00, 0x08, 0x41, 0xFF);
    private static readonly Color BtnPressed     = new Color32(0x00, 0x93, 0xBC, 0xFF);
    private static readonly Color BtnSelected    = new Color32(0x00, 0x05, 0x0C, 0xFF);
    private static readonly Color BtnDisabled    = new Color32(0x00, 0x05, 0x0C, 0xFF);

    [MenuItem("Tools/Setup Ultimate Shop")]
    public static void Execute()
    {
        // Eski ShopCanvas varsa kaldır
        GameObject existing = GameObject.Find("ShopCanvas");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

        // ══════════════════════════════════════════
        // CANVAS
        // ══════════════════════════════════════════

        GameObject canvasGO = MakeUI("ShopCanvas", null);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGO, "Setup Ultimate Shop");

        // ══════════════════════════════════════════
        // PANEL (fullscreen dark overlay)
        // ══════════════════════════════════════════

        GameObject panelGO = MakeUI("ShopPanel", canvasGO.transform);
        Stretch(panelGO);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = PanelBg;
        panelImg.raycastTarget = true;
        CanvasGroup canvasGroup = panelGO.AddComponent<CanvasGroup>();

        // ══════════════════════════════════════════
        // TITLE — "SHOP" üst ortada (beyaz)
        // ══════════════════════════════════════════

        GameObject titleGO = MakeUI("ShopTitle", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta = new Vector2(500f, 80f);
        TMP_Text titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "SHOP";
        titleTxt.fontSize = 52;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = Color.white;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.raycastTarget = false;
        if (font != null) titleTxt.font = font;

        // ══════════════════════════════════════════
        // GOLD DISPLAY — sağ üst
        // ══════════════════════════════════════════

        GameObject coinIconGO = MakeUI("GoldCoinIcon", panelGO.transform);
        RectTransform coinIconRT = coinIconGO.GetComponent<RectTransform>();
        coinIconRT.anchorMin = new Vector2(1f, 1f);
        coinIconRT.anchorMax = new Vector2(1f, 1f);
        coinIconRT.pivot = new Vector2(1f, 1f);
        coinIconRT.anchoredPosition = new Vector2(-30f, -35f);
        coinIconRT.sizeDelta = new Vector2(40f, 40f);
        Image coinIconImg = coinIconGO.AddComponent<Image>();
        coinIconImg.preserveAspect = true;
        coinIconImg.raycastTarget = false;
        coinIconImg.color = Color.white;

        GameObject goldTxtGO = MakeUI("GoldText", panelGO.transform);
        RectTransform goldTxtRT = goldTxtGO.GetComponent<RectTransform>();
        goldTxtRT.anchorMin = new Vector2(1f, 1f);
        goldTxtRT.anchorMax = new Vector2(1f, 1f);
        goldTxtRT.pivot = new Vector2(1f, 1f);
        goldTxtRT.anchoredPosition = new Vector2(-75f, -35f);
        goldTxtRT.sizeDelta = new Vector2(120f, 40f);
        TMP_Text goldTxt = goldTxtGO.AddComponent<TextMeshProUGUI>();
        goldTxt.text = "0";
        goldTxt.fontSize = 36;
        goldTxt.alignment = TextAlignmentOptions.Right;
        goldTxt.color = GoldColor;
        goldTxt.fontStyle = FontStyles.Bold;
        goldTxt.raycastTarget = false;
        if (font != null) goldTxt.font = font;

        // ══════════════════════════════════════════
        // CARDS
        // ══════════════════════════════════════════

        float totalW = SLOT_COUNT * CARD_W + (SLOT_COUNT - 1) * CARD_SPACING;
        float startX = -totalW / 2f + CARD_W / 2f;

        List<ShopCardSlot> slots = new List<ShopCardSlot>();
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            float xPos = startX + i * (CARD_W + CARD_SPACING);
            slots.Add(CreateCard(panelGO.transform, i, xPos, font));
        }

        // ══════════════════════════════════════════
        // REROLL BUTTON — alt sol
        // ══════════════════════════════════════════

        GameObject rerollGO = MakeUI("RerollBtn", panelGO.transform);
        RectTransform rerollRT = rerollGO.GetComponent<RectTransform>();
        rerollRT.anchorMin = new Vector2(0.5f, 0f);
        rerollRT.anchorMax = new Vector2(0.5f, 0f);
        rerollRT.pivot = new Vector2(1f, 0f);
        rerollRT.anchoredPosition = new Vector2(-20f, 40f);
        rerollRT.sizeDelta = new Vector2(240f, 60f);
        Image rerollImg = rerollGO.AddComponent<Image>();
        rerollImg.color = BtnRerollBg;
        Button rerollBtn = rerollGO.AddComponent<Button>();
        rerollBtn.targetGraphic = rerollImg;
        ApplyButtonColors(rerollBtn);

        GameObject rerollTxtGO = MakeUI("RerollText", rerollGO.transform);
        Stretch(rerollTxtGO);
        TMP_Text rerollTxt = rerollTxtGO.AddComponent<TextMeshProUGUI>();
        rerollTxt.text = "REROLL  <color=#FFD933>10</color>";
        rerollTxt.fontSize = 24;
        rerollTxt.alignment = TextAlignmentOptions.Center;
        rerollTxt.color = Color.white;
        rerollTxt.fontStyle = FontStyles.Bold;
        rerollTxt.richText = true;
        rerollTxt.raycastTarget = false;
        if (font != null) rerollTxt.font = font;

        // ══════════════════════════════════════════
        // CONTINUE BUTTON — alt sağ
        // ══════════════════════════════════════════

        GameObject contGO = MakeUI("ContinueBtn", panelGO.transform);
        RectTransform contRT = contGO.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0.5f, 0f);
        contRT.anchorMax = new Vector2(0.5f, 0f);
        contRT.pivot = new Vector2(0f, 0f);
        contRT.anchoredPosition = new Vector2(20f, 40f);
        contRT.sizeDelta = new Vector2(240f, 60f);
        Image contImg = contGO.AddComponent<Image>();
        contImg.color = BtnContBg;
        Button contBtn = contGO.AddComponent<Button>();
        contBtn.targetGraphic = contImg;
        ApplyButtonColors(contBtn);

        GameObject contTxtGO = MakeUI("ContText", contGO.transform);
        Stretch(contTxtGO);
        TMP_Text contTxt = contTxtGO.AddComponent<TextMeshProUGUI>();
        contTxt.text = "CONTINUE";
        contTxt.fontSize = 26;
        contTxt.alignment = TextAlignmentOptions.Center;
        contTxt.color = Color.white;
        contTxt.fontStyle = FontStyles.Bold;
        contTxt.raycastTarget = false;
        if (font != null) contTxt.font = font;

        // ══════════════════════════════════════════
        // AUTO-WIRE SHOPMANAGER
        // ══════════════════════════════════════════

        Shopmanager sm = Object.FindFirstObjectByType<Shopmanager>();
        if (sm != null)
        {
            Undo.RecordObject(sm, "Wire Ultimate Shop");
            sm.shopCanvasObject = canvasGO;
            sm.shopCanvasGroupRef = canvasGroup;
            sm.shopTitleTextRef = titleTxt;
            sm.goldTextRef = goldTxt;
            sm.rerollButtonRef = rerollBtn;
            sm.rerollPriceTextRef = rerollTxt;
            sm.continueButtonRef = contBtn;
            sm.cardSlots = slots;
            sm.shopSlotCount = SLOT_COUNT;
            EditorUtility.SetDirty(sm);
        }

        EditorUtility.SetDirty(canvasGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;

        Debug.Log("[UltimateShop] Shop oluşturuldu!\n" +
                  "• Outline yok — sahte outline beyaz Image ile\n" +
                  "• Font: alagard\n" +
                  "• Tüm alpha = 255\n" +
                  "• Coin sprite'ını GoldCoinIcon ve kart CoinIcon'larına Inspector'dan ata");
    }

    // ══════════════════════════════════════════
    // CARD CREATION
    // ══════════════════════════════════════════

    private static ShopCardSlot CreateCard(Transform parent, int index, float xPos, TMP_FontAsset font)
    {
        // ── Card Root (sadece pozisyon/boyut tutucu) ──
        GameObject cardGO = MakeUI($"ShopCard_{index}", parent);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = new Vector2(xPos, 10f);
        cardRT.sizeDelta = new Vector2(CARD_W, CARD_H);

        // ── Sahte Outline (beyaz, 1.02x) — ilk child, arkada ──
        GameObject cardOutlineGO = MakeUI("CardOutline", cardGO.transform);
        RectTransform outlineRT = cardOutlineGO.GetComponent<RectTransform>();
        outlineRT.anchorMin = new Vector2(0.5f, 0.5f);
        outlineRT.anchorMax = new Vector2(0.5f, 0.5f);
        outlineRT.pivot = new Vector2(0.5f, 0.5f);
        outlineRT.anchoredPosition = Vector2.zero;
        outlineRT.sizeDelta = new Vector2(CARD_W * CARD_OUTLINE_SCALE, CARD_H * CARD_OUTLINE_SCALE);
        Image outlineImg = cardOutlineGO.AddComponent<Image>();
        outlineImg.color = Color.white;
        outlineImg.raycastTarget = false;

        // ── Card BG (koyu, normal boyut) ──
        GameObject cardBgGO = MakeUI("CardBG", cardGO.transform);
        Stretch(cardBgGO);
        Image cardBG = cardBgGO.AddComponent<Image>();
        cardBG.color = CardBg;
        cardBG.raycastTarget = true;

        // ── Button (root üzerinde, targetGraphic = CardBG) ──
        Button buyBtn = cardGO.AddComponent<Button>();
        buyBtn.targetGraphic = cardBG;
        ApplyButtonColors(buyBtn);

        // ── Item Name — üst kısım (beyaz) ──
        GameObject nameGO = MakeUI("ItemName", cardGO.transform);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0f, -12f);
        nameRT.sizeDelta = new Vector2(0f, 40f);
        TMP_Text nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text = "ITEM NAME";
        nameTxt.fontSize = 26;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.raycastTarget = false;
        if (font != null) nameTxt.font = font;

        // ── Icon Sahte Outline (beyaz, 1.05x) ──
        GameObject iconOutlineGO = MakeUI("IconOutline", cardGO.transform);
        RectTransform iconOlRT = iconOutlineGO.GetComponent<RectTransform>();
        iconOlRT.anchorMin = new Vector2(0.5f, 0.5f);
        iconOlRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconOlRT.pivot = new Vector2(0.5f, 0.5f);
        iconOlRT.anchoredPosition = new Vector2(0f, 30f);
        iconOlRT.sizeDelta = new Vector2(140f * ICON_OUTLINE_SCALE, 140f * ICON_OUTLINE_SCALE);
        Image iconOlImg = iconOutlineGO.AddComponent<Image>();
        iconOlImg.color = Color.white;
        iconOlImg.raycastTarget = false;

        // ── Icon ──
        GameObject iconGO = MakeUI("Icon", cardGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0f, 30f);
        iconRT.sizeDelta = new Vector2(140f, 140f);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // ── Description (beyaz) ──
        GameObject descGO = MakeUI("Description", cardGO.transform);
        RectTransform descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0f, 0f);
        descRT.anchorMax = new Vector2(1f, 0.35f);
        descRT.pivot = new Vector2(0.5f, 0.5f);
        descRT.offsetMin = new Vector2(12f, 50f);
        descRT.offsetMax = new Vector2(-12f, 0f);
        TMP_Text descTxt = descGO.AddComponent<TextMeshProUGUI>();
        descTxt.text = "Item description goes here.";
        descTxt.fontSize = 18;
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.color = Color.white;
        descTxt.enableWordWrapping = true;
        descTxt.raycastTarget = false;
        if (font != null) descTxt.font = font;

        // ── CoinIcon (sağ alt) ──
        GameObject pCoinGO = MakeUI("CoinIcon", cardGO.transform);
        RectTransform pCoinRT = pCoinGO.GetComponent<RectTransform>();
        pCoinRT.anchorMin = new Vector2(1f, 0f);
        pCoinRT.anchorMax = new Vector2(1f, 0f);
        pCoinRT.pivot = new Vector2(1f, 0f);
        pCoinRT.anchoredPosition = new Vector2(-12f, 10f);
        pCoinRT.sizeDelta = new Vector2(26f, 26f);
        Image pCoinImg = pCoinGO.AddComponent<Image>();
        pCoinImg.preserveAspect = true;
        pCoinImg.raycastTarget = false;
        pCoinImg.color = Color.white;

        // ── PriceText (altın renk — tek istisna) ──
        GameObject priceTxtGO = MakeUI("PriceText", cardGO.transform);
        RectTransform priceRT = priceTxtGO.GetComponent<RectTransform>();
        priceRT.anchorMin = new Vector2(1f, 0f);
        priceRT.anchorMax = new Vector2(1f, 0f);
        priceRT.pivot = new Vector2(1f, 0f);
        priceRT.anchoredPosition = new Vector2(-42f, 10f);
        priceRT.sizeDelta = new Vector2(60f, 28f);
        TMP_Text priceTxt = priceTxtGO.AddComponent<TextMeshProUGUI>();
        priceTxt.text = "10";
        priceTxt.fontSize = 22;
        priceTxt.alignment = TextAlignmentOptions.Right;
        priceTxt.color = GoldColor;
        priceTxt.fontStyle = FontStyles.Bold;
        priceTxt.raycastTarget = false;
        if (font != null) priceTxt.font = font;

        // ── Sold Out Overlay ──
        GameObject soldGO = MakeUI("SoldOut", cardGO.transform);
        Stretch(soldGO);
        Image soldImg = soldGO.AddComponent<Image>();
        soldImg.color = SoldBg;
        soldImg.raycastTarget = false;

        GameObject soldTxtGO = MakeUI("SoldOutText", soldGO.transform);
        Stretch(soldTxtGO);
        TMP_Text soldTxt = soldTxtGO.AddComponent<TextMeshProUGUI>();
        soldTxt.text = "SOLD";
        soldTxt.fontSize = 40;
        soldTxt.alignment = TextAlignmentOptions.Center;
        soldTxt.color = SoldTextCol;
        soldTxt.fontStyle = FontStyles.Bold;
        soldTxt.raycastTarget = false;
        if (font != null) soldTxt.font = font;
        soldGO.SetActive(false);

        return new ShopCardSlot
        {
            root = cardGO,
            background = cardBG,
            button = buyBtn,
            nameText = nameTxt,
            iconImage = iconImg,
            descriptionText = descTxt,
            priceText = priceTxt,
            coinImage = pCoinImg,
            soldOutOverlay = soldGO
        };
    }

    // ══════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════

    private static void ApplyButtonColors(Button btn)
    {
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = BtnNormal;
        cb.highlightedColor = BtnHighlighted;
        cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnSelected;
        cb.disabledColor = BtnDisabled;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;
    }

    private static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
