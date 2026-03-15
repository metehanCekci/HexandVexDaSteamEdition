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
            defaultConfig.maxNodesPerRow = 4;
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

        // Rest UI yoksa otomatik oluştur
        if (RestNodeUI.instance == null)
        {
            BuildRestUIFromCode();
        }

        // Shop continue butonu
        BuildShopContinueButton();
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
        panelBG.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
        CanvasGroup cg = panelGO.AddComponent<CanvasGroup>();

        // Başlık
        GameObject titleGO = MakeUIObj("Title", panelGO.transform);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -20f);
        titleRT.sizeDelta = new Vector2(400f, 60f);
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "MAP";
        titleTxt.fontSize = 36;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.9f, 0.85f, 0.7f);

        // ScrollRect
        GameObject scrollGO = MakeUIObj("Scroll", panelGO.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0.1f, 0.05f);
        scrollRT.anchorMax = new Vector2(0.9f, 0.9f);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = Vector2.zero;
        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        Image scrollBG = scrollGO.AddComponent<Image>();
        scrollBG.color = new Color(0, 0, 0, 0.01f);
        scrollGO.AddComponent<Mask>().showMaskGraphic = false;

        // NodeContainer (scroll content)
        GameObject containerGO = MakeUIObj("NodeContainer", scrollGO.transform);
        RectTransform containerRT = containerGO.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0f);
        containerRT.anchorMax = new Vector2(0.5f, 0f);
        containerRT.pivot = new Vector2(0.5f, 0f);
        containerRT.sizeDelta = new Vector2(800f, 1200f);
        scroll.content = containerRT;

        // Node prefab
        GameObject nodePrefab = BuildNodePrefab();

        // Line prefab
        GameObject linePrefab = new GameObject("LinePrefab");
        linePrefab.SetActive(false);
        var lineRT = linePrefab.AddComponent<RectTransform>();
        lineRT.sizeDelta = new Vector2(100f, 3f);
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
        ui.rowSpacing = 120f;
        ui.columnSpacing = 140f;
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
        rt.sizeDelta = new Vector2(70f, 70f);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(1f, 0.9f, 0.5f);
        cb.pressedColor = new Color(0.8f, 0.7f, 0.3f);
        cb.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        btn.colors = cb;

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
        labelRT.sizeDelta = new Vector2(100f, 20f);
        var labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
        labelTxt.fontSize = 11;
        labelTxt.alignment = TextAlignmentOptions.Center;
        labelTxt.color = new Color(0.7f, 0.7f, 0.7f);

        MapNodeUI nodeUI = go.AddComponent<MapNodeUI>();
        nodeUI.iconImage = iconImg;
        nodeUI.backgroundImage = bg;
        nodeUI.button = btn;
        nodeUI.labelText = labelTxt;

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

    private void BuildShopContinueButton()
    {
        if (Shopmanager.instance == null || Shopmanager.instance.continueButton != null) return;
        Transform shopParent = Shopmanager.instance.shopSlotContainer != null
            ? Shopmanager.instance.shopSlotContainer.parent : null;
        if (shopParent == null) return;

        GameObject btn = MakeButton("Continue", shopParent, new Vector2(0f, -80f), new Color(0.2f, 0.5f, 0.8f));
        btn.SetActive(false);
        Shopmanager.instance.continueButton = btn;
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
        GenerateNewMap(0);
        ShowMap();
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

        // RunManager'a aktif node tipini bildir
        if (RunManager.instance != null)
            RunManager.instance.currentNodeType = node.nodeType;

        switch (node.nodeType)
        {
            case MapNodeType.Combat:
            case MapNodeType.EliteCombat:
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateNextLevel();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateNextLevel();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Shop:
                HideMap();
                if (Shopmanager.instance != null)
                {
                    Shopmanager.instance.OpenAsMapNode();
                }
                isTransitioning = false;
                break;

            case MapNodeType.PerkSelection:
                HideMap();
                if (LevelUpManager.instance != null)
                {
                    LevelUpManager.instance.ShowLevelUpScreen();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Rest:
                HideMap();
                if (RestNodeUI.instance != null)
                {
                    RestNodeUI.instance.Show();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Event:
                // Stub: şimdilik combat gibi davransın
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateNextLevel();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateNextLevel();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Boss:
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateBossArena();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateBossArena();
                }
                isTransitioning = false;
                break;
        }
    }

    // ─── Combat/Shop/Perk/Rest bittikten sonra haritaya dön ───
    public void OnNodeComplete()
    {
        isTransitioning = false;

        if (mapUI != null) mapUI.RefreshNodeStates(currentMap);

        // Boss node ise layer'ı ilerlet
        MapNode current = currentMap?.GetNode(currentMap.currentNodeId);
        if (current != null && current.nodeType == MapNodeType.Boss)
        {
            OnBossDefeated();
            return;
        }

        ShowMap();
    }

    // ─── Boss yenildiğinde yeni layer'a geç ───
    public void OnBossDefeated()
    {
        isTransitioning = false;

        if (RunManager.instance != null)
        {
            RunManager.instance.currentLayerIndex++;

            // Boss legendary multiplier'ı burada artır (LevelGenerator'dan taşındı)
            // LevelGenerator.GenerateNextLevel() zaten bunu kontrol ediyor ama
            // ileride tamamen buraya taşınacak
        }

        // Yeni layer haritası üret
        int newLayerIndex = RunManager.instance != null ? RunManager.instance.currentLayerIndex : 0;
        GenerateNewMap(newLayerIndex);
        ShowMap();
    }

    // ─── Map UI göster/gizle ───
    public void ShowMap()
    {
        if (mapUI != null)
        {
            mapUI.RefreshNodeStates(currentMap);
            mapUI.Show();
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
