#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class StartingPerkSelectionSetup
{
    [MenuItem("Tools/Hex and Vex/Setup Starting Perk Selection")]
    static void Setup()
    {
        // Clean up existing
        var existingMgr = GameObject.Find("StartingPerkSelection");
        if (existingMgr != null) Object.DestroyImmediate(existingMgr);
        var existingCanvas = GameObject.Find("StartingPerkCanvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        TMP_FontAsset font = UIStyle.LoadFont();

        // ═══════════════════════════════════════════
        // MANAGER
        // ═══════════════════════════════════════════
        GameObject managerGO = new GameObject("StartingPerkSelection");
        var manager = managerGO.AddComponent<StartingPerkSelectionUI>();

        // ═══════════════════════════════════════════
        // CANVAS
        // ═══════════════════════════════════════════
        GameObject canvasGO = new GameObject("StartingPerkCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above everything — first thing player sees
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ═══════════════════════════════════════════
        // PANEL (full-screen dark bg)
        // ═══════════════════════════════════════════
        GameObject panel = MakeUI("Panel", canvasGO.transform);
        Stretch(panel);
        Image panelBG = panel.AddComponent<Image>();
        // NodeMapBG sprite'ı varsa arka plan olarak kullan
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/MapIcons/NodeMapBG.aseprite");
        if (bgSprite != null)
        {
            panelBG.sprite = bgSprite;
            panelBG.type = Image.Type.Simple;
            panelBG.preserveAspect = false;
            panelBG.color = Color.white;
        }
        else
        {
            panelBG.color = new Color(0.02f, 0.02f, 0.04f, 0.98f);
        }
        CanvasGroup cg = panel.AddComponent<CanvasGroup>();
        manager.panel = panel;
        manager.canvasGroup = cg;

        // ═══════════════════════════════════════════
        // TITLE
        // ═══════════════════════════════════════════
        manager.titleText = MakeText("Title", panel.transform, font,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -60), new Vector2(800, 60),
            "CHOOSE YOUR STARTING IMPLANTS", 38, new Color(0.75f, 0.92f, 1f));

        // Subtitle
        MakeText("Subtitle", panel.transform, font,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -110), new Vector2(600, 35),
            "Select 3 mutations to begin your run", 18, new Color(0.5f, 0.5f, 0.55f));

        // ═══════════════════════════════════════════
        // CARD CONTAINER (centered)
        // ═══════════════════════════════════════════
        GameObject cardContainer = MakeUI("CardContainer", panel.transform);
        RectTransform containerRT = cardContainer.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = new Vector2(0, 10);
        containerRT.sizeDelta = new Vector2(1020, 900);

        // 6 cards: top row (3) + bottom row (3) — same size as shop cards (300x420)
        float cardW = 300f;
        float cardH = 420f;
        float spacingX = 30f;
        float rowSpacingY = 30f;

        float totalRowW = cardW * 3 + spacingX * 2;
        float startX = -totalRowW / 2f + cardW / 2f;
        float topY = (cardH + rowSpacingY) / 2f;
        float botY = -(cardH + rowSpacingY) / 2f;

        manager.perkCards.Clear();

        for (int i = 0; i < 6; i++)
        {
            int col = i % 3;
            float x = startX + col * (cardW + spacingX);
            float y = (i < 3) ? topY : botY;

            StartingPerkCard card = BuildCard(cardContainer.transform, $"PerkCard_{i}", font,
                new Vector2(x, y), new Vector2(cardW, cardH));

            manager.perkCards.Add(card);
        }

        // ═══════════════════════════════════════════
        // COUNTER TEXT
        // ═══════════════════════════════════════════
        manager.counterText = MakeText("Counter", panel.transform, font,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 110), new Vector2(200, 40),
            "0 / 3", 24, new Color(0.7f, 0.7f, 0.7f));

        // ═══════════════════════════════════════════
        // CONFIRM BUTTON
        // ═══════════════════════════════════════════
        GameObject confirmGO = MakeUI("ConfirmButton", panel.transform);
        RectTransform confirmRT = confirmGO.GetComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.5f, 0f);
        confirmRT.anchorMax = new Vector2(0.5f, 0f);
        confirmRT.pivot = new Vector2(0.5f, 0f);
        confirmRT.anchoredPosition = new Vector2(0, 45);
        confirmRT.sizeDelta = new Vector2(260, 52);

        Image confirmBG = confirmGO.AddComponent<Image>();
        confirmBG.color = UIStyle.BgDark;

        Button confirmBtn = confirmGO.AddComponent<Button>();
        confirmBtn.colors = UIStyle.ButtonColors();
        confirmBtn.onClick.AddListener(manager.OnConfirmClicked);
        manager.confirmButton = confirmBtn;

        TMP_Text confirmTxt = MakeText("Text", confirmGO.transform, font,
            "CONFIRM", 24, Color.white, true);
        manager.confirmButtonText = confirmTxt;

        UIStyle.AddOutline(confirmGO);

        // ═══════════════════════════════════════════
        // DONE — Select in hierarchy
        // ═══════════════════════════════════════════
        panel.SetActive(false); // Hidden by default

        Undo.RegisterCreatedObjectUndo(managerGO, "Create Starting Perk Selection Manager");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Starting Perk Selection Canvas");
        Selection.activeGameObject = canvasGO;

        Debug.Log("Starting Perk Selection UI created! Run Tools > Hex and Vex > Setup Starting Perk Selection");
    }

    // ═══════════════════════════════════════════
    // BUILD CARD
    // ═══════════════════════════════════════════

    static StartingPerkCard BuildCard(Transform parent, string name, TMP_FontAsset font,
        Vector2 pos, Vector2 size)
    {
        GameObject cardGO = MakeUI(name, parent);
        RectTransform cardRT = cardGO.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.anchoredPosition = pos;
        cardRT.sizeDelta = size;

        // Background — same as shop cards (#00050c)
        Image bg = cardGO.AddComponent<Image>();
        bg.color = new Color(0f, 0.02f, 0.047f, 1f);

        // Selection border (hidden by default)
        GameObject borderGO = MakeUI("SelectionBorder", cardGO.transform);
        Stretch(borderGO);
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(0.2f, 0.9f, 0.4f, 0.15f);
        var borderOutline = borderGO.AddComponent<Outline>();
        borderOutline.effectColor = new Color(0.2f, 0.9f, 0.4f, 0.8f);
        borderOutline.effectDistance = new Vector2(3, -3);
        borderGO.SetActive(false);

        // ─── Icon (upper area, centered) ───
        // Matches shop card: anchoredPosition (150, -132) on 300x420 → normalized ~(0.5, 0.69)
        GameObject iconGO = MakeUI("Icon", cardGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0, 78);
        iconRT.sizeDelta = new Vector2(100, 100);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // ─── Rarity label ───
        TMP_Text rarTxt = MakeText("Rarity", cardGO.transform, font,
            new Vector2(0, 0.42f), new Vector2(1, 0.50f),
            "COMMON", 12, new Color(0.8f, 0.8f, 0.8f));

        // ─── Name (uppercase, rarity colored) ───
        TMP_Text nameTxt = MakeText("Name", cardGO.transform, font,
            new Vector2(0, 0.32f), new Vector2(1, 0.42f),
            "PERK NAME", 18, Color.white);

        // ─── Description ───
        TMP_Text descTxt = MakeText("Description", cardGO.transform, font,
            new Vector2(0, 0.10f), new Vector2(1, 0.32f),
            "Perk description goes here", 13, new Color(0.6f, 0.6f, 0.6f));
        descTxt.enableWordWrapping = true;
        descTxt.overflowMode = TextOverflowModes.Truncate;

        // Button (whole card clickable)
        Button btn = cardGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.95f, 1f);
        colors.pressedColor = new Color(0.7f, 0.9f, 0.7f);
        btn.colors = colors;
        btn.targetGraphic = bg;

        // Wire component
        StartingPerkCard card = cardGO.AddComponent<StartingPerkCard>();
        card.background = bg;
        card.iconImage = iconImg;
        card.selectionBorder = borderImg;
        card.nameText = nameTxt;
        card.rarityText = rarTxt;
        card.descriptionText = descTxt;
        card.button = btn;

        return card;
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5; // UI
        return go;
    }

    static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Anchored text (full control)
    static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size,
        string text, float fontSize, Color color)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        if (font != null) txt.font = font;
        return txt;
    }

    // Anchor-stretch text (for card sections)
    static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, Color color)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(8, 0);
        rt.offsetMax = new Vector2(-8, 0);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Ellipsis;
        if (font != null) txt.font = font;
        return txt;
    }

    // Stretch-fill text
    static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font,
        string text, float fontSize, Color color, bool stretch)
    {
        GameObject go = MakeUI(name, parent);
        if (stretch) Stretch(go);
        TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = color;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        if (font != null) txt.font = font;
        return txt;
    }
}
#endif
