using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MapUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject mapPanel;
    public CanvasGroup canvasGroup;

    [Header("Node Layout")]
    public RectTransform nodeContainer;
    public GameObject mapNodePrefab; // MapNodeUI component'li prefab

    [Header("Layout Settings")]
    public float rowSpacing = 120f;
    public float columnSpacing = 140f;
    public float nodeJitter = 15f;

    [Header("Connection Lines")]
    public GameObject linePrefab; // Basit Image (stretched) prefab

    [Header("Node Icons (opsiyonel)")]
    public Sprite combatIcon;
    public Sprite eliteIcon;
    public Sprite shopIcon;
    public Sprite perkIcon;
    public Sprite restIcon;
    public Sprite eventIcon;
    public Sprite bossIcon;

    private List<MapNodeUI> spawnedNodes = new List<MapNodeUI>();
    private List<GameObject> spawnedLines = new List<GameObject>();
    private Dictionary<int, RectTransform> nodeTransforms = new Dictionary<int, RectTransform>();

    // Çizgi → hangi node'dan hangi node'a gidiyor (dim/bright için)
    private struct LineEdge
    {
        public int fromId;
        public int toId;
        public Image image;
    }
    private List<LineEdge> lineEdges = new List<LineEdge>();

    void Start()
    {
        MapNodeUI.SetIcons(combatIcon, eliteIcon, shopIcon, perkIcon, restIcon, eventIcon, bossIcon);
    }

    public void BuildMap(MapData map)
    {
        ClearMap();

        if (map == null || mapNodePrefab == null || nodeContainer == null) return;

        MapNodeUI.SetIcons(combatIcon, eliteIcon, shopIcon, perkIcon, restIcon, eventIcon, bossIcon);

        // ─── Max row bul ───
        int maxRow = 0;
        foreach (var node in map.nodes)
        {
            if (node.row > maxRow) maxRow = node.row;
        }

        // ─── Row'daki node sayılarını hesapla ───
        Dictionary<int, List<MapNode>> rowMap = new Dictionary<int, List<MapNode>>();
        foreach (var node in map.nodes)
        {
            if (!rowMap.ContainsKey(node.row))
                rowMap[node.row] = new List<MapNode>();
            rowMap[node.row].Add(node);
        }

        // ─── Container boyutunu ayarla ───
        float totalHeight = (maxRow + 1) * rowSpacing + 300f;
        nodeContainer.sizeDelta = new Vector2(nodeContainer.sizeDelta.x, totalHeight);
        Debug.Log($"[MAP] Container height={totalHeight}, maxRow={maxRow}, rowSpacing={rowSpacing}");

        // Parallax arkaplanı güncelle
        var parallax = nodeContainer.parent.GetComponent<MapParallaxBG>();
        if (parallax != null) parallax.Refresh();

        // ─── Node'ları yerleştir ───
        // Container pivot=bottom: y=0 en alt, pozitif y yukarı
        // Row 0 (start) en altta, boss (maxRow) en üstte
        foreach (var node in map.nodes)
        {
            GameObject nodeGO = Instantiate(mapNodePrefab, nodeContainer);
            nodeGO.SetActive(true);
            MapNodeUI nodeUI = nodeGO.GetComponent<MapNodeUI>();

            if (nodeUI != null)
            {
                nodeUI.Setup(node);
                spawnedNodes.Add(nodeUI);
            }

            RectTransform rt = nodeGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                int nodesInRow = rowMap.ContainsKey(node.row) ? rowMap[node.row].Count : 1;
                float xPos = CalculateXPosition(node.column, nodesInRow);

                // Bottom-up: row 0 → y=150, row 1 → y=150+spacing, ... boss → y=150+maxRow*spacing
                float yPos = node.row * rowSpacing + 150f;

                rt.anchoredPosition = new Vector2(xPos, yPos);
                nodeTransforms[node.id] = rt;
            }
        }

        // ─── Bağlantı çizgilerini çiz ───
        DrawConnections(map);

        RefreshNodeStates(map);
    }

    public void RefreshNodeStates(MapData map)
    {
        if (map == null) return;

        // ─── Bir sonraki adımda seçilebilecek node'lar ───
        HashSet<int> nextStepIds = new HashSet<int>();

        if (map.currentNodeId == -1)
        {
            foreach (var node in map.nodes)
            {
                if (node.row == 0) nextStepIds.Add(node.id);
            }
        }
        else
        {
            MapNode current = map.GetNode(map.currentNodeId);
            if (current != null)
            {
                foreach (int childId in current.childIds)
                    nextStepIds.Add(childId);
            }
        }

        // ─── Gelecekte ulaşılabilir tüm node'ları hesapla (BFS) ───
        // current node'dan ileriye doğru tüm olası yollar
        HashSet<int> futureReachable = new HashSet<int>();
        Queue<int> bfsQueue = new Queue<int>();

        if (map.currentNodeId == -1)
        {
            // Henüz başlanmadı — tüm node'lar potansiyel olarak erişilebilir
            foreach (var node in map.nodes)
                futureReachable.Add(node.id);
        }
        else
        {
            // Current node + ondan ileriye gidilebilecek her şey
            futureReachable.Add(map.currentNodeId);
            foreach (int childId in map.GetNode(map.currentNodeId).childIds)
            {
                bfsQueue.Enqueue(childId);
                futureReachable.Add(childId);
            }
            while (bfsQueue.Count > 0)
            {
                int nodeId = bfsQueue.Dequeue();
                MapNode n = map.GetNode(nodeId);
                if (n == null) continue;
                foreach (int childId in n.childIds)
                {
                    if (!futureReachable.Contains(childId))
                    {
                        futureReachable.Add(childId);
                        bfsQueue.Enqueue(childId);
                    }
                }
            }
        }

        // ─── Node durumlarını güncelle ───
        foreach (var nodeUI in spawnedNodes)
        {
            MapNode node = map.GetNode(nodeUI.nodeId);
            if (node == null) continue;

            bool isNextStep = nextStepIds.Contains(node.id);
            bool isVisited = node.visited;
            bool isCurrent = node.id == map.currentNodeId;
            bool isFutureReachable = futureReachable.Contains(node.id);

            nodeUI.SetState(isNextStep, isVisited, isCurrent, isFutureReachable);
        }

        // ─── Çizgi renklerini güncelle ───
        foreach (var edge in lineEdges)
        {
            if (edge.image == null) continue;

            bool fromReachable = futureReachable.Contains(edge.fromId);
            bool toReachable = futureReachable.Contains(edge.toId);

            if (fromReachable && toReachable)
            {
                // Aktif yol — normal parlak çizgi
                bool isNextEdge = nextStepIds.Contains(edge.toId) &&
                    (edge.fromId == map.currentNodeId || map.currentNodeId == -1);
                if (isNextEdge)
                    edge.image.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
                else
                    edge.image.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }
            else
            {
                // Artık ulaşılamaz — soluk
                edge.image.color = new Color(0.3f, 0.3f, 0.3f, 0.15f);
            }
        }
    }

    public bool IsMapVisible()
    {
        return mapPanel != null && mapPanel.activeSelf;
    }

    public void Show()
    {
        if (mapPanel != null) mapPanel.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        // Perk yan panelini göster
        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Show();
    }

    /// <summary>
    /// Scroll'u doğru pozisyona getir:
    /// İlk açılış → row 0 (en alt) görünsün
    /// Bölüm bittikten sonra → seçilebilecek node'lar ortada
    /// </summary>
    public void CenterOnCurrentNode(MapData map)
    {
        if (map == null || nodeContainer == null) return;

        Canvas.ForceUpdateCanvases();

        var dragScroll = nodeContainer.GetComponentInParent<MapDragScroll>();
        if (dragScroll == null) return;

        if (map.currentNodeId == -1)
        {
            // İlk açılış: en alta scroll — row 0 görünsün
            dragScroll.ScrollToBottom();
        }
        else
        {
            // Bitirilen node'un child'larını bul, ortalama Y'sine scroll et
            MapNode current = map.GetNode(map.currentNodeId);
            if (current != null && current.childIds.Count > 0)
            {
                float avgY = 0f;
                int count = 0;
                foreach (int childId in current.childIds)
                {
                    if (nodeTransforms.ContainsKey(childId))
                    {
                        avgY += nodeTransforms[childId].anchoredPosition.y;
                        count++;
                    }
                }
                if (count > 0)
                {
                    avgY /= count;
                    dragScroll.ScrollToNodeY(avgY, true);
                }
            }
            else if (nodeTransforms.ContainsKey(map.currentNodeId))
            {
                // Child yoksa (boss?) current node'u ortala
                float nodeY = nodeTransforms[map.currentNodeId].anchoredPosition.y;
                dragScroll.ScrollToNodeY(nodeY, true);
            }
        }
    }

    public void Hide()
    {
        if (mapPanel != null) mapPanel.SetActive(false);
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // Perk inventory her zaman görünür — burada kapatma
    }

    private void ClearMap()
    {
        foreach (var nodeUI in spawnedNodes)
        {
            if (nodeUI != null) Destroy(nodeUI.gameObject);
        }
        spawnedNodes.Clear();

        foreach (var line in spawnedLines)
        {
            if (line != null) Destroy(line);
        }
        spawnedLines.Clear();
        lineEdges.Clear();

        nodeTransforms.Clear();
    }

    private float CalculateXPosition(int column, int nodesInRow)
    {
        if (nodesInRow <= 1) return 0f;
        float totalWidth = (nodesInRow - 1) * columnSpacing;
        return -totalWidth / 2f + column * columnSpacing;
    }

    private void DrawConnections(MapData map)
    {
        if (linePrefab == null) return;

        foreach (var node in map.nodes)
        {
            if (!nodeTransforms.ContainsKey(node.id)) continue;
            RectTransform fromRT = nodeTransforms[node.id];

            foreach (int childId in node.childIds)
            {
                if (!nodeTransforms.ContainsKey(childId)) continue;
                RectTransform toRT = nodeTransforms[childId];

                GameObject lineGO = Instantiate(linePrefab, nodeContainer);
                lineGO.SetActive(true);
                lineGO.transform.SetAsFirstSibling();

                RectTransform lineRT = lineGO.GetComponent<RectTransform>();
                if (lineRT != null)
                {
                    Vector2 from = fromRT.anchoredPosition;
                    Vector2 to = toRT.anchoredPosition;
                    Vector2 mid = (from + to) / 2f;
                    float distance = Vector2.Distance(from, to);
                    float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

                    lineRT.anchoredPosition = mid;
                    lineRT.sizeDelta = new Vector2(distance, 3f);
                    lineRT.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                Image lineImg = lineGO.GetComponent<Image>();
                if (lineImg != null)
                {
                    lineImg.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);

                    lineEdges.Add(new LineEdge
                    {
                        fromId = node.id,
                        toId = childId,
                        image = lineImg
                    });
                }

                spawnedLines.Add(lineGO);
            }
        }
    }
}
