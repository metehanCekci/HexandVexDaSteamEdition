using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sahneye boş bir GameObject ekle, bu script'i at. Oyun başladığında
/// Map sistemi için gereken TÜM UI'ları otomatik oluşturur ve bağlar.
/// Elle prefab/panel oluşturmana gerek yok.
/// </summary>
public class MapBootstrapper : MonoBehaviour
{
    [Header("Opsiyonel: Node İkonları (boş bırakabilirsin)")]
    public Sprite combatIcon;
    public Sprite eliteIcon;
    public Sprite bossIcon;
    public Sprite restIcon;
    public Sprite shopIcon;
    public Sprite sacrificeIcon;
    public Sprite enchantIcon;
    public Sprite treasureIcon;

    [Header("Layer Config (opsiyonel)")]
    public MapLayerData[] layerConfigs;

    void Start()
    {
        // Zaten varsa tekrar oluşturma
        if (MapManager.instance != null) return;

        CreateMapManager();
        CreateMapUI();
        CreateRestNodeUI();
        CreateShopContinueButton();

        Debug.Log("MapBootstrapper: Tüm Map UI bileşenleri oluşturuldu!");
    }

    // ═══════════════════════════════════════════
    // MAP MANAGER
    // ═══════════════════════════════════════════
    private void CreateMapManager()
    {
        GameObject go = new GameObject("MapManager");
        MapManager mm = go.AddComponent<MapManager>();

        if (layerConfigs != null && layerConfigs.Length > 0)
        {
            mm.layerConfigs = layerConfigs;
        }
        else
        {
            // Default config oluştur
            MapLayerData defaultConfig = ScriptableObject.CreateInstance<MapLayerData>();
            defaultConfig.layerName = "Layer 1";
            defaultConfig.totalRows = 8;
            defaultConfig.minNodesPerRow = 2;
            defaultConfig.maxNodesPerRow = 4;
            defaultConfig.restChance = 0.10f;
            defaultConfig.eliteChance = 0.10f;
            defaultConfig.enchantChance = 0.08f;
            mm.layerConfigs = new MapLayerData[] { defaultConfig };
        }
    }

    // ═══════════════════════════════════════════
    // MAP UI (Canvas + Panel + ScrollRect + Nodes)
    // ═══════════════════════════════════════════
    private void CreateMapUI()
    {
        // ─── Canvas ───
        GameObject canvasGO = new GameObject("MapCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90; // Diğer UI'ların üstünde ama fader'ın altında
        var mapScaler = canvasGO.AddComponent<CanvasScaler>();
        mapScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        mapScaler.referenceResolution = new Vector2(1920, 1080);
        mapScaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Map Panel (arka plan) ───
        GameObject panelGO = CreateUIElement("MapPanel", canvasGO.transform);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        StretchFull(panelRT);
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0.05f, 0.05f, 0.12f, 0.95f); // Koyu arka plan
        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();

        // ─── Başlık ───
        GameObject titleGO = CreateUIElement("MapTitle", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -20f);
        titleRT.sizeDelta = new Vector2(400f, 60f);
        TMP_Text titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "MAP";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.9f, 0.85f, 0.7f);

        // ─── ScrollRect ───
        GameObject scrollGO = CreateUIElement("MapScroll", panelGO.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.1f, 0.05f);
        scrollRT.anchorMax = new Vector2(0.9f, 0.9f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        // Scroll için mask
        Image scrollBG = scrollGO.AddComponent<Image>();
        scrollBG.color = new Color(0f, 0f, 0f, 0.01f); // Neredeyse görünmez ama raycast için lazım
        scrollGO.AddComponent<Mask>().showMaskGraphic = false;

        // ─── Node Container (scroll content) ───
        GameObject containerGO = CreateUIElement("NodeContainer", scrollGO.transform);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0f);
        containerRT.anchorMax = new Vector2(0.5f, 0f);
        containerRT.pivot = new Vector2(0.5f, 0f);
        containerRT.sizeDelta = new Vector2(800f, 1200f); // MapUI.BuildMap bunu ayarlayacak
        scroll.content = containerRT;

        // ─── MapNodeUI Prefab (runtime prefab) ───
        GameObject nodePrefab = CreateNodePrefab();

        // ─── Line Prefab ───
        GameObject linePrefab = CreateLinePrefab();

        // ─── MapUI component'ini bağla ───
        MapUI mapUI = panelGO.AddComponent<MapUI>();
        mapUI.mapPanel = panelGO;
        mapUI.canvasGroup = cg;
        mapUI.nodeContainer = containerRT;
        mapUI.mapNodePrefab = nodePrefab;
        mapUI.linePrefab = linePrefab;
        mapUI.rowSpacing = 120f;
        mapUI.columnSpacing = 140f;
        mapUI.nodeJitter = 15f;

        // İkonları ata
        mapUI.combatIcon = combatIcon;
        mapUI.eliteIcon = eliteIcon;
        mapUI.bossIcon = bossIcon;
        mapUI.restIcon = restIcon;
        mapUI.shopIcon = shopIcon;
        mapUI.sacrificeIcon = sacrificeIcon;
        mapUI.enchantIcon = enchantIcon;
        mapUI.treasureIcon = treasureIcon;

        // MapManager'a bağla
        if (MapManager.instance != null)
            MapManager.instance.mapUI = mapUI;

        // Başlangıçta gizle
        panelGO.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // NODE PREFAB (runtime'da oluşturulan template)
    // ═══════════════════════════════════════════
    private GameObject CreateNodePrefab()
    {
        GameObject nodeGO = new GameObject("MapNodePrefab");
        nodeGO.SetActive(false); // Prefab olarak kullanılacak, sahneye konmayacak

        RectTransform rt = nodeGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(70f, 70f);

        // Arka plan (daire şeklinde)
        Image bg = nodeGO.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        // Buton
        Button btn = nodeGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.5f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.3f, 1f);
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = colors;

