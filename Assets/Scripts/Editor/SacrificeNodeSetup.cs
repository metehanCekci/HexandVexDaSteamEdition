#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class SacrificeNodeSetup
{
    [MenuItem("Tools/Hex and Vex/Setup Sacrifice Node")]
    static void Setup()
    {
        // Destroy existing
        var existing = GameObject.Find("SacrificeNodeManager");
        if (existing != null) Object.DestroyImmediate(existing);
        var existingCanvas = GameObject.Find("SacrificeCanvas");
        if (existingCanvas != null) Object.DestroyImmediate(existingCanvas);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/alagard SDF.asset");

        // ─── Manager GO ───
        GameObject managerGO = new GameObject("SacrificeNodeManager");
        var manager = managerGO.AddComponent<SacrificeNodeManager>();

        // ─── Canvas ───
        GameObject canvasGO = new GameObject("SacrificeCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Panel ───
        GameObject panel = MakeUI("SacrificePanel", canvasGO.transform);
        Stretch(panel);
        Image panelBG = panel.AddComponent<Image>();
        panelBG.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
        CanvasGroup cg = panel.AddComponent<CanvasGroup>();
        manager.panel = panel;
        manager.canvasGroup = cg;

        // ─── Title ───
        manager.titleText = MakeTextObj("Title", panel.transform, font,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-50, -15), new Vector2(500, 55),
            "SACRIFICE MACHINE", 36, new Color(0.8f, 0.2f, 0.6f));

        // ─── Machine Body ───
        GameObject machineGO = MakeUI("MachineBody", panel.transform);
        SetAnchored(machineGO, new Vector2(-50, 20), new Vector2(420, 520));
        Image machineBG = machineGO.AddComponent<Image>();
        machineBG.color = new Color(0.12f, 0.12f, 0.15f, 1f);

        // Header
        GameObject headerGO = MakeUI("Header", machineGO.transform);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0, 35);
        headerGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 1f);

        // ─── Tube ───
        GameObject tubeOuterGO = MakeUI("TubeOuter", machineGO.transform);
        RectTransform toRT = tubeOuterGO.GetComponent<RectTransform>();
        toRT.anchorMin = new Vector2(0.06f, 0.15f);
        toRT.anchorMax = new Vector2(0.94f, 0.93f);
        toRT.offsetMin = Vector2.zero;
        toRT.offsetMax = Vector2.zero;
        tubeOuterGO.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f, 0.45f);

        GameObject tubeInnerGO = MakeUI("TubeInner", tubeOuterGO.transform);
        Stretch(tubeInnerGO);
        tubeInnerGO.GetComponent<RectTransform>().offsetMin = new Vector2(3, 3);
        tubeInnerGO.GetComponent<RectTransform>().offsetMax = new Vector2(-3, -3);
        Image tubeImg = tubeInnerGO.AddComponent<Image>();
        tubeImg.color = new Color(0.12f, 0.35f, 0.18f, 0.1f);
        manager.tubeArea = tubeInnerGO.GetComponent<RectTransform>();
        manager.tubeImage = tubeImg;

        var dropZone = tubeInnerGO.AddComponent<SacrificeTubeDropZone>();
        dropZone.highlightImage = tubeImg;

        // Perk Grid
        GameObject gridGO = MakeUI("PerkGrid", tubeInnerGO.transform);
        Stretch(gridGO);
        gridGO.GetComponent<RectTransform>().offsetMin = new Vector2(8, 8);
        gridGO.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -8);
        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(60, 65);
        grid.spacing = new Vector2(8, 6);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        manager.perkGridParent = gridGO.transform;

        // ─── Acid Pool ───
        GameObject acidGO = MakeUI("AcidPool", machineGO.transform);
        RectTransform acidRT = acidGO.GetComponent<RectTransform>();
        acidRT.anchorMin = new Vector2(0.06f, 0.04f);
        acidRT.anchorMax = new Vector2(0.94f, 0.15f);
        acidRT.offsetMin = Vector2.zero;
        acidRT.offsetMax = Vector2.zero;
        manager.acidPoolImage = acidGO.AddComponent<Image>();
        manager.acidPoolImage.color = new Color(0.08f, 0.5f, 0.08f, 0.55f);

        // ─── Lever ───
        BuildLever(panel.transform, manager, font);

        // ─── Reward Slots ───
        BuildRewardSlots(panel.transform, manager, font);

        // ─── Status Text ───
        manager.statusText = MakeTextObj("StatusText", panel.transform, font,
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(-50, 55), new Vector2(550, 35),
            "DRAG PERKS INTO THE TUBE", 18, new Color(0.8f, 0.8f, 0.8f));

        // ─── Leave Button ───
        BuildLeaveButton(panel.transform, manager, font);

        // Start hidden
        panel.SetActive(false);

        Selection.activeGameObject = managerGO;
        Debug.Log("[SacrificeNodeSetup] Sacrifice Node UI created successfully!");
    }

    // ═══════════════════════════════════════════
    // LEVER
    // ═══════════════════════════════════════════

    static void BuildLever(Transform parent, SacrificeNodeManager mgr, TMP_FontAsset font)
    {
        GameObject leverGO = MakeUI("Lever", parent);
        SetAnchored(leverGO, new Vector2(-330, 20), new Vector2(80, 280));
        Image baseBG = leverGO.AddComponent<Image>();
        baseBG.color = new Color(0.09f, 0.09f, 0.11f, 0.85f);

        // Shaft
        GameObject shaftGO = MakeUI("Shaft", leverGO.transform);
        RectTransform shaftRT = shaftGO.GetComponent<RectTransform>();
        shaftRT.anchorMin = new Vector2(0.5f, 0.15f);
        shaftRT.anchorMax = new Vector2(0.5f, 0.8f);
        shaftRT.sizeDelta = new Vector2(8, 0);
        shaftGO.AddComponent<Image>().color = new Color(0.45f, 0.45f, 0.5f);

        // Handle
        GameObject handleGO = MakeUI("Handle", leverGO.transform);
        mgr.leverHandle = handleGO.GetComponent<RectTransform>();
        mgr.leverHandle.anchorMin = new Vector2(0.5f, 0.8f);
        mgr.leverHandle.anchorMax = new Vector2(0.5f, 0.8f);
        mgr.leverHandle.sizeDelta = new Vector2(36, 36);
        mgr.leverHandleImage = handleGO.AddComponent<Image>();
        mgr.leverHandleImage.color = Color.white;

        mgr.leverButton = leverGO.AddComponent<Button>();
        mgr.leverButton.targetGraphic = baseBG;

        mgr.leverText = MakeTextObj("PullText", leverGO.transform, font,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1),
            new Vector2(0, -5), new Vector2(0, 28),
            "PULL", 15, Color.white);
    }

    // ═══════════════════════════════════════════
    // REWARD SLOTS
    // ═══════════════════════════════════════════

    static void BuildRewardSlots(Transform parent, SacrificeNodeManager mgr, TMP_FontAsset font)
    {
        GameObject rowGO = MakeUI("RewardRow", parent);
        SetAnchored(rowGO, new Vector2(-50, -290), new Vector2(520, 180));
        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        mgr.rareSlot = CreateSlot("RareSlot", rowGO.transform, PerkRarity.Rare, 2, font);
        mgr.epicSlot = CreateSlot("EpicSlot", rowGO.transform, PerkRarity.Epic, 4, font);
        mgr.legendarySlot = CreateSlot("LegendarySlot", rowGO.transform, PerkRarity.Legendary, 6, font);
    }

    static SacrificeRewardSlot CreateSlot(string name, Transform parent, PerkRarity rarity, int cost, TMP_FontAsset font)
    {
        Color rc = SacrificeRewardSlot.GetRarityColor(rarity);

        GameObject slotGO = MakeUI(name, parent);
        Image bg = slotGO.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);

        // Glow
        GameObject glowGO = MakeUI("Glow", slotGO.transform);
        Stretch(glowGO);
        glowGO.GetComponent<RectTransform>().offsetMin = new Vector2(-3, -3);
        glowGO.GetComponent<RectTransform>().offsetMax = new Vector2(3, 3);
        glowGO.transform.SetAsFirstSibling();
        Image glowImg = glowGO.AddComponent<Image>();
        glowImg.color = new Color(0.25f, 0.25f, 0.25f, 0.25f);
        glowImg.raycastTarget = false;

        // Rarity label (top)
        TMP_Text rarTxt = MakeAnchText("Rarity", slotGO.transform, font,
            new Vector2(0, 0.88f), new Vector2(1, 1),
            rarity.ToString().ToUpper(), 12, rc);

        // Icon (center)
        GameObject iconGO = MakeUI("Icon", slotGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.55f);
        iconRT.anchorMax = new Vector2(0.5f, 0.55f);
        iconRT.sizeDelta = new Vector2(48, 48);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Name
        TMP_Text nameTxt = MakeAnchText("Name", slotGO.transform, font,
            new Vector2(0, 0.30f), new Vector2(1, 0.44f),
            "???", 12, Color.white);

        // Description
        TMP_Text descTxt = MakeAnchText("Desc", slotGO.transform, font,
            new Vector2(0, 0.12f), new Vector2(1, 0.30f),
            "", 9, new Color(0.65f, 0.65f, 0.65f));
        descTxt.enableWordWrapping = true;
        descTxt.overflowMode = TextOverflowModes.Truncate;

        // Cost (bottom)
        TMP_Text costTxt = MakeAnchText("Cost", slotGO.transform, font,
            new Vector2(0, 0f), new Vector2(1, 0.12f),
            $"{cost} PERKS", 10, new Color(0.6f, 0.6f, 0.6f));

        SacrificeRewardSlot slot = slotGO.AddComponent<SacrificeRewardSlot>();
        slot.background = bg;
        slot.iconImage = iconImg;
        slot.glowBorder = glowImg;
        slot.nameText = nameTxt;
        slot.descText = descTxt;
        slot.costText = costTxt;
        slot.rarityLabel = rarTxt;
        return slot;
    }

    // ═══════════════════════════════════════════
    // LEAVE BUTTON
    // ═══════════════════════════════════════════

    static void BuildLeaveButton(Transform parent, SacrificeNodeManager mgr, TMP_FontAsset font)
    {
        GameObject btnGO = MakeUI("LeaveButton", parent);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0);
        btnRT.anchorMax = new Vector2(0.5f, 0);
        btnRT.pivot = new Vector2(0.5f, 0);
        btnRT.anchoredPosition = new Vector2(-50, 15);
        btnRT.sizeDelta = new Vector2(170, 42);
        Image btnBG = btnGO.AddComponent<Image>();
        btnBG.color = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        mgr.leaveButton = btnGO.AddComponent<Button>();
        MakeTextObj("Text", btnGO.transform, font, "LEAVE", 20, Color.white, true);
    }

    // ═══════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════

    static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = 5;
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

    static void SetAnchored(GameObject go, Vector2 pos, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // Full-anchor text
    static TMP_Text MakeTextObj(string name, Transform parent, TMP_FontAsset font,
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

    // Stretch-fill text
    static TMP_Text MakeTextObj(string name, Transform parent, TMP_FontAsset font,
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

    // Anchor-stretched text for reward slot sections
    static TMP_Text MakeAnchText(string name, Transform parent, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax,
        string text, float fontSize, Color color)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(4, 0);
        rt.offsetMax = new Vector2(-4, 0);
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
}
#endif
