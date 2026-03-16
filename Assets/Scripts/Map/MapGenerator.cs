using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    // Ödül node'ları — combat olmayan, oyuncuya fayda sağlayan
    private static readonly HashSet<MapNodeType> rewardTypes = new HashSet<MapNodeType>
    {
        MapNodeType.Shop, MapNodeType.PerkSelection, MapNodeType.Rest
    };

    // Risk node'ları — düşmanlı
    private static readonly HashSet<MapNodeType> riskTypes = new HashSet<MapNodeType>
    {
        MapNodeType.Combat, MapNodeType.EliteCombat
    };

    public static MapData Generate(MapLayerData config, int layerIndex)
    {
        MapData map = new MapData();
        map.layerIndex = layerIndex;

        int nextId = 0;
        int totalRows = config.totalRows;

        // ─── Row 0: Start node (always combat) ───
        MapNode startNode = new MapNode
        {
            id = nextId++,
            row = 0,
            column = 0,
            nodeType = MapNodeType.Combat,
            visited = false
        };
        map.nodes.Add(startNode);

        // ─── Rows 1 through totalRows-1: Middle rows ───
        for (int r = 1; r < totalRows; r++)
        {
            // %40 tek node, %50 iki node, %10 üç node
            float roll = Random.value;
            int nodeCount = roll < 0.40f ? 1 : roll < 0.90f ? 2 : 3;
            List<MapNode> rowNodes = new List<MapNode>();

            for (int c = 0; c < nodeCount; c++)
            {
                MapNode node = new MapNode
                {
                    id = nextId++,
                    row = r,
                    column = c,
                    nodeType = MapNodeType.Combat, // Placeholder — aşağıda atanacak
                    visited = false
                };
                rowNodes.Add(node);
                map.nodes.Add(node);
            }

            // Node tiplerini dengeli ata
            AssignRowTypes(rowNodes, config, r, totalRows);
        }

        // ─── Final row: Boss node ───
        MapNode bossNode = new MapNode
        {
            id = nextId++,
            row = totalRows,
            column = 0,
            nodeType = MapNodeType.Boss,
            visited = false
        };
        map.nodes.Add(bossNode);
        map.bossNodeId = bossNode.id;

        // ─── Generate connections (no crossing edges) ───
        GenerateConnections(map, totalRows);

        // ─── Prune orphans ───
        PruneUnreachable(map);

        // ─── Post-processing: patika dengeleme ───
        BalancePaths(map, totalRows);

        // ─── Enforce: elite arkasında ödül garanti ───
        EnforceEliteReward(map);

        // ─── Enforce: ardışık ödül yasağı ───
        EnforceNoConsecutiveRewards(map);

        // ─── Enforce: ardışık shop yasağı ───
        EnforceNoConsecutiveShops(map);

        return map;
    }

    // ═══════════════════════════════════════════════════════
    // ROW TİP ATAMASI — Dengeli dağılım
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Bir row'daki node'lara tip atar.
    /// Kurallar:
    /// - Row 1: hep combat
    /// - Boss öncesi (totalRows-1): combat veya elite
    /// - Rest sadece row 3+
    /// - Bir row'da en fazla 1 ödül node
    /// - Ödül node varsa, geri kalanı combat/elite karışık
    /// </summary>
    private static void AssignRowTypes(List<MapNode> rowNodes, MapLayerData config, int row, int totalRows)
    {
        int count = rowNodes.Count;

        // Row 1: hep combat
        if (row <= 1)
        {
            foreach (var n in rowNodes) n.nodeType = MapNodeType.Combat;
            return;
        }

        // Boss öncesi row: combat veya elite
        if (row == totalRows - 1)
        {
            foreach (var n in rowNodes)
                n.nodeType = Random.value < config.eliteChance * 2.5f ? MapNodeType.EliteCombat : MapNodeType.Combat;
            return;
        }

        // ─── Normal row: en fazla 1 ödül node ───
        // Önce hepsini combat yap
        foreach (var n in rowNodes) n.nodeType = MapNodeType.Combat;

        // Ödül node verilecek mi? (toplam ödül şansı)
        bool canRest = row >= 3;
        float rewardChance = config.shopChance + config.perkChance + (canRest ? config.restChance : 0f);

        if (count >= 2 && Random.value < rewardChance * 1.5f)
        {
            // Rastgele bir slot'a ödül ver
            int rewardSlot = Random.Range(0, count);
            rowNodes[rewardSlot].nodeType = PickRewardType(config, canRest);

            // Kalan slotlardan birine elite şansı
            if (Random.value < config.eliteChance * 2f)
            {
                // Ödül olmayan bir slot seç
                List<int> combatSlots = new List<int>();
                for (int i = 0; i < count; i++)
                    if (i != rewardSlot) combatSlots.Add(i);

                if (combatSlots.Count > 0)
                {
                    int eliteSlot = combatSlots[Random.Range(0, combatSlots.Count)];
                    rowNodes[eliteSlot].nodeType = MapNodeType.EliteCombat;
                }
            }
        }
        else
        {
            // Ödül yok — sadece combat + muhtemel elite
            if (Random.value < config.eliteChance * 2f && count >= 2)
            {
                int eliteSlot = Random.Range(0, count);
                rowNodes[eliteSlot].nodeType = MapNodeType.EliteCombat;
            }
        }
    }

    /// <summary>Ağırlıklı rastgele bir ödül tipi seç.</summary>
    private static MapNodeType PickRewardType(MapLayerData config, bool canRest)
    {
        float total = config.shopChance + config.perkChance + (canRest ? config.restChance : 0f);
        if (total <= 0f) return MapNodeType.PerkSelection;

        float roll = Random.value * total;
        float cum = 0f;

        cum += config.shopChance;
        if (roll < cum) return MapNodeType.Shop;

        cum += config.perkChance;
        if (roll < cum) return MapNodeType.PerkSelection;

        if (canRest) return MapNodeType.Rest;

        return MapNodeType.PerkSelection;
    }

    // ═══════════════════════════════════════════════════════
    // PATİKA DENGELEME — Her yol ayrımında seçim anlamlı olsun
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Her parent node'un child'larını kontrol et.
    /// Eğer birden fazla child varsa ve hepsi aynı "kategori"deyse
    /// (hepsi reward veya hepsi risk), birini değiştir.
    /// Bu sayede yol ayrımlarında her zaman bir risk/ödül dengesi olur.
    /// </summary>
    private static void BalancePaths(MapData map, int totalRows)
    {
        for (int r = 0; r < totalRows; r++)
        {
            List<MapNode> row = map.GetRow(r);
            foreach (var parent in row)
            {
                if (parent.childIds.Count < 2) continue;

                List<MapNode> children = new List<MapNode>();
                foreach (int cid in parent.childIds)
                {
                    MapNode c = map.GetNode(cid);
                    if (c != null) children.Add(c);
                }

                if (children.Count < 2) continue;

                // Boss node'lara dokunma
                if (children.Exists(c => c.nodeType == MapNodeType.Boss)) continue;

                int rewardCount = 0;
                int riskCount = 0;
                foreach (var c in children)
                {
                    if (rewardTypes.Contains(c.nodeType)) rewardCount++;
                    else if (riskTypes.Contains(c.nodeType)) riskCount++;
                }

                // Hepsi ödül? → birini combat'a çevir
                if (rewardCount == children.Count)
                {
                    int idx = Random.Range(0, children.Count);
                    children[idx].nodeType = MapNodeType.Combat;
                }
                // Hepsi risk ve 3+ child? → birini ödüle çevir
                else if (riskCount == children.Count && children.Count >= 3)
                {
                    int idx = Random.Range(0, children.Count);
                    bool canRest = children[idx].row >= 3;
                    children[idx].nodeType = PickRewardType(
                        GetDefaultRewardWeights(), canRest);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ELİTE → ÖDÜL GARANTİSİ
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Elite node'un child'larından en az biri ödül olmalı.
    /// Yoksa, bir combat child'ı ödüle çevir.
    /// Risk aldın → ödül hak ettin.
    /// </summary>
    private static void EnforceEliteReward(MapData map)
    {
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.EliteCombat) continue;
            if (node.childIds.Count == 0) continue;

            // Child'lardan biri zaten ödül mü?
            bool hasReward = false;
            List<MapNode> combatChildren = new List<MapNode>();

            foreach (int cid in node.childIds)
            {
                MapNode child = map.GetNode(cid);
                if (child == null) continue;
                if (rewardTypes.Contains(child.nodeType)) hasReward = true;
                else if (child.nodeType == MapNodeType.Combat) combatChildren.Add(child);
            }

            // Ödül yoksa, bir combat child'ı ödüle çevir
            if (!hasReward && combatChildren.Count > 0)
            {
                MapNode target = combatChildren[Random.Range(0, combatChildren.Count)];
                bool canRest = target.row >= 3;
                target.nodeType = PickRewardType(GetDefaultRewardWeights(), canRest);
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ARDIŞIK ÖDÜL YASAĞI
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Ödül node'unun child'ı da ödülse → child'ı combat'a çevir.
    /// Art arda bedava ödül yok.
    /// </summary>
    private static void EnforceNoConsecutiveRewards(MapData map)
    {
        foreach (var node in map.nodes)
        {
            if (!rewardTypes.Contains(node.nodeType)) continue;

            foreach (int childId in node.childIds)
            {
                MapNode child = map.GetNode(childId);
                if (child != null && rewardTypes.Contains(child.nodeType))
                {
                    child.nodeType = MapNodeType.Combat;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // BAĞLANTILAR
    // ═══════════════════════════════════════════════════════

    private static void GenerateConnections(MapData map, int totalRows)
    {
        for (int r = 0; r < totalRows; r++)
        {
            List<MapNode> currentRow = map.GetRow(r);
            List<MapNode> nextRow = map.GetRow(r + 1);

            if (currentRow.Count == 0 || nextRow.Count == 0) continue;

            HashSet<int> connectedChildren = new HashSet<int>();

            for (int i = 0; i < currentRow.Count; i++)
            {
                MapNode node = currentRow[i];

                // En yakın child'ı bul — sadece komşu sütunlara bağlan
                int bestCol = FindClosestColumn(i, currentRow.Count, nextRow.Count);
                bestCol = Mathf.Clamp(bestCol, 0, nextRow.Count - 1);

                node.childIds.Add(nextRow[bestCol].id);
                connectedChildren.Add(nextRow[bestCol].id);

                // 60% şansla ikinci bağlantı (sadece bitişik sütuna)
                if (Random.value < 0.6f && nextRow.Count > 1)
                {
                    int secondaryCol = bestCol + (Random.value < 0.5f ? -1 : 1);
                    secondaryCol = Mathf.Clamp(secondaryCol, 0, nextRow.Count - 1);

                    if (secondaryCol != bestCol && !WouldCross(currentRow, node, nextRow, secondaryCol))
                    {
                        node.childIds.Add(nextRow[secondaryCol].id);
                        connectedChildren.Add(nextRow[secondaryCol].id);
                    }
                }
            }

            // Her next-row node'un en az 1 parent'ı olsun — en yakın parent'a bağla
            for (int j = 0; j < nextRow.Count; j++)
            {
                if (!connectedChildren.Contains(nextRow[j].id))
                {
                    int closestParent = FindClosestColumn(j, nextRow.Count, currentRow.Count);
                    closestParent = Mathf.Clamp(closestParent, 0, currentRow.Count - 1);
                    currentRow[closestParent].childIds.Add(nextRow[j].id);
                }
            }
        }
    }

    /// <summary>
    /// Bir node'un sütun pozisyonuna göre hedef row'daki en yakın sütunu hesaplar.
    /// 3→2 veya 2→3 geçişlerinde uzak çapraz bağlantıları engeller.
    /// </summary>
    private static int FindClosestColumn(int sourceCol, int sourceCount, int targetCount)
    {
        if (sourceCount <= 1 || targetCount <= 1) return 0;

        // Kaynak node'un normalize pozisyonu (0.0 = en sol, 1.0 = en sağ)
        float sourcePos = (float)sourceCol / (sourceCount - 1);

        // Hedef row'daki her sütunun mesafesini hesapla, en yakını seç
        int bestCol = 0;
        float bestDist = float.MaxValue;
        for (int t = 0; t < targetCount; t++)
        {
            float targetPos = (float)t / (targetCount - 1);
            float dist = Mathf.Abs(sourcePos - targetPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCol = t;
            }
        }
        return bestCol;
    }

    private static bool WouldCross(List<MapNode> currentRow, MapNode fromNode, List<MapNode> nextRow, int targetCol)
    {
        int fromIdx = currentRow.IndexOf(fromNode);

        for (int i = 0; i < currentRow.Count; i++)
        {
            if (i == fromIdx) continue;
            MapNode other = currentRow[i];

            foreach (int childId in other.childIds)
            {
                MapNode child = null;
                foreach (var n in nextRow)
                {
                    if (n.id == childId) { child = n; break; }
                }
                if (child == null) continue;

                int otherCol = child.column;
                if (fromIdx < i && targetCol > otherCol) return true;
                if (fromIdx > i && targetCol < otherCol) return true;
            }
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════
    // YARDIMCI
    // ═══════════════════════════════════════════════════════

    private static void PruneUnreachable(MapData map)
    {
        HashSet<int> reachable = new HashSet<int>();
        Queue<int> queue = new Queue<int>();

        queue.Enqueue(0);
        reachable.Add(0);

        while (queue.Count > 0)
        {
            int nodeId = queue.Dequeue();
            MapNode node = map.GetNode(nodeId);
            if (node == null) continue;

            foreach (int childId in node.childIds)
            {
                if (!reachable.Contains(childId))
                {
                    reachable.Add(childId);
                    queue.Enqueue(childId);
                }
            }
        }

        map.nodes.RemoveAll(n => !reachable.Contains(n.id));

        foreach (var node in map.nodes)
        {
            node.childIds.RemoveAll(id => !reachable.Contains(id));
        }
    }

    private static void EnforceNoConsecutiveShops(MapData map)
    {
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.Shop) continue;

            foreach (int childId in node.childIds)
            {
                MapNode child = map.GetNode(childId);
                if (child != null && child.nodeType == MapNodeType.Shop)
                {
                    child.nodeType = MapNodeType.Combat;
                }
            }
        }
    }

    /// <summary>Varsayılan ödül ağırlıkları (config olmadan kullanılır).</summary>
    private static MapLayerData GetDefaultRewardWeights()
    {
        MapLayerData d = ScriptableObject.CreateInstance<MapLayerData>();
        d.shopChance = 0.30f;
        d.perkChance = 0.40f;
        d.restChance = 0.30f;
        return d;
    }
}
