using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class MapManager : MonoBehaviour
{
    public static MapManager instance;

    [Header("Layer Config")]
    public MapLayerData[] layerConfigs; // Inspector'dan ata — şimdilik tek layer yeterli

    [Header("Map UI")]
    public MapUI mapUI;

    [Header("Parallax Background")]
    public Texture mapBackgroundTexture; // Inspector'dan kendi arkaplan dokunuzu atayın

    [Header("Node Icons")]
    public Sprite combatIcon;
    public Sprite eliteIcon;
    public Sprite shopIcon;
    public Sprite perkIcon;
    public Sprite restIcon;
    public Sprite eventIcon;
    public Sprite bossIcon;

    [Header("Rest UI")]
    public GameObject restCanvasPrefab; // Inspector'dan RestCanvas prefab'ını sürükle

    [Header("UI Vignette")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.55f;
    [Range(0.1f, 1f)] public float vignetteSmoothness = 0.7f;

    [Header("Runtime State")]
    public MapData currentMap;

    private bool isTransitioning;

    private bool isDuplicate = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            isDuplicate = true;
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        if (isDuplicate) return; // Duplikat instance ise UI oluşturma
        // Eğer layerConfigs boşsa default oluştur
        if (layerConfigs == null || layerConfigs.Length == 0)
        {
            MapLayerData defaultConfig = ScriptableObject.CreateInstance<MapLayerData>();
            defaultConfig.layerName = "Layer 1";
            defaultConfig.totalRows = 8;
            defaultConfig.minNodesPerRow = 2;
            defaultConfig.maxNodesPerRow = 3;
            defaultConfig.threeNodeChance = 0.15f;
            defaultConfig.shopChance = 0.12f;
            defaultConfig.perkChance = 0.15f;
            defaultConfig.restChance = 0.10f;
            defaultConfig.eliteChance = 0.10f;
            defaultConfig.enchantChance = 0.08f;
            layerConfigs = new MapLayerData[] { defaultConfig };
        }

        // Map UI yoksa otomatik oluştur
        if (mapUI == null)
        {
            BuildUIFromCode();
        }

        // Perk side panel yoksa otomatik oluştur
        if (PerkInventoryUI.instance == null)
        {
            PerkInventoryUI.CreateFromCode();
        }

        // Rest UI yoksa prefab'dan yükle
        if (RestNodeUI.instance == null)
        {
            LoadRestUIFromPrefab();
        }

        // Enchant UI yoksa otomatik oluştur
        if (EnchantNodeUI.instance == null)
        {
            GameObject enchantGO = new GameObject("EnchantNodeUI");
            enchantGO.AddComponent<EnchantNodeUI>();
            DontDestroyOnLoad(enchantGO);
        }

        // MagicTileManager yoksa otomatik oluştur
        if (MagicTileManager.instance == null)
        {
            GameObject mtGO = new GameObject("MagicTileManager");
            mtGO.AddComponent<MagicTileManager>();
        }

        // Auto-spawn Inventory & Hotbar if not present
        EnsureInventoryAndHotbar();

        // UI Vignette — tüm overlay ekranlarının üstünde vignette efekti
        if (UIVignette.instance == null)
        {
            GameObject vignetteGO = new GameObject("UIVignette");
            var uiVignette = vignetteGO.AddComponent<UIVignette>();
            uiVignette.intensity = vignetteIntensity;
            uiVignette.smoothness = vignetteSmoothness;
        }
    }

    // ═══════════════════════════════════════════
    // OTOMATİK UI OLUŞTURMA
    // ═══════════════════════════════════════════
    private static bool uiBuilt = false;

    private void BuildUIFromCode()
    {
        // Zaten oluşturulmuşsa tekrar oluşturma
        if (uiBuilt) return;
        uiBuilt = true;

        // Canvas
        GameObject canvasGO = new GameObject("MapCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel (saydam — arkaplan parallax'tan gelecek)
        GameObject panelGO = MakeUIObj("MapPanel", canvasGO.transform);
        StretchFull(panelGO.GetComponent<RectTransform>());
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0f, 0f, 0f, 0f);
        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();

        // ─── ScrollRect tabanlı scroll sistemi ───
        // Viewport (scroll alanı) — tam ekran
        GameObject scrollGO = MakeUIObj("Scroll", panelGO.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // Raycast almak için
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        // ─── Parallax arkaplan (inspector'dan sprite atanacak) ───
        GameObject parallaxGO = MakeUIObj("ParallaxBG", scrollGO.transform);
        RectTransform parallaxRT = parallaxGO.GetComponent<RectTransform>();
        StretchFull(parallaxRT);
        RawImage parallaxImg = parallaxGO.AddComponent<RawImage>();
        parallaxImg.raycastTarget = false;
        if (mapBackgroundTexture != null)
        {
            parallaxImg.texture = mapBackgroundTexture;
            parallaxImg.color = new Color(0.07f, 0.07f, 0.07f, 1f);
            parallaxImg.uvRect = new Rect(0f, 0f, 16f, 9f); // 16:9 oran, 2x tile
        }
        else
        {
            // Texture atanmamışsa tamamen gizle
            parallaxImg.color = new Color(0f, 0f, 0f, 0f);
        }

        // NodeContainer (scroll content) — alttan başlar, yukarı doğru büyür
        GameObject containerGO = MakeUIObj("NodeContainer", scrollGO.transform);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        // pivot=bottom: y=0 container'ın altı, pozitif y yukarı
        containerRT.anchorMin = new Vector2(0.5f, 0f);
        containerRT.anchorMax = new Vector2(0.5f, 0f);
        containerRT.pivot = new Vector2(0.5f, 0f);
        containerRT.anchoredPosition = Vector2.zero;
        containerRT.sizeDelta = new Vector2(1000f, 2000f);

        // ScrollRect — Unity'nin kendi scroll sistemi
        ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.content = containerRT;
        scrollRect.viewport = scrollRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 60f;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.1f;

        // MapDragScroll — sol tık sürüklemeyi engelle, sağ tık/orta tuş ile pan
        scrollGO.AddComponent<MapDragScroll>();

        // Parallax component
        MapParallaxBG parallax = scrollGO.AddComponent<MapParallaxBG>();
        parallax.scrollRect = scrollRect;
        parallax.bgImage = parallaxImg;
        parallax.parallaxFactor = 0.3f;

        // Node prefab
        GameObject nodePrefab = BuildNodePrefab();

        // Line prefab — anchor bottom-center (container pivot=bottom)
        GameObject linePrefab = new GameObject("LinePrefab");
        linePrefab.SetActive(false);
        var lineRT = linePrefab.AddComponent<RectTransform>();
        lineRT.sizeDelta = new Vector2(100f, 3f);
        lineRT.anchorMin = new Vector2(0.5f, 0f);
        lineRT.anchorMax = new Vector2(0.5f, 0f);
        Image lineImg = linePrefab.AddComponent<Image>();
        lineImg.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        lineImg.raycastTarget = false;

        // MapUI component
        MapUI ui = panelGO.AddComponent<MapUI>();
        ui.mapPanel = canvasGO; // Canvas'ın kendisini kapat/aç — böylece hiçbir şey ekranda kalmaz
        ui.canvasGroup = cg;
        ui.nodeContainer = containerRT;
        ui.mapNodePrefab = nodePrefab;
        ui.linePrefab = linePrefab;
        ui.rowSpacing = 170f;
        ui.columnSpacing = 190f;
        ui.nodeJitter = 15f;

        // Node icon'larını ata
        ui.combatIcon = combatIcon;
        ui.eliteIcon = eliteIcon;
        ui.shopIcon = shopIcon;
        ui.perkIcon = perkIcon;
        ui.restIcon = restIcon;
        ui.eventIcon = eventIcon;
        ui.bossIcon = bossIcon;

        mapUI = ui;
        canvasGO.SetActive(false); // Başlangıçta tüm canvas'ı kapat

        Debug.Log("MapManager: Map UI otomatik oluşturuldu.");
    }

    private GameObject BuildNodePrefab()
    {
        GameObject go = new GameObject("NodePrefab");
        go.SetActive(false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80f, 80f);
        // Container pivot=bottom: anchor bottom-center
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        Button btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;

        // Outline — icon'dan sadece 2px büyük ince beyaz çerçeve
        GameObject outlineGO = MakeUIObj("Outline", go.transform);
        var outlineRT = outlineGO.GetComponent<RectTransform>();
        outlineRT.anchorMin = new Vector2(0.15f, 0.15f);
        outlineRT.anchorMax = new Vector2(0.85f, 0.85f);
        outlineRT.offsetMin = new Vector2(-2f, -2f);
        outlineRT.offsetMax = new Vector2(2f, 2f);
        Image outlineImg = outlineGO.AddComponent<Image>();
        outlineImg.color = new Color(0f, 0f, 0f, 0f); // Başlangıçta tamamen gizli
        outlineImg.raycastTarget = false;
        outlineGO.transform.SetAsFirstSibling(); // BG'nin arkasında

        // Icon
        GameObject iconGO = MakeUIObj("Icon", go.transform);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.15f, 0.15f);
        iconRT.anchorMax = new Vector2(0.85f, 0.85f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;

        // Label
        GameObject labelGO = MakeUIObj("Label", go.transform);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.5f, 0f);
        labelRT.anchorMax = new Vector2(0.5f, 0f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -5f);
        labelRT.sizeDelta = new Vector2(130f, 28f);
        var labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
        labelTxt.fontSize = 14;
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.color = new Color(0.7f, 0.7f, 0.7f);

        MapNodeUI nodeUI = go.AddComponent<MapNodeUI>();
        nodeUI.iconImage = iconImg;
        nodeUI.backgroundImage = bg;
        nodeUI.button = btn;
        nodeUI.labelText = labelTxt;
        nodeUI.outlineImage = outlineImg;

        return go;
    }

    private static bool restUIBuilt = false;

    private void LoadRestUIFromPrefab()
    {
        if (restUIBuilt) return;
        restUIBuilt = true;

        if (restCanvasPrefab == null)
        {
            Debug.LogError("[MapManager] restCanvasPrefab atanmamış! Inspector'dan RestCanvas prefab'ını sürükle.");
            return;
        }

        GameObject canvasGO = Instantiate(restCanvasPrefab);
        canvasGO.name = "RestCanvas";

        // RestPanel inactive olduğu için Awake çalışmaz — instance'ı elle bul
        RestNodeUI restUI = canvasGO.GetComponentInChildren<RestNodeUI>(true);
        if (restUI != null)
        {
            RestNodeUI.instance = restUI;
        }
        else
        {
            Debug.LogError("[MapManager] RestCanvas prefab'ında RestNodeUI component'i bulunamadı!");
        }
    }

    // ─── Inventory & Hotbar auto-spawn ───
    private void EnsureInventoryAndHotbar()
    {
        if (InventoryManager.instance == null)
        {
            GameObject invGO = new GameObject("InventoryManager");
            invGO.AddComponent<InventoryManager>();
        }

        if (HotbarUI.instance == null)
        {
            GameObject hotbarGO = new GameObject("HotbarUI");
            hotbarGO.AddComponent<HotbarUI>();
        }
    }

    // ─── UI yardımcıları ───
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

    private GameObject MakeButton(string label, Transform parent, Vector2 pos, Color color)
    {
        GameObject go = MakeUIObj(label + "Btn", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(200f, 80f);
        Image img = go.AddComponent<Image>();
        img.color = color;
        go.AddComponent<Button>();

        GameObject txtGO = MakeUIObj("Text", go.transform);
        StretchFull(txtGO.GetComponent<RectTransform>());
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 20;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        return go;
    }

    // ─── Yeni oyun başlarken çağır ───
    public void StartNewRun()
    {
        StartCoroutine(StartNewRunDelayed());
    }

    /// <summary>
    /// Sahne yeniden yüklendiğinde yok olan UI referanslarını kontrol eder,
    /// gerekirse static flag'leri sıfırlayıp UI'ı yeniden oluşturur.
    /// </summary>
    private void RebuildUIIfDestroyed()
    {
        // MapUI veya Canvas'ı sahne geçişinde yok olmuş olabilir
        if (mapUI == null || mapUI.mapPanel == null)
        {
            uiBuilt = false;
            mapUI = null;
            BuildUIFromCode();
        }

        // RestNodeUI yok olmuş olabilir
        if (RestNodeUI.instance == null || RestNodeUI.instance.gameObject == null)
        {
            restUIBuilt = false;
            RestNodeUI.instance = null;
            LoadRestUIFromPrefab();
        }

        // PerkInventoryUI yok olmuş olabilir
        if (PerkInventoryUI.instance == null)
        {
            PerkInventoryUI.CreateFromCode();
        }

        // EnchantNodeUI yok olmuş olabilir
        if (EnchantNodeUI.instance == null)
        {
            GameObject enchantGO = new GameObject("EnchantNodeUI");
            enchantGO.AddComponent<EnchantNodeUI>();
            DontDestroyOnLoad(enchantGO);
        }

        // MagicTileManager yok olmuş olabilir
        if (MagicTileManager.instance == null)
        {
            GameObject mtGO = new GameObject("MagicTileManager");
            mtGO.AddComponent<MagicTileManager>();
        }

        // Inventory & Hotbar yok olmuş olabilir
        EnsureInventoryAndHotbar();
    }

    private IEnumerator StartNewRunDelayed()
    {
        // Sahne geçişinde yok olan UI'ları yeniden oluştur
        RebuildUIIfDestroyed();

        // UI'ın oluşması için 1 frame bekle
        yield return null;

        Debug.Log($"[MAP-DEBUG] StartNewRunDelayed: mapUI={mapUI}, mapUI is null={mapUI == null}");

        // Reset inventory for new run
        if (InventoryManager.instance != null)
            InventoryManager.instance.ResetForNewRun();

        GenerateNewMap(0);

        Debug.Log($"[MAP-DEBUG] After GenerateNewMap: currentMap nodes={currentMap?.nodes?.Count}, mapUI={mapUI}");

        // Map'i aç
        ShowMapInstant();

        // Canvas açıldıktan sonra 1 frame bekle — viewport boyutu ancak şimdi doğru
        yield return null;

        // Şimdi doğru pozisyona scroll et
        if (mapUI != null) mapUI.CenterOnCurrentNode(currentMap);

        // Kısa bekleme (render edilsin)
        yield return new WaitForSecondsRealtime(0.1f);

        // Ekranı aydınlat
        yield return StartCoroutine(FadeFromBlack());
    }

    // ─── Yeni layer haritası üret ───
    public void GenerateNewMap(int layerIndex)
    {
        MapLayerData config = GetLayerConfig(layerIndex);
        currentMap = MapGenerator.Generate(config, layerIndex);

        // İlk node'u (row 0) otomatik olarak "current" yap ama visited yapma
        // Oyuncu ilk combat'a girmeden map'i görecek
        if (currentMap.nodes.Count > 0)
        {
            // Start node'u bul (row 0)
            foreach (var node in currentMap.nodes)
            {
                if (node.row == 0)
                {
                    currentMap.currentNodeId = -1; // Henüz hiçbir node'a girilmedi
                    break;
                }
            }
        }

        // Debug: tüm node'ları logla
        foreach (var n in currentMap.nodes)
        {
            Debug.Log($"[MAP] Node id={n.id} row={n.row} col={n.column} type={n.nodeType} children=[{string.Join(",", n.childIds)}]");
        }

        if (mapUI != null) mapUI.BuildMap(currentMap);
    }

    // ─── Oyuncu bir node seçtiğinde ───
    public void SelectNode(int nodeId)
    {
        if (isTransitioning) return;
        if (currentMap == null) return;

        MapNode node = currentMap.GetNode(nodeId);
        if (node == null) return;

        // İlk hamle: row 0 node'larından birine girebilir
        if (currentMap.currentNodeId == -1)
        {
            if (node.row != 0) return;
        }
        else
        {
            // Sadece current node'un child'larına gidilebilir
            MapNode current = currentMap.GetNode(currentMap.currentNodeId);
            if (current == null || !current.childIds.Contains(nodeId)) return;
        }

        node.visited = true;
        currentMap.currentNodeId = nodeId;

        // Hemen transition'a geç — üst node'ların outline'ı gözükmesin
        isTransitioning = true;

        // Node tipine göre dispatch
        ExecuteNode(node);
    }

    // ─── Node tipine göre aksiyon başlat ───
    private void ExecuteNode(MapNode node)
    {
        isTransitioning = true;

        Debug.Log($"[MAP] ExecuteNode: id={node.id} row={node.row} type={node.nodeType}");

        if (RunManager.instance != null)
            RunManager.instance.currentNodeType = node.nodeType;

        StartCoroutine(ExecuteNodeSequence(node));
    }

    private IEnumerator ExecuteNodeSequence(MapNode node)
    {
        bool isCombatNode = node.nodeType == MapNodeType.Combat
                         || node.nodeType == MapNodeType.EliteCombat
                         || node.nodeType == MapNodeType.Boss;

        if (isCombatNode)
        {
            // Combat/Event: klasik fade in/out
            yield return StartCoroutine(FadeToBlack());
            HideMap();

            // Savaşta perk inventory gizle
            if (PerkInventoryUI.instance != null)
                PerkInventoryUI.instance.Hide();
        }
        else
        {
            // Non-combat (Shop, Perk, Rest): node'ları smooth küçültüp kaybet
            yield return StartCoroutine(SmoothDismissMapNodes());
            HideGameplayElements();
        }

        // İçeriği yükle + hotbar visibility
        bool showHotbar = false;

        switch (node.nodeType)
        {
            case MapNodeType.Combat:
            case MapNodeType.EliteCombat:
                RunManager.instance.currentLevel++;
                LevelGenerator.instance.GenerateNextLevel();
                showHotbar = true;
                break;

            case MapNodeType.Enchant:
                if (EnchantNodeUI.instance != null)
                    EnchantNodeUI.instance.Show();
                break;

            case MapNodeType.Shop:
            case MapNodeType.PerkSelection:
                Debug.Log($"[MapManager] Shop/PerkSelection node. MergedShopManager.instance={MergedShopManager.instance != null}");
                if (MergedShopManager.instance != null)
                    MergedShopManager.instance.OpenAsMapNode();
                else
                    Debug.LogError("[MapManager] MergedShopManager.instance is NULL! Check if MergedShopManager exists in scene and is active.");
                showHotbar = true;
                break;

            case MapNodeType.Rest:
                if (RestNodeUI.instance != null)
                    RestNodeUI.instance.Show();
                NotifyDiceHoarder();
                break;

            case MapNodeType.Sacrifice:
                if (SacrificeNodeManager.instance != null)
                    SacrificeNodeManager.instance.Show();
                break;

            case MapNodeType.Boss:
                PlayerPrefs.SetInt("kills_before_boss", 0); // Boss'a ulaşıldı, sayaç sıfırla
                RunManager.instance.currentLevel++;
                LevelGenerator.instance.GenerateBossArena();
                showHotbar = true;
                break;
        }

        if (HotbarUI.instance != null)
            HotbarUI.instance.SetVisible(showHotbar);

        if (isCombatNode)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            yield return StartCoroutine(FadeFromBlack());
        }

        isTransitioning = false;
    }

    /// <summary>
    /// Map node'larını smooth saydamlaştırarak yok eder.
    /// Arkaplan (parallax) yerinde kalır.
    /// </summary>
    private IEnumerator SmoothDismissMapNodes()
    {
        if (mapUI == null || mapUI.nodeContainer == null) yield break;

        RectTransform containerRT = mapUI.nodeContainer;
        CanvasGroup cg = containerRT.GetComponent<CanvasGroup>();
        if (cg == null) cg = containerRT.gameObject.AddComponent<CanvasGroup>();

        float startAlpha = cg.alpha;
        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        cg.alpha = 0f;
        containerRT.gameObject.SetActive(false);
        cg.alpha = 1f; // resetle (ShowMapNodes'da geri açılacak)
    }

    // ─── Kendi fade helper'larımız (ScreenFader'ın faderGroup'unu kullanır) ───
    private float mapFadeDuration = 0.5f;

    private IEnumerator FadeToBlack()
    {
        CanvasGroup fader = ScreenFader.instance != null ? ScreenFader.instance.faderGroup : null;
        if (fader == null) yield break;

        fader.blocksRaycasts = true;
        float elapsed = 0f;
        float startAlpha = fader.alpha;

        while (elapsed < mapFadeDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            fader.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / mapFadeDuration);
            yield return null;
        }
        fader.alpha = 1f;
    }

    private IEnumerator FadeFromBlack()
    {
        // ScreenFader'ın faderGroup'unu bul — yoksa InitializeFader ile tekrar dene
        CanvasGroup fader = ScreenFader.instance != null ? ScreenFader.instance.faderGroup : null;
        if (fader == null && ScreenFader.instance != null)
        {
            // faderGroup henüz atanmamış olabilir — bir kere daha dene
            yield return null;
            fader = ScreenFader.instance.faderGroup;
        }

        if (fader == null)
        {
            Debug.LogWarning("[MAP] FadeFromBlack: faderGroup bulunamadı! Ekran siyah kalabilir.");
            yield break;
        }

        // ScreenFader'ın kendi coroutine'leri çakışmasın
        if (ScreenFader.instance != null)
            ScreenFader.instance.StopAllCoroutines();

        float elapsed = 0f;
        fader.alpha = 1f; // Başlangıçta kesin siyah

        while (elapsed < mapFadeDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            fader.alpha = Mathf.Lerp(1f, 0f, elapsed / mapFadeDuration);
            yield return null;
        }
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
    }

    // ─── Combat/Shop/Perk/Rest bittikten sonra haritaya dön ───
    public void OnNodeComplete()
    {
        Debug.Log($"[MAP] OnNodeComplete called. currentNodeId={currentMap?.currentNodeId}");
        isTransitioning = false;

        // Boss node ise layer'ı ilerlet
        MapNode current = currentMap?.GetNode(currentMap.currentNodeId);
        if (current != null && current.nodeType == MapNodeType.Boss)
        {
            Debug.Log("[MAP] Current node is Boss → OnBossDefeated");
            OnBossDefeated();
            return;
        }

        Debug.Log("[MAP] Starting ReturnToMapWithFade coroutine");
        // ScreenFader ile: karart → map göster → aydınlat
        // Ama sadece ScreenFader'ın meşgul olmadığı durumlarda
        StartCoroutine(ReturnToMapWithFade());
    }

    private IEnumerator ReturnToMapWithFade()
    {
        // Non-combat node'lardan dönüş: arkaplan zaten açık, sadece node'ları smooth geri aç
        MapNodeType lastType = MapNodeType.Combat;
        if (RunManager.instance != null)
            lastType = RunManager.instance.currentNodeType;

        bool wasNonCombat = lastType == MapNodeType.Shop
                         || lastType == MapNodeType.Rest
                         || lastType == MapNodeType.Enchant;

        if (wasNonCombat)
        {
            // Map arkaplanı zaten açık — node'ları smooth geri getir
            if (mapUI != null) mapUI.RefreshNodeStates(currentMap);
            ShowMapNodes();
            yield return null;
            if (mapUI != null) mapUI.CenterOnCurrentNode(currentMap);
            yield return StartCoroutine(SmoothRevealMapNodes());
        }
        else if (ScreenFader.instance != null)
        {
            // Combat/Event dönüşü: klasik fade
            yield return StartCoroutine(FadeToBlack());

            if (TurnManager.instance != null && TurnManager.instance.player != null)
                TurnManager.instance.player.gameObject.SetActive(true);

            if (mapUI != null) mapUI.RefreshNodeStates(currentMap);
            ShowMapInstant();

            // Map'e dönünce perk inventory'yi geri aç
            if (PerkInventoryUI.instance != null)
                PerkInventoryUI.instance.Show();

            yield return null;
            if (mapUI != null) mapUI.CenterOnCurrentNode(currentMap);

            yield return new WaitForSecondsRealtime(0.2f);
            yield return StartCoroutine(FadeFromBlack());
        }
        else
        {
            if (TurnManager.instance != null && TurnManager.instance.player != null)
                TurnManager.instance.player.gameObject.SetActive(true);
            ShowMapInstant();
        }
    }

    /// <summary>
    /// Map node'larını smooth saydamdan opağa getirerek gösterir.
    /// </summary>
    private IEnumerator SmoothRevealMapNodes()
    {
        if (mapUI == null || mapUI.nodeContainer == null) yield break;

        RectTransform containerRT = mapUI.nodeContainer;
        CanvasGroup cg = containerRT.GetComponent<CanvasGroup>();
        if (cg == null) cg = containerRT.gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        containerRT.gameObject.SetActive(true);

        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            cg.alpha = t;
            yield return null;
        }

        cg.alpha = 1f;
    }

    // ─── Boss yenildiğinde yeni layer'a geç ───
    public void OnBossDefeated()
    {
        isTransitioning = false;

        if (RunManager.instance != null)
        {
            RunManager.instance.currentLayerIndex++;
            Debug.Log($"[MAP] OnBossDefeated — new layerIndex={RunManager.instance.currentLayerIndex}, currentLevel={RunManager.instance.currentLevel}");
        }

        StartCoroutine(BossDefeatedSequence());
    }

    private IEnumerator BossDefeatedSequence()
    {
        Debug.Log("[MAP] BossDefeatedSequence started");

        if (ScreenFader.instance != null)
        {
            yield return StartCoroutine(FadeToBlack());

            // Ekran karardıktan sonra tile'ları temizle
            if (LevelGenerator.instance != null)
            {
                LevelGenerator.instance.groundMap?.ClearAllTiles();
                if (LevelGenerator.instance.columnMap != null)
                    LevelGenerator.instance.columnMap.ClearAllTiles();
            }

            int newLayerIndex = RunManager.instance != null ? RunManager.instance.currentLayerIndex : 0;
            Debug.Log($"[MAP] BossDefeatedSequence — GenerateNewMap(layerIndex={newLayerIndex})");
            GenerateNewMap(newLayerIndex);
            ShowMapInstant();

            yield return null;
            if (mapUI != null) mapUI.CenterOnCurrentNode(currentMap);

            yield return new WaitForSecondsRealtime(0.2f);
            yield return StartCoroutine(FadeFromBlack());
            Debug.Log("[MAP] BossDefeatedSequence completed — new map visible");
        }
        else
        {
            int newLayerIndex = RunManager.instance != null ? RunManager.instance.currentLayerIndex : 0;
            GenerateNewMap(newLayerIndex);
            ShowMapInstant();
            Debug.Log("[MAP] BossDefeatedSequence completed (no fader)");
        }
    }

    // ─── Map UI göster/gizle (instant — fade ScreenFader'da) ───
    public void ShowMap()
    {
        ShowMapInstant();
    }

    private void ShowMapInstant()
    {
        Debug.Log($"[MAP-DEBUG] ShowMapInstant: mapUI={mapUI}, mapUI null={mapUI == null}, currentMap={currentMap}, nodes={currentMap?.nodes?.Count}");
        // Hide hotbar when map is shown
        if (HotbarUI.instance != null)
            HotbarUI.instance.SetVisible(false);

        // Map'te savaş UI'larını gizle
        SetCombatUIVisible(false);

        // Player'ı gizle — ScreenSpaceCamera modunda kamera arkasından görünür
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            TurnManager.instance.player.gameObject.SetActive(false);

        // Düşmanları da gizle
        if (TurnManager.instance != null)
        {
            foreach (var enemy in TurnManager.instance.enemies)
                if (enemy != null) enemy.gameObject.SetActive(false);
        }

        if (mapUI != null)
        {
            mapUI.RefreshNodeStates(currentMap);
            mapUI.Show();
            mapUI.CenterOnCurrentNode(currentMap);
        }
    }

    public void HideMap()
    {
        if (mapUI != null) mapUI.Hide();

        // Player'ı geri getir
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            TurnManager.instance.player.gameObject.SetActive(true);

        // Savaşa girerken combat UI'ları göster
        SetCombatUIVisible(true);
    }

    /// <summary>
    /// ActivePerkBar gibi savaş UI'larını göster/gizle.
    /// Map'te gizli, savaşta görünür.
    /// </summary>
    private void SetCombatUIVisible(bool visible)
    {
        if (ActivePerkBar.instance != null && ActivePerkBar.instance.barCanvas != null)
            ActivePerkBar.instance.barCanvas.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Sadece map node'larını gizler — arkaplan/parallax görünmeye devam eder.
    /// Rest UI gibi overlay ekranlarında kullanılır.
    /// </summary>
    public void HideMapNodes()
    {
        if (mapUI != null && mapUI.nodeContainer != null)
            mapUI.nodeContainer.gameObject.SetActive(false);
    }

    public void ShowMapNodes()
    {
        if (mapUI != null && mapUI.nodeContainer != null)
            mapUI.nodeContainer.gameObject.SetActive(true);
    }

    /// <summary>
    /// Savaş dışı bölümlerde çağır: player, düşmanlar, tüm tilemap'ler gizlenir.
    /// Sadece UI overlay ekranları (shop, perk, rest) görünsün.
    /// </summary>
    private void HideGameplayElements()
    {
        // Player'ı gizle
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            TurnManager.instance.player.gameObject.SetActive(false);

        // Düşmanları gizle
        if (TurnManager.instance != null)
        {
            foreach (var enemy in TurnManager.instance.enemies)
                if (enemy != null) enemy.gameObject.SetActive(false);
        }

        // Tüm tilemap'leri temizle
        if (LevelGenerator.instance != null)
        {
            LevelGenerator.instance.groundMap?.ClearAllTiles();
            if (LevelGenerator.instance.columnMap != null)
                LevelGenerator.instance.columnMap.ClearAllTiles();
            if (LevelGenerator.instance.foreGroundA != null)
                LevelGenerator.instance.foreGroundA.ClearAllTiles();
            if (LevelGenerator.instance.foreGroundB != null)
                LevelGenerator.instance.foreGroundB.ClearAllTiles();
            if (ScaffoldManager.instance != null)
                ScaffoldManager.instance.ClearAll();
        }
    }

    private void NotifyDiceHoarder()
    {
        if (RunManager.instance == null) return;
        foreach (var p in RunManager.instance.activePerks)
            if (p is DiceHoarderPerk hoarder) hoarder.OnCampfireVisited();
    }

    // ─── Layer config'i güvenli al (fallback: son config) ───
    public MapLayerData GetLayerConfig(int layerIndex)
    {
        if (layerConfigs == null || layerConfigs.Length == 0)
        {
            Debug.LogError("MapManager: layerConfigs boş! Default MapLayerData kullanılıyor.");
            return ScriptableObject.CreateInstance<MapLayerData>();
        }

        int idx = Mathf.Clamp(layerIndex, 0, layerConfigs.Length - 1);
        return layerConfigs[idx];
    }
}
