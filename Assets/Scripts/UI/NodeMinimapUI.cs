using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Pause menüsüne eklenecek statik node haritası görünümü.
/// Geçilen, mevcut ve gelecek node'ları ikonlarla gösterir.
/// Collapsible dropdown olarak çalışır.
/// İkonları MapManager'dan alır.
/// </summary>
public class NodeMinimapUI : MonoBehaviour
{
    public static NodeMinimapUI instance;

    [Header("Panel")]
    public GameObject contentPanel;
    public Button toggleButton;
    public TMP_Text toggleButtonText;

    [Header("Font")]
    public TMP_FontAsset minimapFont;

    private RectTransform container;
    private bool isExpanded = false;

    // Layout
    private const float NODE_SIZE = 30f;
    private const float ROW_SPACING = 40f;
    private const float COL_SPACING = 55f;
    private const float LINE_THICKNESS = 2f;
    private const float PADDING = 15f;

    void Awake()
    {
        instance = this;
        if (contentPanel != null) contentPanel.SetActive(false);
        UpdateToggleText();

        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Toggle()
    {
        isExpanded = !isExpanded;
        if (isExpanded)
            Show();
        else
            Hide();
    }

    public void Show()
    {
        isExpanded = true;
        if (contentPanel != null) contentPanel.SetActive(true);
        Rebuild();
        UpdateToggleText();
    }

    public void Hide()
    {
        isExpanded = false;
        if (contentPanel != null) contentPanel.SetActive(false);
        UpdateToggleText();
    }

    public void Refresh()
    {
        if (isExpanded) Rebuild();
    }

    private void UpdateToggleText()
    {
        if (toggleButtonText != null)
            toggleButtonText.text = isExpanded ? "NODE MAP  v" : "NODE MAP  >";
    }

    // ═══════════════════════════════════════════
    // BUILD
    // ═══════════════════════════════════════════

    private void Rebuild()
    {
        // Eski child'ları temizle
        if (contentPanel == null) return;
        foreach (Transform child in contentPanel.transform)
            Destroy(child.gameObject);
        container = null;

        if (MapManager.instance == null || MapManager.instance.currentMap == null)
            return;

        MapData map = MapManager.instance.currentMap;
        if (map.nodes == null || map.nodes.Count == 0) return;

        // Row bilgisi
        int maxRow = 0;
        Dictionary<int, List<MapNode>> rowMap = new Dictionary<int, List<MapNode>>();
        foreach (var node in map.nodes)
        {
            if (node.row > maxRow) maxRow = node.row;
            if (!rowMap.ContainsKey(node.row))
                rowMap[node.row] = new List<MapNode>();
            rowMap[node.row].Add(node);
        }

        // Content panel boyutunu ayarla (fixed size, NOT stretched)
        float totalHeight = (maxRow + 1) * ROW_SPACING + PADDING * 2;
        RectTransform contentRT = contentPanel.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(contentRT.sizeDelta.x, totalHeight);

        // Container oluştur
        GameObject containerGO = new GameObject("Container", typeof(RectTransform));
        containerGO.transform.SetParent(contentPanel.transform, false);
        container = containerGO.GetComponent<RectTransform>();
        container.anchorMin = new Vector2(0, 0);
        container.anchorMax = new Vector2(1, 0);
        container.pivot = new Vector2(0.5f, 0);
        container.anchoredPosition = new Vector2(0, PADDING);
        container.sizeDelta = new Vector2(0, (maxRow + 1) * ROW_SPACING);

        // Reachability
        HashSet<int> nextStepIds = new HashSet<int>();
        HashSet<int> futureReachable = new HashSet<int>();
        ComputeReachability(map, nextStepIds, futureReachable);

        // İkonları al
        Sprite combatIcon = null, eliteIcon = null, bossIcon = null;
        Sprite restIcon = null, sacrificeIcon = null;
        Sprite enchantIcon = null, treasureIcon = null;
        if (MapManager.instance != null)
        {
            combatIcon = MapManager.instance.combatIcon;
            eliteIcon = MapManager.instance.eliteIcon;
            bossIcon = MapManager.instance.bossIcon;
            restIcon = MapManager.instance.restIcon;
            sacrificeIcon = MapManager.instance.sacrificeIcon;
            enchantIcon = MapManager.instance.enchantIcon;
            treasureIcon = MapManager.instance.treasureIcon;
        }

        // Node pozisyonları
        Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();

        // Node'ları yerleştir — row 0 altta, boss üstte
        foreach (var node in map.nodes)
        {
            int nodesInRow = rowMap[node.row].Count;
            float xPos = CalculateX(node.column, nodesInRow);
            float yPos = node.row * ROW_SPACING;
            nodePositions[node.id] = new Vector2(xPos, yPos);

            bool isCurrent = node.id == map.currentNodeId;
            bool isVisited = node.visited;
            bool isNext = nextStepIds.Contains(node.id);
            bool isFuture = futureReachable.Contains(node.id);

            // Node GameObject
            GameObject nodeGO = new GameObject("Node_" + node.id, typeof(RectTransform));
            nodeGO.transform.SetParent(container, false);
            RectTransform nrt = nodeGO.GetComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.5f, 0);
            nrt.anchorMax = new Vector2(0.5f, 0);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(xPos, yPos);
            nrt.sizeDelta = new Vector2(NODE_SIZE, NODE_SIZE);

            // İkon
            Image nodeImg = nodeGO.AddComponent<Image>();
            nodeImg.raycastTarget = false;
            Sprite icon = GetIcon(node.nodeType, combatIcon, eliteIcon, bossIcon, restIcon, sacrificeIcon, enchantIcon, treasureIcon);

            if (icon != null)
            {
                nodeImg.sprite = icon;
                nodeImg.preserveAspect = true;
            }

            // Alpha ayarla duruma göre
            if (isCurrent)
                nodeImg.color = Color.white;
            else if (isVisited)
                nodeImg.color = new Color(1f, 1f, 1f, 0.3f);
            else if (isNext)
                nodeImg.color = Color.white;
            else if (isFuture)
                nodeImg.color = new Color(1f, 1f, 1f, 0.45f);
            else
                nodeImg.color = new Color(1f, 1f, 1f, 0.12f);

            // İkon yoksa fallback label göster
            if (icon == null)
            {
                GameObject lblGO = new GameObject("Label", typeof(RectTransform));
                lblGO.transform.SetParent(nodeGO.transform, false);
                RectTransform lrt = lblGO.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;

                TextMeshProUGUI txt = lblGO.AddComponent<TextMeshProUGUI>();
                txt.text = GetShortLabel(node.nodeType);
                txt.fontSize = 10;
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false;
                txt.color = nodeImg.color;
                if (minimapFont != null) txt.font = minimapFont;

                // İkon yoksa arka plan rengini koy
                nodeImg.color = GetFallbackBgColor(node.nodeType, isCurrent, isVisited, isNext, isFuture);
            }
        }

        // Bağlantı çizgileri (node'ların arkasına)
        foreach (var node in map.nodes)
        {
            if (!nodePositions.ContainsKey(node.id)) continue;
            Vector2 from = nodePositions[node.id];

            foreach (int childId in node.childIds)
            {
                if (!nodePositions.ContainsKey(childId)) continue;
                Vector2 to = nodePositions[childId];

                bool fromR = futureReachable.Contains(node.id);
                bool toR = futureReachable.Contains(childId);
                bool isNextEdge = nextStepIds.Contains(childId) &&
                    (node.id == map.currentNodeId || map.currentNodeId == -1);

                float alpha = isNextEdge ? 0.6f : (fromR && toR) ? 0.25f : 0.08f;
                DrawLine(from, to, new Color(0.7f, 0.7f, 0.7f, alpha));
            }
        }
    }

