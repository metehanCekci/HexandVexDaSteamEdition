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
            defaultConfig.eventChance = 0.08f;
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

        // Rest UI yoksa otomatik oluştur
        if (RestNodeUI.instance == null)
        {
            BuildRestUIFromCode();
        }

        // Auto-spawn Inventory & Hotbar if not present
        EnsureInventoryAndHotbar();
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
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel (arka plan)
        GameObject panelGO = MakeUIObj("MapPanel", canvasGO.transform);
        StretchFull(panelGO.GetComponent<RectTransform>());
        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = Color.black; // Siyah arka plan (ileride sprite ile değiştirilebilir)
        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();

        // Başlık — üstte sabit, scroll'dan bağımsız
        GameObject titleGO = MakeUIObj("Title", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -10f);
        titleRT.sizeDelta = new Vector2(400f, 50f);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "MAP";
        titleTxt.fontSize = 36;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.9f, 0.85f, 0.7f);

        // ─── ScrollRect tabanlı scroll sistemi ───
        // Viewport (scroll alanı) — başlığın altından başlasın
        GameObject scrollGO = MakeUIObj("Scroll", panelGO.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = new Vector2(0f, -60f); // Başlık için üstten 60px boşluk
        scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // Raycast almak için
        scrollGO.AddComponent<Mask>().showMaskGraphic = true; // İçeriği kırp

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

        mapUI = ui;
        canvasGO.SetActive(false); // Başlangıçta tüm canvas'ı kapat

        Debug.Log("MapManager: Map UI otomatik oluşturuldu.");
    }

    private GameObject BuildNodePrefab()
    {
        GameObject go = new GameObject("NodePrefab");
        go.SetActive(false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(110f, 110f);
        // Container pivot=bottom: anchor bottom-center
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        Button btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = bg;

        // Outline (ince çerçeve — #00050C renk)
        GameObject outlineGO = MakeUIObj("Outline", go.transform);
        var outlineRT = outlineGO.GetComponent<RectTransform>();
        outlineRT.anchorMin = Vector2.zero;
        outlineRT.anchorMax = Vector2.one;
        outlineRT.offsetMin = new Vector2(-3f, -3f);
        outlineRT.offsetMax = new Vector2(3f, 3f);
        Image outlineImg = outlineGO.AddComponent<Image>();
        outlineImg.color = new Color(0f / 255f, 5f / 255f, 12f / 255f, 1f); // #00050C
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

    private void BuildRestUIFromCode()
    {
        if (restUIBuilt) return;
        restUIBuilt = true;

        GameObject canvasGO = new GameObject("RestCanvas");
        Canvas c = canvasGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 95;
        var sc = canvasGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject panelGO = MakeUIObj("RestPanel", canvasGO.transform);
        StretchFull(panelGO.GetComponent<RectTransform>());
        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.05f, 0.92f);

        // Başlık
        GameObject titleGO = MakeUIObj("Title", panelGO.transform);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.7f);
        titleRT.anchorMax = new Vector2(0.5f, 0.7f);
        titleRT.sizeDelta = new Vector2(400f, 60f);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "Campfire";
        titleTxt.fontSize = 42;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.7f, 0.3f);

        // Rest butonu
        GameObject restBtn = MakeButton("Rest", panelGO.transform, new Vector2(-120f, -30f), new Color(0.2f, 0.6f, 0.3f));
        // Train butonu
        GameObject trainBtn = MakeButton("Train", panelGO.transform, new Vector2(120f, -30f), new Color(0.3f, 0.3f, 0.7f));

        // Info text
        GameObject infoGO = MakeUIObj("Info", panelGO.transform);
        var infoRT = infoGO.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.5f, 0.25f);
        infoRT.anchorMax = new Vector2(0.5f, 0.25f);
        infoRT.sizeDelta = new Vector2(400f, 40f);
        var infoTxt = infoGO.AddComponent<TextMeshProUGUI>();
        infoTxt.fontSize = 24;
        infoTxt.alignment = TextAlignmentOptions.Center;
        infoTxt.color = Color.white;

        RestNodeUI restUI = panelGO.AddComponent<RestNodeUI>();
        restUI.restPanel = panelGO;
        restUI.restButton = restBtn.GetComponent<Button>();
        restUI.trainButton = trainBtn.GetComponent<Button>();
        restUI.titleText = titleTxt;
        restUI.restButtonText = restBtn.GetComponentInChildren<TMP_Text>();
        restUI.trainButtonText = trainBtn.GetComponentInChildren<TMP_Text>();
        restUI.infoText = infoTxt;

        panelGO.SetActive(false);
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

    private IEnumerator StartNewRunDelayed()
    {
        // UI'ın oluşması için 1 frame bekle
        yield return null;

        // Reset inventory for new run
        if (InventoryManager.instance != null)
            InventoryManager.instance.ResetForNewRun();

        GenerateNewMap(0);

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

        if (mapUI != null) mapUI.RefreshNodeStates(currentMap);

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
        // 1. Ekranı karart
        yield return StartCoroutine(FadeToBlack());

        // 2. Map'i gizle
        HideMap();

        // 3. İçeriği yükle + hotbar visibility
        bool showHotbar = false;

        switch (node.nodeType)
        {
            case MapNodeType.Combat:
            case MapNodeType.EliteCombat:
            case MapNodeType.Event:
                RunManager.instance.currentLevel++;
                LevelGenerator.instance.GenerateNextLevel();
                showHotbar = true; // Hotbar visible during combat
                break;

            case MapNodeType.Shop:
                // Full-screen canvas shop — clear EVERYTHING behind
                if (LevelGenerator.instance != null)
                {
                    LevelGenerator.instance.groundMap?.ClearAllTiles();
                    if (LevelGenerator.instance.backgroundMap != null)
                        LevelGenerator.instance.backgroundMap.ClearAllTiles();
                    if (LevelGenerator.instance.hazardMap != null)
                        LevelGenerator.instance.hazardMap.ClearAllTiles();
                    if (LevelGenerator.instance.scaffoldMap != null)
                        LevelGenerator.instance.scaffoldMap.ClearAllTiles();
                    if (ScaffoldManager.instance != null)
                        ScaffoldManager.instance.ClearAll();
                }
                // Hide player
                if (TurnManager.instance != null && TurnManager.instance.player != null)
                    TurnManager.instance.player.gameObject.SetActive(false);
                // Destroy remaining enemies
                if (TurnManager.instance != null)
                {
                    foreach (var enemy in TurnManager.instance.enemies)
                        if (enemy != null) Destroy(enemy.gameObject);
                    TurnManager.instance.enemies.Clear();
                }
                if (Shopmanager.instance != null)
                    Shopmanager.instance.OpenAsMapNode();
                showHotbar = false;
                break;

            case MapNodeType.PerkSelection:
                // Perk seçim ekranı açılmadan önce level tile'larını temizle
                if (LevelGenerator.instance != null)
                {
                    LevelGenerator.instance.groundMap?.ClearAllTiles();
                    if (LevelGenerator.instance.backgroundMap != null)
                        LevelGenerator.instance.backgroundMap.ClearAllTiles();
                }
                if (LevelUpManager.instance != null)
                    LevelUpManager.instance.ShowLevelUpScreen();
                break;

            case MapNodeType.Rest:
                // Rest açılmadan önce level tile'larını temizle
                if (LevelGenerator.instance != null)
                {
                    LevelGenerator.instance.groundMap?.ClearAllTiles();
                    if (LevelGenerator.instance.backgroundMap != null)
                        LevelGenerator.instance.backgroundMap.ClearAllTiles();
                }
                if (RestNodeUI.instance != null)
                    RestNodeUI.instance.Show();
                break;

            case MapNodeType.Boss:
                RunManager.instance.currentLevel++;
                LevelGenerator.instance.GenerateBossArena();
                showHotbar = true; // Hotbar visible during boss
                break;
        }

        if (HotbarUI.instance != null)
            HotbarUI.instance.SetVisible(showHotbar);

        // 4. İçeriğin render edilmesi için kısa bekleme
        yield return new WaitForSecondsRealtime(0.2f);

        // 5. Ekranı aydınlat
        yield return StartCoroutine(FadeFromBlack());

        isTransitioning = false;
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
        // Re-enable player if it was hidden (e.g. shop)
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            TurnManager.instance.player.gameObject.SetActive(true);

        // ScreenFader'ın mevcut fade'i bitmesini bekle
        if (ScreenFader.instance != null)
        {
            // Ekranı karart
            yield return StartCoroutine(FadeToBlack());

            // Map'i arkada hazırla
            if (mapUI != null) mapUI.RefreshNodeStates(currentMap);
            ShowMapInstant();

            // 1 frame bekle — viewport boyutu güncellensin
            yield return null;
            if (mapUI != null) mapUI.CenterOnCurrentNode(currentMap);

            // Kısa bekleme (map render edilsin)
            yield return new WaitForSecondsRealtime(0.2f);

            // Ekranı aydınlat
            yield return StartCoroutine(FadeFromBlack());
        }
        else
        {
            ShowMapInstant();
        }
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

        // Boss sahnesini temizle — yoksa arkada boş level gözükür
        if (LevelGenerator.instance != null)
        {
            LevelGenerator.instance.groundMap?.ClearAllTiles();
            if (LevelGenerator.instance.backgroundMap != null)
                LevelGenerator.instance.backgroundMap.ClearAllTiles();
        }

        if (ScreenFader.instance != null)
        {
            yield return StartCoroutine(FadeToBlack());

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
        // Hide hotbar when map is shown
        if (HotbarUI.instance != null)
            HotbarUI.instance.SetVisible(false);

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
    }

    // ─── Layer config'i güvenli al (fallback: son config) ───
    private MapLayerData GetLayerConfig(int layerIndex)
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
