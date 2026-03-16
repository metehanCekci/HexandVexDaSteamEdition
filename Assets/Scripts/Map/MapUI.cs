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
    public GameObject mapNodePrefab;

    [Header("Layout Settings")]
    public float rowSpacing = 120f;
    public float columnSpacing = 140f;

    [Header("Connection Lines")]
    public GameObject linePrefab;

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

    // Bağlantı çizgisi metadata — hangi node'dan hangi node'a
    private struct LineInfo
    {
        public GameObject lineGO;
        public int fromId;
        public int toId;
    }
    private List<LineInfo> lineInfos = new List<LineInfo>();

    // Çizgi animasyon coroutine'leri
    private Coroutine lineAnimCoroutine;

    void Start()
    {
        MapNodeUI.SetIcons(combatIcon, eliteIcon, shopIcon, perkIcon, restIcon, eventIcon, bossIcon);
    }

    public void BuildMap(MapData map)
    {
        ClearMap();

        if (map == null || mapNodePrefab == null || nodeContainer == null) return;

        MapNodeUI.SetIcons(combatIcon, eliteIcon, shopIcon, perkIcon, restIcon, eventIcon, bossIcon);

        int maxRow = 0;
        foreach (var node in map.nodes)
        {
            if (node.row > maxRow) maxRow = node.row;
        }

        Dictionary<int, List<MapNode>> rowMap = new Dictionary<int, List<MapNode>>();
        foreach (var node in map.nodes)
        {
            if (!rowMap.ContainsKey(node.row))
                rowMap[node.row] = new List<MapNode>();
            rowMap[node.row].Add(node);
        }

        float totalHeight = (maxRow + 1) * rowSpacing + 300f;
        nodeContainer.sizeDelta = new Vector2(nodeContainer.sizeDelta.x, totalHeight);

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
                float yPos = node.row * rowSpacing + 150f;

                rt.anchoredPosition = new Vector2(xPos, yPos);
                nodeTransforms[node.id] = rt;
            }
        }

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

        // Bağlantı çizgilerini güncelle
        UpdateLineStates(map, reachableIds);
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

        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Show();
    }

    public void CenterOnCurrentNode(MapData map)
    {
        if (map == null || nodeContainer == null) return;

        Canvas.ForceUpdateCanvases();

        var dragScroll = nodeContainer.GetComponentInParent<MapDragScroll>();
        if (dragScroll == null) return;

        if (map.currentNodeId == -1)
        {
            dragScroll.ScrollToBottom();
        }
        else
        {
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

        if (PerkInventoryUI.instance != null)
            PerkInventoryUI.instance.Hide();

        // Çizgi animasyonlarını durdur
        if (lineAnimCoroutine != null) { StopCoroutine(lineAnimCoroutine); lineAnimCoroutine = null; }
    }

    // ═══════════════════════════════════════════════════════
    // BAĞLANTI ÇİZGİLERİ
    // ═══════════════════════════════════════════════════════

    private void UpdateLineStates(MapData map, HashSet<int> reachableIds)
    {
        // Eski animasyonu durdur
        if (lineAnimCoroutine != null) { StopCoroutine(lineAnimCoroutine); lineAnimCoroutine = null; }

        List<Image> glowLines = new List<Image>();

        foreach (var info in lineInfos)
        {
            if (info.lineGO == null) continue;
            Image img = info.lineGO.GetComponent<Image>();
            if (img == null) continue;

            MapNode fromNode = map.GetNode(info.fromId);
            MapNode toNode = map.GetNode(info.toId);
            if (fromNode == null || toNode == null) continue;

            bool isVisitedPath = fromNode.visited && toNode.visited;
            bool isCurrentPath = (fromNode.id == map.currentNodeId && reachableIds.Contains(info.toId));
            bool isReachablePath = reachableIds.Contains(info.toId) || reachableIds.Contains(info.fromId);

            if (isCurrentPath)
            {
                // Aktif yol — parlak, animasyonlu
                img.color = new Color(0.4f, 1f, 0.6f, 0.8f);
                // Çizgiyi kalınlaştır
                RectTransform lrt = info.lineGO.GetComponent<RectTransform>();
                if (lrt != null) lrt.sizeDelta = new Vector2(lrt.sizeDelta.x, 5f);
                glowLines.Add(img);
            }
            else if (isVisitedPath)
            {
                // Geçilmiş yol — soluk
                img.color = new Color(0.4f, 0.4f, 0.4f, 0.25f);
                RectTransform lrt = info.lineGO.GetComponent<RectTransform>();
                if (lrt != null) lrt.sizeDelta = new Vector2(lrt.sizeDelta.x, 2f);
            }
            else if (isReachablePath)
            {
                // Erişilebilir ama mevcut değil
                img.color = new Color(0.6f, 0.6f, 0.6f, 0.45f);
                RectTransform lrt = info.lineGO.GetComponent<RectTransform>();
                if (lrt != null) lrt.sizeDelta = new Vector2(lrt.sizeDelta.x, 3f);
            }
            else
            {
                // Kilitli yol — çok soluk
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.15f);
                RectTransform lrt = info.lineGO.GetComponent<RectTransform>();
                if (lrt != null) lrt.sizeDelta = new Vector2(lrt.sizeDelta.x, 2f);
            }
        }

        // Aktif yolları animasyonla
        if (glowLines.Count > 0)
        {
            lineAnimCoroutine = StartCoroutine(LineGlowLoop(glowLines));
        }
    }

    /// <summary>Aktif bağlantı çizgilerinde pulse glow</summary>
    private IEnumerator LineGlowLoop(List<Image> lines)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) / 2f;
            float alpha = Mathf.Lerp(0.5f, 1f, t);
            Color glowColor = new Color(0.4f, 1f, 0.6f, alpha);

            foreach (var img in lines)
            {
                if (img != null) img.color = glowColor;
            }

            yield return null;
        }
    }

    private void ClearMap()
    {
        if (lineAnimCoroutine != null) { StopCoroutine(lineAnimCoroutine); lineAnimCoroutine = null; }

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
        lineInfos.Clear();

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
                    lineImg.color = new Color(0.4f, 0.4f, 0.4f, 0.3f);
                }

                spawnedLines.Add(lineGO);
                lineInfos.Add(new LineInfo { lineGO = lineGO, fromId = node.id, toId = childId });
            }
        }
    }
}