    private void ComputeReachability(MapData map, HashSet<int> nextStepIds, HashSet<int> futureReachable)
    {
        if (map.currentNodeId == -1)
        {
            foreach (var node in map.nodes)
            {
                if (node.row == 0) nextStepIds.Add(node.id);
                futureReachable.Add(node.id);
            }
            return;
        }

        MapNode current = map.GetNode(map.currentNodeId);
        if (current == null) return;

        foreach (int childId in current.childIds)
            nextStepIds.Add(childId);

        futureReachable.Add(map.currentNodeId);
        Queue<int> bfs = new Queue<int>();
        foreach (int childId in current.childIds)
        {
            bfs.Enqueue(childId);
            futureReachable.Add(childId);
        }
        while (bfs.Count > 0)
        {
            int id = bfs.Dequeue();
            MapNode n = map.GetNode(id);
            if (n == null) continue;
            foreach (int childId in n.childIds)
            {
                if (!futureReachable.Contains(childId))
                {
                    futureReachable.Add(childId);
                    bfs.Enqueue(childId);
                }
            }
        }
    }

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        GameObject lineGO = new GameObject("Line", typeof(RectTransform));
        lineGO.transform.SetParent(container, false);
        lineGO.transform.SetAsFirstSibling();

        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = color;
        lineImg.raycastTarget = false;

