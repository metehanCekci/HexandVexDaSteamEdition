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

        // İkon'ları her build'de güncelle (geç yükleme durumu için)
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

        // Content viewport'tan büyük olmalı
        var dragScroll = nodeContainer != null ? nodeContainer.GetComponentInParent<MapDragScroll>() : null;
        if (dragScroll != null && dragScroll.viewport != null)
        {
            float viewportH = dragScroll.viewport.rect.height;
            if (viewportH > 0 && totalHeight <= viewportH)
                totalHeight = viewportH + 200f;
        }

        nodeContainer.sizeDelta = new Vector2(nodeContainer.sizeDelta.x, totalHeight);
        Debug.Log($"[MAP] Container height={totalHeight}, maxRow={maxRow}, rowSpacing={rowSpacing}");

        // ─── Node'ları yerleştir ───
        foreach (var node in map.nodes)
        {
            GameObject nodeGO = Instantiate(mapNodePrefab, nodeContainer);
            nodeGO.SetActive(true); // Prefab inactive olabilir, zorla aktif et
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
                float yPos = node.row * rowSpacing + 50f; // Alttan yukarı

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

        // Scroll'u en alta getir (oyuncu alttan başlıyor)
        if (nodeContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            var dragScroll = nodeContainer.GetComponentInParent<MapDragScroll>();
            if (dragScroll != null)
                dragScroll.ScrollToBottom();
        }
    }

    public void RefreshNodeStates(MapData map)
    {
        if (map == null) return;

        HashSet<int> reachableIds = new HashSet<int>();

        if (map.currentNodeId == -1)
        {
            // Henüz başlanmadı: row 0 node'ları reachable
            foreach (var node in map.nodes)
            {
                if (node.row == 0) reachableIds.Add(node.id);
            }
        }
        else
        {
            // Current node'un child'ları reachable
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
                lineGO.SetActive(true); // Prefab inactive olabilir
                // Çizgiyi node'ların arkasına at
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
