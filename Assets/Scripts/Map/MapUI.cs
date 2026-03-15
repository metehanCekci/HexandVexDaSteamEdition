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

                // Jitter (start ve boss hariç)
                if (node.row > 0 && node.row < maxRow)
                {
                    xPos += Random.Range(-nodeJitter, nodeJitter);
                    yPos += Random.Range(-nodeJitter * 0.5f, nodeJitter * 0.5f);
                }

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

        HashSet<int> reachableIds = new HashSet<int>();

        if (map.currentNodeId == -1)
        {
            foreach (var node in map.nodes)
            {
                if (node.row == 0) reachableIds.Add(node.id);
            }
        }
        else
        {
            MapNode current = map.GetNode(map.currentNodeId);
            if (current != null)
            {
                foreach (int childId in current.childIds)
                    reachableIds.Add(childId);
            }
        }

        foreach (var nodeUI in spawnedNodes)
        {
            MapNode node = map.GetNode(nodeUI.nodeId);
            if (node == null) continue;

            bool isReachable = reachableIds.Contains(node.id);
            bool isVisited = node.visited;
            bool isCurrent = node.id == map.currentNodeId;

            nodeUI.SetState(isReachable, isVisited, isCurrent);
        }
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
                }

                spawnedLines.Add(lineGO);
            }
        }
    }
}