        RectTransform lrt = lineGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0);
        lrt.anchorMax = new Vector2(0.5f, 0);
        lrt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 mid = (from + to) / 2f;
        float distance = Vector2.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        lrt.anchoredPosition = mid;
        lrt.sizeDelta = new Vector2(distance, LINE_THICKNESS);
        lrt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private float CalculateX(int column, int nodesInRow)
    {
        if (nodesInRow <= 1) return 0f;
        float totalWidth = (nodesInRow - 1) * COL_SPACING;
        return -totalWidth / 2f + column * COL_SPACING;
    }

    private Sprite GetIcon(MapNodeType type, Sprite combat, Sprite elite, Sprite boss, Sprite rest, Sprite sacrifice, Sprite enchant, Sprite treasure)
    {
        switch (type)
        {
            case MapNodeType.Combat:        return combat;
            case MapNodeType.EliteCombat:    return elite;
            case MapNodeType.Boss:           return boss;
            case MapNodeType.Rest:           return rest;
            case MapNodeType.Sacrifice:      return sacrifice;
            case MapNodeType.Enchant:        return enchant;
            case MapNodeType.Treasure:       return treasure;
            default:                         return null;
        }
    }

    private string GetShortLabel(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Combat:        return "F";
            case MapNodeType.EliteCombat:    return "E";
            case MapNodeType.Rest:           return "R";
            case MapNodeType.Enchant:        return "E";
            case MapNodeType.Boss:           return "B";
            case MapNodeType.Treasure:       return "T";
            default:                         return "?";
        }
    }

    private Color GetFallbackBgColor(MapNodeType type, bool isCurrent, bool isVisited, bool isNext, bool isFuture)
    {
        Color c;
        switch (type)
        {
            case MapNodeType.Combat:        c = new Color(0.8f, 0.2f, 0.2f); break;
            case MapNodeType.EliteCombat:    c = new Color(1f, 0.4f, 0f); break;
            case MapNodeType.Rest:           c = new Color(0.2f, 0.8f, 0.4f); break;
            case MapNodeType.Enchant:        c = new Color(0.3f, 0.8f, 1f); break;
            case MapNodeType.Boss:           c = new Color(1f, 0f, 0f); break;
            case MapNodeType.Treasure:       c = new Color(1f, 0.85f, 0.2f); break;
            default:                         c = Color.white; break;
        }
        if (isCurrent) return c;
        if (isVisited) return new Color(c.r, c.g, c.b, 0.3f);
        if (isNext) return c;
        if (isFuture) return new Color(c.r, c.g, c.b, 0.45f);
        return new Color(c.r, c.g, c.b, 0.12f);
    }
}
