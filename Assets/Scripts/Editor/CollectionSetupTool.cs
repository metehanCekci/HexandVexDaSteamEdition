using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Koleksiyon UI'ını MainMenu sahnesine otomatik kurar.
/// HexAndVex > Setup Collection UI menüsünden erişilir.
/// Canvas, grid, kartlar, tooltip, filtreler, toast sistemi — hepsini oluşturur.
/// </summary>
public class CollectionSetupTool : EditorWindow
{
    [MenuItem("HexAndVex/Setup Collection UI")]
    public static void OpenWindow()
    {
        var win = GetWindow<CollectionSetupTool>("Collection Setup");
        win.minSize = new Vector2(380, 320);
    }

    private bool createToastSystem = true;
    private bool createDatabase = true;

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Perk Koleksiyon UI Kurulum Aracı", EditorStyles.boldLabel);
        GUILayout.Space(5);
        GUILayout.Label(
            "MainMenu sahnesine koleksiyon canvas'ını, grid kartları,\n" +
            "tooltip paneli, filtre butonları ve toast sistemini kurar.",
            EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        createDatabase = EditorGUILayout.Toggle("Database Asset Oluştur", createDatabase);
        createToastSystem = EditorGUILayout.Toggle("Toast Notification Sistemi", createToastSystem);

        GUILayout.Space(15);

        if (GUILayout.Button("Sahneye Kur", GUILayout.Height(40)))
            SetupCollection();

        GUILayout.Space(5);

        if (GUILayout.Button("Sadece Database Oluştur (Perk Prefab'lardan)", GUILayout.Height(30)))
            CreateDatabaseFromPrefabs();

        GUILayout.Space(5);

        if (GUILayout.Button("Rarity'leri MergedShopManager'dan Güncelle", GUILayout.Height(30)))
            SyncRaritiesFromMergedShop();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Kurulumdan sonra:\n" +
            "1. MainMenu sahnesindeki bir butona CollectionUI.Open() bağla\n" +
            "2. PerkCollectionDatabase asset'ini inspector'dan kontrol et\n" +
            "3. Her perk'in unlock koşulunu ayarla",
            MessageType.Info);
    }

    // ═══════════════════════════════════════════════════════
    //  ANA KURULUM
    // ═══════════════════════════════════════════════════════

    private void SetupCollection()
    {
        // Zaten varsa uyar
        if (Object.FindFirstObjectByType<CollectionUI>() != null)
        {
            if (!EditorUtility.DisplayDialog("Collection UI Zaten Var",
                "Sahnede zaten bir CollectionUI bulundu. Yeniden oluşturmak istiyor musun?",
                "Evet, Yeniden Oluştur", "İptal"))
                return;

            var old = Object.FindFirstObjectByType<CollectionUI>();
            if (old != null) Undo.DestroyObjectImmediate(old.transform.root.gameObject);
        }

        TMP_FontAsset font = UIStyle.LoadFont();
        PerkCollectionDatabase db = createDatabase ? CreateDatabaseFromPrefabs() : FindExistingDatabase();

        // ── 1. Ana Canvas ──────────────────────────────────
        GameObject canvasGO = new GameObject("CollectionCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Collection UI");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── 2. Panel Root (açılıp kapanacak) ───────────────
        GameObject panelRoot = CreatePanel(canvasGO.transform, "PanelRoot", Vector2.zero, new Vector2(1600, 900));
        Image panelBg = panelRoot.GetComponent<Image>();
        panelBg.color = new Color(0f, 0.02f, 0.06f, 0.97f);
        AddRoundedOutline(panelRoot, new Color(0.1f, 0.2f, 0.4f, 0.6f));

        CanvasGroup panelCG = panelRoot.AddComponent<CanvasGroup>();

        // ── 3. Header ──────────────────────────────────────
        GameObject header = CreatePanel(panelRoot.transform, "Header", new Vector2(0, 400), new Vector2(1560, 80));
        header.GetComponent<Image>().color = new Color(0, 0, 0, 0); // Şeffaf
        RectTransform headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0.5f, 1f);
        headerRT.anchorMax = new Vector2(0.5f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = new Vector2(0, -20);

        TMP_Text titleText = CreateText(header.transform, "Title", "KOLEKSIYON", font, 42,
            new Vector2(-200, 0), new Vector2(500, 70));
        titleText.color = new Color(1f, 0.85f, 0.2f);
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Left;
        RectTransform titleRT = titleText.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(0, 0.5f);
        titleRT.pivot = new Vector2(0, 0.5f);
        titleRT.anchoredPosition = new Vector2(20, 0);

        TMP_Text counterText = CreateText(header.transform, "Counter", "0 / 0", font, 28,
            new Vector2(200, 0), new Vector2(300, 50));
        counterText.color = new Color(0.7f, 0.7f, 0.7f);
        counterText.alignment = TextAlignmentOptions.Right;
        RectTransform counterRT = counterText.GetComponent<RectTransform>();
        counterRT.anchorMin = new Vector2(1, 0.5f);
        counterRT.anchorMax = new Vector2(1, 0.5f);
        counterRT.pivot = new Vector2(1, 0.5f);
        counterRT.anchoredPosition = new Vector2(-20, 0);

        // ── 4. Close Button ────────────────────────────────
        GameObject closeBtnGO = CreateButton(panelRoot.transform, "CloseButton", 50, 50, font, "X");
        RectTransform closeBtnRT = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1, 1);
        closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.pivot = new Vector2(1, 1);
        closeBtnRT.anchoredPosition = new Vector2(-10, -10);
        closeBtnGO.GetComponent<Image>().color = new Color(0.6f, 0.1f, 0.1f, 0.8f);
        TMP_Text closeTxt = closeBtnGO.GetComponentInChildren<TMP_Text>();
        if (closeTxt != null) { closeTxt.fontSize = 24; closeTxt.color = Color.white; }

        // ── 5. Filter Bar ──────────────────────────────────
        GameObject filterBar = new GameObject("FilterBar", typeof(RectTransform));
        filterBar.transform.SetParent(panelRoot.transform, false);
        RectTransform filterBarRT = filterBar.GetComponent<RectTransform>();
        filterBarRT.anchorMin = new Vector2(0.5f, 1f);
        filterBarRT.anchorMax = new Vector2(0.5f, 1f);
        filterBarRT.pivot = new Vector2(0.5f, 1f);
        filterBarRT.anchoredPosition = new Vector2(0, -90);
        filterBarRT.sizeDelta = new Vector2(1000, 45);

        HorizontalLayoutGroup filterLayout = filterBar.AddComponent<HorizontalLayoutGroup>();
        filterLayout.spacing = 10;
        filterLayout.childAlignment = TextAnchor.MiddleCenter;
        filterLayout.childForceExpandWidth = false;
        filterLayout.childForceExpandHeight = true;

        string[] filterNames = { "TÜMÜ", "COMMON", "RARE", "EPIC", "LEGENDARY" };
        Color[] filterColors = {
            new Color(0.5f, 0.5f, 0.5f),
            new Color(0.75f, 0.75f, 0.75f),
            new Color(0.2f, 0.5f, 1f),
            new Color(0.6f, 0.2f, 1f),
            new Color(1f, 0.6f, 0f)
        };
        Button[] filterBtns = new Button[5];

        for (int i = 0; i < 5; i++)
        {
            GameObject fbGO = CreateButton(filterBar.transform, $"Filter_{filterNames[i]}", 150, 40, font, filterNames[i]);
            LayoutElement le = fbGO.AddComponent<LayoutElement>();
            le.preferredWidth = 150;
            le.preferredHeight = 40;
            fbGO.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            AddRoundedOutline(fbGO, filterColors[i] * 0.6f);
            TMP_Text ftxt = fbGO.GetComponentInChildren<TMP_Text>();
            if (ftxt != null) { ftxt.fontSize = 16; ftxt.color = filterColors[i]; }
            filterBtns[i] = fbGO.GetComponent<Button>();
        }

        // ── 6. Scroll View + Grid ─────────────────────────
        GameObject scrollViewGO = new GameObject("ScrollView", typeof(RectTransform));
        scrollViewGO.transform.SetParent(panelRoot.transform, false);
        RectTransform scrollRT = scrollViewGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRT.pivot = new Vector2(0.5f, 0.5f);
        scrollRT.sizeDelta = new Vector2(1520, 680);
        scrollRT.anchoredPosition = new Vector2(0, -40);

        ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 30f;

        scrollViewGO.AddComponent<RectMask2D>();

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollViewGO.transform, false);
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRT;

        // Content (Grid)
        GameObject gridContent = new GameObject("GridContent", typeof(RectTransform));
        gridContent.transform.SetParent(viewport.transform, false);
        RectTransform gridRT = gridContent.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 1);
        gridRT.anchorMax = new Vector2(1, 1);
        gridRT.pivot = new Vector2(0.5f, 1);
        gridRT.anchoredPosition = Vector2.zero;
        gridRT.sizeDelta = new Vector2(0, 1200); // ContentSizeFitter ayarlayacak

        GridLayoutGroup grid = gridContent.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(160, 200);
        grid.spacing = new Vector2(15, 15);
        grid.padding = new RectOffset(20, 20, 10, 20);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = gridContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = gridRT;

        // ── 7. Card Prefab (template, deaktif) ─────────────
        GameObject cardPrefab = CreateCardPrefab(panelRoot.transform, font);

        // ── 8. Tooltip Panel ───────────────────────────────
        var tooltipRefs = CreateTooltipPanel(panelRoot.transform, font);

        // ── 9. CollectionUI Component ──────────────────────
        CollectionUI collectionUI = canvasGO.AddComponent<CollectionUI>();
        collectionUI.database = db;
        collectionUI.panelRoot = panelRoot;
        collectionUI.gridContainer = gridContent.transform;
        collectionUI.cardPrefab = cardPrefab;
        collectionUI.panelCanvasGroup = panelCG;

        // Tooltip bağlantıları
        collectionUI.tooltipPanel = tooltipRefs.panel;
        collectionUI.tooltipName = tooltipRefs.nameText;
        collectionUI.tooltipDescription = tooltipRefs.descText;
        collectionUI.tooltipLore = tooltipRefs.loreText;
        collectionUI.tooltipRarity = tooltipRefs.rarityText;
        collectionUI.tooltipUnlockHint = tooltipRefs.unlockHintText;
        collectionUI.tooltipIcon = tooltipRefs.iconImg;
        collectionUI.tooltipProgressBar = tooltipRefs.progressBar;
        collectionUI.tooltipProgressText = tooltipRefs.progressText;
        collectionUI.tooltipBorderGlow = tooltipRefs.borderGlow;

        // Header bağlantıları
        collectionUI.headerTitle = titleText;
        collectionUI.headerCounter = counterText;

        // Filter bağlantıları
        collectionUI.filterAllBtn = filterBtns[0];
        collectionUI.filterCommonBtn = filterBtns[1];
        collectionUI.filterRareBtn = filterBtns[2];
        collectionUI.filterEpicBtn = filterBtns[3];
        collectionUI.filterLegendaryBtn = filterBtns[4];

        // Close button bağlantısı
        Button closeBtn = closeBtnGO.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            closeBtn.onClick,
            new UnityEngine.Events.UnityAction(collectionUI.Close));

        // ── 10. PerkCollectionManager (yoksa oluştur) ──────
        if (Object.FindFirstObjectByType<PerkCollectionManager>() == null)
        {
            GameObject mgrGO = new GameObject("PerkCollectionManager");
            Undo.RegisterCreatedObjectUndo(mgrGO, "Create PerkCollectionManager");
            PerkCollectionManager mgr = mgrGO.AddComponent<PerkCollectionManager>();
            mgr.database = db;
        }

        // ── 11. Toast Sistem (opsiyonel) ───────────────────
        if (createToastSystem)
            SetupToastSystem(font, canvasGO);

        // Panel'i başlangıçta kapat
        panelRoot.SetActive(false);

        Selection.activeGameObject = canvasGO;
        Debug.Log("<color=#FFD700>[COLLECTION SETUP]</color> Koleksiyon UI başarıyla kuruldu!");
        EditorUtility.DisplayDialog("Kurulum Tamamlandı",
            "Koleksiyon UI sahneye eklendi.\n\n" +
            "Sonraki adımlar:\n" +
            "1. MainMenu'deki bir butona CollectionUI.Open() bağla\n" +
            "2. Database asset'ini kontrol et (Assets/Resources/)\n" +
            "3. Her perk için unlock koşullarını ayarla",
            "Tamam");
    }

    // ═══════════════════════════════════════════════════════
    //  CARD PREFAB
    // ═══════════════════════════════════════════════════════

    private GameObject CreateCardPrefab(Transform parent, TMP_FontAsset font)
    {
        // Ana kart container
        GameObject card = new GameObject("CardPrefab", typeof(RectTransform));
        card.transform.SetParent(parent, false);
        RectTransform cardRT = card.GetComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(160, 200);

        // CardBG
        GameObject bg = new GameObject("CardBG", typeof(RectTransform));
        bg.transform.SetParent(card.transform, false);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        AddRoundedOutline(bg, new Color(0.15f, 0.15f, 0.25f, 0.6f));

        // GlowOverlay (rarity glow, kartın arkasında)
        GameObject glow = new GameObject("GlowOverlay", typeof(RectTransform));
        glow.transform.SetParent(card.transform, false);
        RectTransform glowRT = glow.GetComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-4, -4);
        glowRT.offsetMax = new Vector2(4, 4);
        Image glowImg = glow.AddComponent<Image>();
        glowImg.color = new Color(1, 1, 1, 0.05f);
        glowImg.raycastTarget = false;

        // RarityStripe (üst kısımda ince çizgi)
        GameObject stripe = new GameObject("RarityStripe", typeof(RectTransform));
        stripe.transform.SetParent(card.transform, false);
        RectTransform stripeRT = stripe.GetComponent<RectTransform>();
        stripeRT.anchorMin = new Vector2(0, 1);
        stripeRT.anchorMax = new Vector2(1, 1);
        stripeRT.pivot = new Vector2(0.5f, 1);
        stripeRT.anchoredPosition = Vector2.zero;
        stripeRT.sizeDelta = new Vector2(0, 4);
        Image stripeImg = stripe.AddComponent<Image>();
        stripeImg.color = new Color(0.5f, 0.5f, 0.5f);
        stripeImg.raycastTarget = false;

        // Icon
        GameObject icon = new GameObject("Icon", typeof(RectTransform));
        icon.transform.SetParent(card.transform, false);
        RectTransform iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax = new Vector2(0.5f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0, 20);
        iconRT.sizeDelta = new Vector2(80, 80);
        Image iconImg = icon.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // LockIcon (kilit sembolü)
        GameObject lockIcon = new GameObject("LockIcon", typeof(RectTransform));
        lockIcon.transform.SetParent(card.transform, false);
        RectTransform lockRT = lockIcon.GetComponent<RectTransform>();
        lockRT.anchorMin = new Vector2(0.5f, 0.5f);
        lockRT.anchorMax = new Vector2(0.5f, 0.5f);
        lockRT.pivot = new Vector2(0.5f, 0.5f);
        lockRT.anchoredPosition = new Vector2(0, 20);
        lockRT.sizeDelta = new Vector2(36, 36);
        Image lockImg = lockIcon.AddComponent<Image>();
        lockImg.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        lockImg.raycastTarget = false;

        // NameLabel
        GameObject nameLbl = new GameObject("NameLabel", typeof(RectTransform));
        nameLbl.transform.SetParent(card.transform, false);
        RectTransform nameRT = nameLbl.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0);
        nameRT.pivot = new Vector2(0.5f, 0);
        nameRT.anchoredPosition = new Vector2(0, 10);
        nameRT.sizeDelta = new Vector2(-10, 50);
        TMP_Text nameTxt = nameLbl.AddComponent<TextMeshProUGUI>();
        nameTxt.text = "Perk Name";
        nameTxt.fontSize = 13;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = Color.white;
        nameTxt.enableWordWrapping = true;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;
        if (font != null) nameTxt.font = font;
        nameTxt.raycastTarget = false;

        // NewBadge (sağ üst köşe)
        GameObject newBadge = new GameObject("NewBadge", typeof(RectTransform));
        newBadge.transform.SetParent(card.transform, false);
        RectTransform badgeRT = newBadge.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(1, 1);
        badgeRT.anchorMax = new Vector2(1, 1);
        badgeRT.pivot = new Vector2(1, 1);
        badgeRT.anchoredPosition = new Vector2(5, 5);
        badgeRT.sizeDelta = new Vector2(40, 20);
        Image badgeBg = newBadge.AddComponent<Image>();
        badgeBg.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        badgeBg.raycastTarget = false;

        GameObject badgeText = new GameObject("BadgeText", typeof(RectTransform));
        badgeText.transform.SetParent(newBadge.transform, false);
        RectTransform btRT = badgeText.GetComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.offsetMin = Vector2.zero;
        btRT.offsetMax = Vector2.zero;
        TMP_Text btTxt = badgeText.AddComponent<TextMeshProUGUI>();
        btTxt.text = "YENİ!";
        btTxt.fontSize = 10;
        btTxt.alignment = TextAlignmentOptions.Center;
        btTxt.color = Color.white;
        btTxt.fontStyle = FontStyles.Bold;
        if (font != null) btTxt.font = font;
        btTxt.raycastTarget = false;

        newBadge.SetActive(false);

        // Deaktif et (prefab template olarak kullanılacak)
        card.SetActive(false);

        return card;
    }

    // ═══════════════════════════════════════════════════════
    //  TOOLTIP PANEL
    // ═══════════════════════════════════════════════════════

    private struct TooltipRefs
    {
        public GameObject panel;
        public TMP_Text nameText, descText, loreText, rarityText, unlockHintText, progressText;
        public Image iconImg, borderGlow;
        public Slider progressBar;
    }

    private TooltipRefs CreateTooltipPanel(Transform parent, TMP_FontAsset font)
    {
        var refs = new TooltipRefs();

        // Tooltip Panel
        GameObject tooltip = CreatePanel(parent, "TooltipPanel", Vector2.zero, new Vector2(320, 380));
        tooltip.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.97f);
        AddRoundedOutline(tooltip, new Color(0.3f, 0.3f, 0.5f, 0.6f));
        RectTransform tipRT = tooltip.GetComponent<RectTransform>();
        tipRT.pivot = new Vector2(0, 0);
        refs.panel = tooltip;

        // Border Glow
        GameObject borderGlow = new GameObject("BorderGlow", typeof(RectTransform));
        borderGlow.transform.SetParent(tooltip.transform, false);
        RectTransform bglowRT = borderGlow.GetComponent<RectTransform>();
        bglowRT.anchorMin = Vector2.zero;
        bglowRT.anchorMax = Vector2.one;
        bglowRT.offsetMin = new Vector2(-3, -3);
        bglowRT.offsetMax = new Vector2(3, 3);
        Image borderGlowImg = borderGlow.AddComponent<Image>();
        borderGlowImg.color = new Color(1, 1, 1, 0.2f);
        borderGlowImg.raycastTarget = false;
        refs.borderGlow = borderGlowImg;

        // Layout
        VerticalLayoutGroup vlg = tooltip.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.spacing = 8;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        ContentSizeFitter tipCSF = tooltip.AddComponent<ContentSizeFitter>();
        tipCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        tipCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // İkon + İsim (yatay)
        GameObject topRow = new GameObject("TopRow", typeof(RectTransform));
        topRow.transform.SetParent(tooltip.transform, false);
        HorizontalLayoutGroup hlg = topRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        LayoutElement topLE = topRow.AddComponent<LayoutElement>();
        topLE.preferredHeight = 50;

        // Icon
        GameObject tipIcon = new GameObject("TooltipIcon", typeof(RectTransform));
        tipIcon.transform.SetParent(topRow.transform, false);
        Image tipIconImg = tipIcon.AddComponent<Image>();
        tipIconImg.preserveAspect = true;
        LayoutElement iconLE = tipIcon.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 48;
        iconLE.preferredHeight = 48;
        refs.iconImg = tipIconImg;

        // Name + Rarity (dikey)
        GameObject nameCol = new GameObject("NameCol", typeof(RectTransform));
        nameCol.transform.SetParent(topRow.transform, false);
        VerticalLayoutGroup nameVLG = nameCol.AddComponent<VerticalLayoutGroup>();
        nameVLG.spacing = 2;
        nameVLG.childForceExpandWidth = true;
        nameVLG.childForceExpandHeight = false;
        LayoutElement nameColLE = nameCol.AddComponent<LayoutElement>();
        nameColLE.flexibleWidth = 1;
        nameColLE.preferredHeight = 50;

        refs.nameText = CreateLayoutText(nameCol.transform, "TooltipName", "Perk Name", font, 22, Color.white);
        refs.nameText.fontStyle = FontStyles.Bold;
        refs.rarityText = CreateLayoutText(nameCol.transform, "TooltipRarity", "COMMON", font, 12, Color.gray);

        // Description
        refs.descText = CreateLayoutText(tooltip.transform, "TooltipDesc", "Description goes here...", font, 14, new Color(0.8f, 0.8f, 0.8f));
        refs.descText.enableWordWrapping = true;

        // Lore
        refs.loreText = CreateLayoutText(tooltip.transform, "TooltipLore", "", font, 12, new Color(0.5f, 0.5f, 0.6f));
        refs.loreText.fontStyle = FontStyles.Italic;
        refs.loreText.enableWordWrapping = true;

        // Divider
        GameObject divider = new GameObject("Divider", typeof(RectTransform));
        divider.transform.SetParent(tooltip.transform, false);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(1, 1, 1, 0.1f);
        divImg.raycastTarget = false;
        LayoutElement divLE = divider.AddComponent<LayoutElement>();
        divLE.preferredHeight = 1;

        // Unlock Hint
        refs.unlockHintText = CreateLayoutText(tooltip.transform, "TooltipUnlockHint", "", font, 13, new Color(1f, 0.7f, 0.3f));
        refs.unlockHintText.enableWordWrapping = true;

        // Progress Bar
        GameObject progressGO = new GameObject("ProgressBar", typeof(RectTransform));
        progressGO.transform.SetParent(tooltip.transform, false);
        LayoutElement progLE = progressGO.AddComponent<LayoutElement>();
        progLE.preferredHeight = 20;

        Slider slider = progressGO.AddComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0;
        slider.maxValue = 100;

        // Background
        GameObject sliderBg = new GameObject("Background", typeof(RectTransform));
        sliderBg.transform.SetParent(progressGO.transform, false);
        RectTransform slBgRT = sliderBg.GetComponent<RectTransform>();
        slBgRT.anchorMin = Vector2.zero;
        slBgRT.anchorMax = Vector2.one;
        slBgRT.offsetMin = Vector2.zero;
        slBgRT.offsetMax = Vector2.zero;
        Image slBgImg = sliderBg.AddComponent<Image>();
        slBgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(progressGO.transform, false);
        RectTransform faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero;
        faRT.anchorMax = Vector2.one;
        faRT.offsetMin = Vector2.zero;
        faRT.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.7f, 0.2f, 0.9f);

        slider.fillRect = fillRT;
        refs.progressBar = slider;

        // Progress Text
        refs.progressText = CreateLayoutText(tooltip.transform, "ProgressText", "0 / 100", font, 12, new Color(0.6f, 0.6f, 0.6f));
        refs.progressText.alignment = TextAlignmentOptions.Center;

        tooltip.SetActive(false);

        return refs;
    }

    // ═══════════════════════════════════════════════════════
    //  TOAST SYSTEM
    // ═══════════════════════════════════════════════════════

    private void SetupToastSystem(TMP_FontAsset font, GameObject parentCanvas)
    {
        // Toast zaten varsa atla
        if (Object.FindFirstObjectByType<CollectionUnlockToast>() != null) return;

        // Toast Canvas (ayrı, DontDestroyOnLoad olacak)
        GameObject toastCanvasGO = new GameObject("ToastCanvas");
        Undo.RegisterCreatedObjectUndo(toastCanvasGO, "Create Toast Canvas");

        Canvas toastCanvas = toastCanvasGO.AddComponent<Canvas>();
        toastCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        toastCanvas.sortingOrder = 999; // Her şeyin üstünde

        CanvasScaler toastScaler = toastCanvasGO.AddComponent<CanvasScaler>();
        toastScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        toastScaler.referenceResolution = new Vector2(1920, 1080);
        toastScaler.matchWidthOrHeight = 0.5f;

        toastCanvasGO.AddComponent<GraphicRaycaster>();

        // Toast Container (sağ üst köşe, dikey layout)
        GameObject container = new GameObject("ToastContainer", typeof(RectTransform));
        container.transform.SetParent(toastCanvasGO.transform, false);
        RectTransform contRT = container.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(1, 1);
        contRT.anchorMax = new Vector2(1, 1);
        contRT.pivot = new Vector2(1, 1);
        contRT.anchoredPosition = new Vector2(-20, -20);
        contRT.sizeDelta = new Vector2(380, 600);

        VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperRight;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        // Toast Prefab (template)
        GameObject toastPrefab = CreateToastPrefab(toastCanvasGO.transform, font);

        // CollectionUnlockToast component
        CollectionUnlockToast toast = toastCanvasGO.AddComponent<CollectionUnlockToast>();
        toast.toastPrefab = toastPrefab;
    }

    private GameObject CreateToastPrefab(Transform parent, TMP_FontAsset font)
    {
        GameObject toast = new GameObject("ToastPrefab", typeof(RectTransform));
        toast.transform.SetParent(parent, false);
        RectTransform toastRT = toast.GetComponent<RectTransform>();
        toastRT.sizeDelta = new Vector2(360, 90);

        LayoutElement le = toast.AddComponent<LayoutElement>();
        le.preferredHeight = 90;
        le.preferredWidth = 360;

        // Background
        Image bg = toast.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.08f, 0.95f);
        AddRoundedOutline(toast, new Color(1f, 0.85f, 0.2f, 0.5f));

        toast.AddComponent<CanvasGroup>();

        // ToastGlow (arka plan glow)
        GameObject glow = new GameObject("ToastGlow", typeof(RectTransform));
        glow.transform.SetParent(toast.transform, false);
        RectTransform glowRT = glow.GetComponent<RectTransform>();
        glowRT.anchorMin = Vector2.zero;
        glowRT.anchorMax = Vector2.one;
        glowRT.offsetMin = new Vector2(-5, -5);
        glowRT.offsetMax = new Vector2(5, 5);
        Image glowImg = glow.AddComponent<Image>();
        glowImg.color = new Color(1f, 0.85f, 0.2f, 0.15f);
        glowImg.raycastTarget = false;

        // ToastBorder
        GameObject border = new GameObject("ToastBorder", typeof(RectTransform));
        border.transform.SetParent(toast.transform, false);
        RectTransform borderRT = border.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0, 0);
        borderRT.anchorMax = new Vector2(0, 1);
        borderRT.pivot = new Vector2(0, 0.5f);
        borderRT.anchoredPosition = Vector2.zero;
        borderRT.sizeDelta = new Vector2(5, 0);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(1f, 0.85f, 0.2f);
        borderImg.raycastTarget = false;

        // Icon
        GameObject icon = new GameObject("ToastIcon", typeof(RectTransform));
        icon.transform.SetParent(toast.transform, false);
        RectTransform iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.pivot = new Vector2(0, 0.5f);
        iconRT.anchoredPosition = new Vector2(15, 0);
        iconRT.sizeDelta = new Vector2(56, 56);
        Image iconImg = icon.AddComponent<Image>();
        iconImg.preserveAspect = true;

        // Title
        GameObject titleGO = new GameObject("ToastTitle", typeof(RectTransform));
        titleGO.transform.SetParent(toast.transform, false);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0, 1);
        titleRT.anchoredPosition = new Vector2(80, -8);
        titleRT.sizeDelta = new Vector2(-100, 0);
        TMP_Text titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "PERK UNLOCKED!";
        titleTxt.fontSize = 16;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = new Color(1f, 0.85f, 0.2f);
        if (font != null) titleTxt.font = font;
        titleTxt.raycastTarget = false;

        // Subtitle (perk name)
        GameObject subtitleGO = new GameObject("ToastSubtitle", typeof(RectTransform));
        subtitleGO.transform.SetParent(toast.transform, false);
        RectTransform subRT = subtitleGO.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0, 0);
        subRT.anchorMax = new Vector2(1, 0.5f);
        subRT.pivot = new Vector2(0, 0);
        subRT.anchoredPosition = new Vector2(80, 8);
        subRT.sizeDelta = new Vector2(-100, 0);
        TMP_Text subTxt = subtitleGO.AddComponent<TextMeshProUGUI>();
        subTxt.text = "Perk Name";
        subTxt.fontSize = 20;
        subTxt.color = Color.white;
        if (font != null) subTxt.font = font;
        subTxt.raycastTarget = false;

        toast.SetActive(false);
        return toast;
    }

    // ═══════════════════════════════════════════════════════
    //  DATABASE OLUŞTURMA
    // ═══════════════════════════════════════════════════════

    private PerkCollectionDatabase CreateDatabaseFromPrefabs()
    {
        // Mevcut database varsa güncelle
        PerkCollectionDatabase db = FindExistingDatabase();
        bool isNew = db == null;

        if (isNew)
        {
            db = ScriptableObject.CreateInstance<PerkCollectionDatabase>();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(db, "Assets/Resources/PerkCollectionDatabase.asset");
        }

        // Perk prefab'larını tara
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Perks" });
        var existingIds = new HashSet<string>(db.entries.Where(e => e != null).Select(e => e.perkId));

        int addedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            string perkId = prefab.name;
            if (existingIds.Contains(perkId)) continue;

            // Entry asset oluştur
            PerkCollectionData entry = ScriptableObject.CreateInstance<PerkCollectionData>();
            entry.perkPrefab = prefab;
            entry.perkId = perkId;
            entry.loreText = "";

            // İlk 20 perk default açık olsun, geri kalanı koşullu
            if (db.entries.Count < 20)
                entry.unlockCondition = UnlockCondition.Default;
            else
                entry.unlockCondition = UnlockCondition.KillEnemies; // Placeholder, sonra ayarlanacak

            entry.requiredAmount = entry.unlockCondition == UnlockCondition.Default ? 0 : 25;
            entry.unlockHint = entry.unlockCondition == UnlockCondition.Default ? "" : "Koşulu ayarla!";

            string entryDir = "Assets/Resources/CollectionEntries";
            if (!AssetDatabase.IsValidFolder(entryDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder("Assets/Resources", "CollectionEntries");
            }

            string entryPath = $"{entryDir}/{perkId}_CollectionEntry.asset";
            AssetDatabase.CreateAsset(entry, entryPath);

            db.entries.Add(entry);
            existingIds.Add(perkId);
            addedCount++;
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#FFD700>[COLLECTION]</color> Database güncellendi: {addedCount} yeni perk eklendi, toplam {db.entries.Count}");
        return db;
    }

    private PerkCollectionDatabase FindExistingDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:PerkCollectionDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PerkCollectionDatabase>(path);
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════
    //  RARITY SYNC
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Ana Sahne'deki MergedShopManager listelerinden rarity bilgisini okuyup
    /// PerkCollectionData entry'lerine yazar.
    /// </summary>
    private static void SyncRaritiesFromMergedShop()
    {
        MergedShopManager shop = Object.FindFirstObjectByType<MergedShopManager>();

        if (shop == null)
        {
            EditorUtility.DisplayDialog("MergedShopManager Bulunamadı",
                "Sahnede MergedShopManager yok.\n\n" +
                "Ana Sahne'yi (Ana Sahne.unity) aç ve tekrar dene,\n" +
                "veya entry'lerdeki rarity'leri elle ayarla.",
                "Tamam");
            return;
        }

        PerkCollectionDatabase db = AssetDatabase.LoadAssetAtPath<PerkCollectionDatabase>("Assets/Resources/PerkCollectionDatabase.asset");
        if (db == null)
        {
            // Fallback: tip ile ara
            string[] guids = AssetDatabase.FindAssets("PerkCollectionDatabase");
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".asset"))
                {
                    db = AssetDatabase.LoadAssetAtPath<PerkCollectionDatabase>(p);
                    if (db != null) break;
                }
            }
        }

        if (db == null)
        {
            EditorUtility.DisplayDialog("Database Bulunamadı", "PerkCollectionDatabase asset'i bulunamadı.", "Tamam");
            return;
        }

        var legendarySet = new HashSet<GameObject>(shop.legendaryPerks);
        var epicSet = new HashSet<GameObject>(shop.epicPerks);
        var rareSet = new HashSet<GameObject>(shop.rarePerks);

        int updated = 0;
        foreach (var entry in db.entries)
        {
            if (entry == null || entry.perkPrefab == null) continue;

            PerkRarity newRarity;
            if (legendarySet.Contains(entry.perkPrefab)) newRarity = PerkRarity.Legendary;
            else if (epicSet.Contains(entry.perkPrefab)) newRarity = PerkRarity.Epic;
            else if (rareSet.Contains(entry.perkPrefab)) newRarity = PerkRarity.Rare;
            else
            {
                // Secret perkler kendi rarity'lerini taşır
                BasePerk bp = entry.perkPrefab.GetComponent<BasePerk>();
                if (bp != null && bp.rarity == PerkRarity.Secret) newRarity = PerkRarity.Secret;
                else newRarity = PerkRarity.Common;
            }

            if (entry.rarity != newRarity)
            {
                entry.rarity = newRarity;
                EditorUtility.SetDirty(entry);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#FFD700>[COLLECTION]</color> {updated} entry'nin rarity'si güncellendi.");
        EditorUtility.DisplayDialog("Rarity Güncelleme Tamamlandı",
            $"{updated} perk'in rarity'si MergedShopManager'dan güncellendi.", "Tamam");
    }

    // ═══════════════════════════════════════════════════════
    //  UI HELPER METOTLARI
    // ═══════════════════════════════════════════════════════

    private static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>();
        return go;
    }

    private static GameObject CreateButton(Transform parent, string name, float w, float h, TMP_FontAsset font, string label = "")
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        Image img = go.AddComponent<Image>();
        img.color = UIStyle.BgDark;

        Button btn = go.AddComponent<Button>();
        btn.colors = UIStyle.ButtonColors();
        btn.targetGraphic = img;

        if (!string.IsNullOrEmpty(label))
        {
            GameObject textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            TMP_Text txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 16;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            if (font != null) txt.font = font;
            txt.raycastTarget = false;
        }

        return go;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, TMP_FontAsset font,
        int fontSize, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TMP_Text CreateLayoutText(Transform parent, string name, string text, TMP_FontAsset font,
        int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = color;
        if (font != null) tmp.font = font;
        tmp.raycastTarget = false;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8;
        le.flexibleWidth = 1;

        return tmp;
    }

    private static void AddRoundedOutline(GameObject go, Color color)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
    }
}
