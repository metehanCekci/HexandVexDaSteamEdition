using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Tools > Setup Sell Box  —  Sahneye SellBox Canvas'ini ekler.
/// Balatro tarzi basili-tut-surukleme satis kutusu.
/// </summary>
public class SellBoxSetup : Editor
{
    static readonly Color CyanBright = new Color(0f, 0.576f, 0.737f, 1f);    // #0093BC
    static readonly Color CyanDim    = new Color(0f, 0.576f, 0.737f, 0.5f);

    [MenuItem("Tools/Setup Sell Box")]
    static void Create()
    {
        // Eski varsa sil
        var old = GameObject.Find("SellBoxCanvas");
        if (old != null) Undo.DestroyObjectImmediate(old);

        TMP_FontAsset font = UIStyle.LoadFont();

        // ═══════ CANVAS ═══════
        var canvasGO = new GameObject("SellBoxCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight   = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ═══════ OUTER GLOW (buyuk yayilan parlama) ═══════
        var outerGlowGO = Child(canvasGO, "OuterGlow");
        var outerGlowRT  = outerGlowGO.GetComponent<RectTransform>();
        outerGlowRT.anchorMin = new Vector2(0.5f, 0f);
        outerGlowRT.anchorMax = new Vector2(0.5f, 0f);
        outerGlowRT.pivot     = new Vector2(0.5f, 0f);
        outerGlowRT.anchoredPosition = new Vector2(0f, 28f);
        outerGlowRT.sizeDelta = new Vector2(250f, 130f);

        var outerGlowImg = outerGlowGO.AddComponent<Image>();
        outerGlowImg.color = new Color(0f, 0.576f, 0.737f, 0.10f);
        outerGlowImg.raycastTarget = false;

        // ═══════ SELL BOX PANEL ═══════
        var panelGO = Child(canvasGO, "SellBoxPanel");
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 0f);
        panelRT.pivot     = new Vector2(0.5f, 0f);
        panelRT.anchoredPosition = new Vector2(0f, 35f);
        panelRT.sizeDelta = new Vector2(220f, 110f);

        // Panel arkaplan — koyu #00050C
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0.02f, 0.047f, 0.97f);
        panelImg.raycastTarget = true;

        // Cyan border (4 yonlu — 2 component ile)
        var ol1 = panelGO.AddComponent<Outline>();
        ol1.effectColor    = CyanBright;
        ol1.effectDistance  = new Vector2(2.5f, -2.5f);
        var ol2 = panelGO.AddComponent<Shadow>();
        ol2.effectColor    = CyanBright;
        ol2.effectDistance  = new Vector2(-2.5f, 2.5f);

        // CanvasGroup — editor'de gorunur, runtime'da script alpha kontrol eder
        var cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;

        // ═══════ TOP BORDER LINE (ince parlak ust cizgi) ═══════
        var topLine = Child(panelGO, "TopLine");
        var topLineRT = topLine.GetComponent<RectTransform>();
        topLineRT.anchorMin = new Vector2(0f, 1f);
        topLineRT.anchorMax = new Vector2(1f, 1f);
        topLineRT.pivot     = new Vector2(0.5f, 1f);
        topLineRT.anchoredPosition = Vector2.zero;
        topLineRT.sizeDelta = new Vector2(0f, 3f);
        var topLineImg = topLine.AddComponent<Image>();
        topLineImg.color = CyanBright;
        topLineImg.raycastTarget = false;

        // ═══════ BOTTOM BORDER LINE ═══════
        var botLine = Child(panelGO, "BottomLine");
        var botLineRT = botLine.GetComponent<RectTransform>();
        botLineRT.anchorMin = new Vector2(0f, 0f);
        botLineRT.anchorMax = new Vector2(1f, 0f);
        botLineRT.pivot     = new Vector2(0.5f, 0f);
        botLineRT.anchoredPosition = Vector2.zero;
        botLineRT.sizeDelta = new Vector2(0f, 3f);
        var botLineImg = botLine.AddComponent<Image>();
        botLineImg.color = CyanBright;
        botLineImg.raycastTarget = false;

        // ═══════ LEFT BORDER LINE ═══════
        var leftLine = Child(panelGO, "LeftLine");
        var leftLineRT = leftLine.GetComponent<RectTransform>();
        leftLineRT.anchorMin = new Vector2(0f, 0f);
        leftLineRT.anchorMax = new Vector2(0f, 1f);
        leftLineRT.pivot     = new Vector2(0f, 0.5f);
        leftLineRT.anchoredPosition = Vector2.zero;
        leftLineRT.sizeDelta = new Vector2(3f, 0f);
        var leftLineImg = leftLine.AddComponent<Image>();
        leftLineImg.color = CyanBright;
        leftLineImg.raycastTarget = false;