        // İkon (child)
        GameObject iconGO = CreateUIElement("Icon", nodeGO.transform);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;

        // Label (child, node altında)
        GameObject labelGO = CreateUIElement("Label", nodeGO.transform);
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0f);
        labelRT.anchorMax = new Vector2(0.5f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -5f);
        labelRT.sizeDelta = new Vector2(100f, 20f);
        TMP_Text labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.fontSize = 11;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(0.7f, 0.7f, 0.7f);

        // MapNodeUI component
        MapNodeUI nodeUI = nodeGO.AddComponent<MapNodeUI>();
        nodeUI.iconImage = iconImg;
        nodeUI.backgroundImage = bg;
        nodeUI.button = btn;
        nodeUI.labelText = labelText;

        return nodeGO;
    }

    // ═══════════════════════════════════════════
    // LINE PREFAB
    // ═══════════════════════════════════════════
    private GameObject CreateLinePrefab()
    {
        GameObject lineGO = new GameObject("LinePrefab");
        lineGO.SetActive(false);

        RectTransform rt = lineGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 3f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image img = lineGO.AddComponent<Image>();
        img.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        img.raycastTarget = false;

        return lineGO;
    }

    // ═══════════════════════════════════════════
    // REST NODE UI
    // ═══════════════════════════════════════════
    private void CreateRestNodeUI()
    {
        // ─── Canvas (ayrı, üstte) ───
        GameObject canvasGO = new GameObject("RestCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var restScaler = canvasGO.AddComponent<CanvasScaler>();
        restScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        restScaler.referenceResolution = new Vector2(1920, 1080);
        restScaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Panel ───
        GameObject panelGO = CreateUIElement("RestPanel", canvasGO.transform);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        StretchFull(panelRT);
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0f, 0f, 0f, 1f);

        // ─── Başlık ───
        GameObject titleGO = CreateUIElement("Title", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.7f);
        titleRT.anchorMax = new Vector2(0.5f, 0.7f);
        titleRT.sizeDelta = new Vector2(400f, 60f);
        TMP_Text titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "Campfire";
        titleText.fontSize = 42;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.7f, 0.3f);

        // ─── Rest Butonu ───
        GameObject restBtnGO = CreateButtonUI("RestButton", panelGO.transform,
            new Vector2(-120f, -30f), new Vector2(200f, 80f),
            new Color(0.2f, 0.6f, 0.3f, 1f));
        TMP_Text restBtnText = restBtnGO.GetComponentInChildren<TMP_Text>();

        // ─── Train Butonu ───
        GameObject trainBtnGO = CreateButtonUI("TrainButton", panelGO.transform,
            new Vector2(120f, -30f), new Vector2(200f, 80f),
            new Color(0.3f, 0.3f, 0.7f, 1f));
        TMP_Text trainBtnText = trainBtnGO.GetComponentInChildren<TMP_Text>();

        // ─── Info Text ───
        GameObject infoGO = CreateUIElement("InfoText", panelGO.transform);
        RectTransform infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.5f, 0.25f);
        infoRT.anchorMax = new Vector2(0.5f, 0.25f);
        infoRT.sizeDelta = new Vector2(400f, 40f);
        TMP_Text infoText = infoGO.AddComponent<TextMeshProUGUI>();
        infoText.text = "";
        infoText.fontSize = 24;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = Color.white;

        // ─── RestNodeUI component ───
        RestNodeUI restUI = panelGO.AddComponent<RestNodeUI>();
        restUI.restPanel = panelGO;
        restUI.restButton = restBtnGO.GetComponent<Button>();
        restUI.trainButton = trainBtnGO.GetComponent<Button>();
        restUI.titleText = titleText;
        restUI.restButtonText = restBtnText;
        restUI.trainButtonText = trainBtnText;
        restUI.infoText = infoText;

        // Başlangıçta gizle
        panelGO.SetActive(false);
    }

    // ═══════════════════════════════════════════
    // SHOP CONTINUE BUTONU
    // ═══════════════════════════════════════════
    private void CreateShopContinueButton()
    {
        if (Shopmanager.instance == null) return;

        // Shop panel'inin parent'ını bul
        Transform shopParent = null;
        if (Shopmanager.instance.shopSlotContainer != null)
            shopParent = Shopmanager.instance.shopSlotContainer.parent;
        if (shopParent == null) return;

        // Continue butonu oluştur
        GameObject btnGO = CreateButtonUI("ContinueButton", shopParent,
            new Vector2(0f, -80f), new Vector2(180f, 50f),
            new Color(0.2f, 0.5f, 0.8f, 1f));
        TMP_Text btnText = btnGO.GetComponentInChildren<TMP_Text>();
        if (btnText != null) btnText.text = "Continue";

        btnGO.SetActive(false); // Başlangıçta gizli

        Shopmanager.instance.continueButton = btnGO;
    }

    // ═══════════════════════════════════════════
    // YARDIMCI METODLAR
    // ═══════════════════════════════════════════
    private GameObject CreateUIElement(string name, Transform parent)
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

    private GameObject CreateButtonUI(string name, Transform parent, Vector2 position, Vector2 size, Color bgColor)
    {
        GameObject btnGO = CreateUIElement(name, parent);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = position;
        btnRT.sizeDelta = size;

        Image btnBG = btnGO.AddComponent<Image>();
        btnBG.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        // Buton text'i
        GameObject textGO = CreateUIElement("Text", btnGO.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        StretchFull(textRT);
        TMP_Text text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = name;
        text.fontSize = 20;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return btnGO;
    }
}