        // ═══════ RIGHT BORDER LINE ═══════
        var rightLine = Child(panelGO, "RightLine");
        var rightLineRT = rightLine.GetComponent<RectTransform>();
        rightLineRT.anchorMin = new Vector2(1f, 0f);
        rightLineRT.anchorMax = new Vector2(1f, 1f);
        rightLineRT.pivot     = new Vector2(1f, 0.5f);
        rightLineRT.anchoredPosition = Vector2.zero;
        rightLineRT.sizeDelta = new Vector2(3f, 0f);
        var rightLineImg = rightLine.AddComponent<Image>();
        rightLineImg.color = CyanBright;
        rightLineImg.raycastTarget = false;

        // ═══════ "SELL" TITLE ═══════
        var titleGO = Child(panelGO, "SellTitle");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.52f);
        titleRT.anchorMax = new Vector2(1f, 0.95f);
        titleRT.offsetMin = new Vector2(10f, 0f);
        titleRT.offsetMax = new Vector2(-10f, 0f);

        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "SELL";
        titleTMP.font      = font;
        titleTMP.fontSize  = 32;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color     = CyanBright;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.raycastTarget = false;

        // ═══════ SEPARATOR (orta cizgi) ═══════
        var sepGO = Child(panelGO, "Separator");
        var sepRT = sepGO.GetComponent<RectTransform>();
        sepRT.anchorMin = new Vector2(0.08f, 0.48f);
        sepRT.anchorMax = new Vector2(0.92f, 0.48f);
        sepRT.pivot     = new Vector2(0.5f, 0.5f);
        sepRT.anchoredPosition = Vector2.zero;
        sepRT.sizeDelta = new Vector2(0f, 1.5f);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color = CyanDim;
        sepImg.raycastTarget = false;

        // ═══════ PRICE ROW (coin icon + fiyat texti) ═══════
        var priceRowGO = Child(panelGO, "PriceRow");
        var priceRowRT = priceRowGO.GetComponent<RectTransform>();
        priceRowRT.anchorMin = new Vector2(0f, 0.05f);
        priceRowRT.anchorMax = new Vector2(1f, 0.46f);
        priceRowRT.offsetMin = Vector2.zero;
        priceRowRT.offsetMax = Vector2.zero;

        var hlg = priceRowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.spacing              = 8f;
        hlg.childControlWidth    = false;
        hlg.childControlHeight   = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // Coin — gold rengi kare (sprite yoksa)
        var coinGO = Child(priceRowGO, "CoinIcon");
        coinGO.GetComponent<RectTransform>().sizeDelta = new Vector2(26f, 26f);
        var coinImg = coinGO.AddComponent<Image>();
        coinImg.color = UIStyle.TextGold;
        coinImg.raycastTarget = false;

        // Fiyat text
        var priceGO = Child(priceRowGO, "PriceText");
        priceGO.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 32f);

        var priceTMP = priceGO.AddComponent<TextMeshProUGUI>();
        priceTMP.text      = "+10";
        priceTMP.font      = font;
        priceTMP.fontSize  = 26;
        priceTMP.alignment = TextAlignmentOptions.MidlineLeft;
        priceTMP.color     = UIStyle.TextGold;
        priceTMP.fontStyle = FontStyles.Bold;
        priceTMP.raycastTarget = false;

        // ═══════ DROP ZONE ═══════
        var dropGO = Child(panelGO, "DropZone");
        var dropRT = dropGO.GetComponent<RectTransform>();
        dropRT.anchorMin = Vector2.zero;
        dropRT.anchorMax = Vector2.one;
        dropRT.offsetMin = new Vector2(-30f, -30f);
        dropRT.offsetMax = new Vector2(30f, 30f);
        var dropImg = dropGO.AddComponent<Image>();
        dropImg.color = new Color(0, 0, 0, 0);
        dropImg.raycastTarget = true;

        // DropZone tag component'i ekle
        dropGO.AddComponent<SellBoxDropZone>();

        // ═══════ CONTROLLER COMPONENT ═══════
        var controller = canvasGO.AddComponent<SellBoxController>();
        controller.sellBoxPanel = panelGO;
        controller.panelCanvasGroup = cg;
        controller.panelRect = panelRT;
        controller.priceText = priceTMP;
        controller.sellTitleText = titleTMP;

        // ═══════ DONE ═══════
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create SellBox Canvas");
        Selection.activeGameObject = panelGO;

        Debug.Log("<color=cyan>[SellBoxSetup]</color> SellBoxCanvas olusturuldu! " +
                  "SellBoxController ve SellBoxDropZone otomatik eklendi.");
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }
}
