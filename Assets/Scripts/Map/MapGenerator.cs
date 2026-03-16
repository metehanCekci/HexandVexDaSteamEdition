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
                    nodeType = MapNodeType.Combat, // Placeholder
                    visited = false
                };
                rowNodes.Add(node);
                map.nodes.Add(node);
            }
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

        // ─── PATH-AWARE TİP ATAMASI ───
        // Bağlantılar belli olduktan sonra, her patikada ritmi garanti et
        AssignTypesPathAware(map, config, totalRows);

        return map;
    }

    // ═══════════════════════════════════════════════════════
    // PATH-AWARE TİP ATAMASI
    // Mantık: Bağlantılar hazır. Şimdi her node'a tip ata,
    // ama bunu yaparken tüm olası patikalarda
    // "max 2 ardışık savaş → ödül gelecek" ritmine uy.
    // ═══════════════════════════════════════════════════════

    private static void AssignTypesPathAware(MapData map, MapLayerData config, int totalRows)
    {
        // Row 0 = combat (zaten), Boss = boss (zaten). Geri kalanı atayacağız.
        // Row 1 = hep combat (ilk adım her zaman savaş)
        // Boss öncesi row = combat veya elite (boss hazırlığı)

        // ─── Adım 1: Sabit kurallar ───
        foreach (var node in map.nodes)
        {
            if (node.nodeType == MapNodeType.Boss) continue;
            if (node.row == 0) { node.nodeType = MapNodeType.Combat; continue; }
            if (node.row <= 1) { node.nodeType = MapNodeType.Combat; continue; }
            if (node.row == totalRows - 1)
            {
                node.nodeType = Random.value < config.eliteChance * 2.5f
                    ? MapNodeType.EliteCombat : MapNodeType.Combat;
                continue;
            }
            // Geri kalanı şimdilik combat — aşağıda değiştirilecek
            node.nodeType = MapNodeType.Combat;
        }

        // ─── Adım 2: Her parent'ın tüm olası patikalarını düşünerek ödül yerleştir ───
        // Row bazlı ilerle (row 2'den başla — row 0,1 combat).
        // Her row'da, o row'a gelen patikalardaki "ardışık savaş sayısı"na bak.
        // Eğer bir node'a gelen herhangi bir patikada 2+ ardışık savaş varsa → ödül ver.

        // Her node için: "bu node'a gelen en uzun ardışık savaş streak'i"
        Dictionary<int, int> maxCombatStreak = new Dictionary<int, int>();

        // Row 0 start = 1 combat streak (kendisi combat)
        foreach (var node in map.nodes)
        {
            if (node.row == 0)
                maxCombatStreak[node.id] = 1;
        }

        // Row bazlı ilerle
        for (int r = 1; r <= totalRows; r++)
        {
            List<MapNode> rowNodes = map.GetRow(r);
            if (rowNodes.Count == 0) continue;

            // Boss row veya row 1 → streak hesapla ama tip değiştirme
            // Row 2+ → ödül yerleştirme mantığı

            // Her node'un parent streak'lerini topla
            Dictionary<int, int> incomingMaxStreak = new Dictionary<int, int>();
            foreach (var node in rowNodes)
            {
                incomingMaxStreak[node.id] = 0;
            }

            // Parent'lardan streak propagate et
            List<MapNode> prevRow = map.GetRow(r - 1);
            foreach (var parent in prevRow)
            {
                int parentStreak = maxCombatStreak.ContainsKey(parent.id) ? maxCombatStreak[parent.id] : 0;

                foreach (int childId in parent.childIds)
                {
                    MapNode child = map.GetNode(childId);
                    if (child == null || child.row != r) continue;

                    if (parentStreak > incomingMaxStreak[childId])
                        incomingMaxStreak[childId] = parentStreak;
                }
            }

            // ─── Bu row'daki node'lara tip ata ───
            if (r >= 2 && r < totalRows)
            {
                AssignRowTypesPathAware(rowNodes, config, r, totalRows, incomingMaxStreak);
            }

            // ─── Streak'leri güncelle ───
            foreach (var node in rowNodes)
            {
                int incoming = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;

                if (riskTypes.Contains(node.nodeType))
                    maxCombatStreak[node.id] = incoming + 1;
                else if (node.nodeType == MapNodeType.Boss)
                    maxCombatStreak[node.id] = 0; // boss farklı
                else
                    maxCombatStreak[node.id] = 0; // ödül node → streak sıfırlanır
            }
        }

        // ─── Adım 3: Post-processing kuralları ───

        // Yol ayrımlarında farklı ödül tipleri garanti et
        EnforceDiverseRewards(map, config, totalRows);

        // Elite arkasında ödül garanti
        EnforceEliteReward(map, config);

        // Ardışık ödül yasağı (ödül → ödül olmasın)
        EnforceNoConsecutiveRewards(map);

        // Ardışık shop yasağı
        EnforceNoConsecutiveShops(map);
    }

    /// <summary>
    /// Bir row'daki node'lara tip atar — parent'lardan gelen savaş streak'ini dikkate alarak.
    /// Kural: Bir node'a 2+ ardışık savaştan sonra geliniyorsa → ödül olmalı.
    /// </summary>
    private static void AssignRowTypesPathAware(
        List<MapNode> rowNodes, MapLayerData config,
        int row, int totalRows,
        Dictionary<int, int> incomingMaxStreak)
    {
        int count = rowNodes.Count;
        bool canRest = row >= 3;

        // Boss öncesi → sadece combat/elite (zaten atandı ama güvenlik)
        if (row == totalRows - 1)
        {
            foreach (var n in rowNodes)
                n.nodeType = Random.value < config.eliteChance * 2.5f
                    ? MapNodeType.EliteCombat : MapNodeType.Combat;
            return;
        }

        // ─── Önce zorunlu ödülleri belirle ───
        // 2+ ardışık savaş streak'i olan node'lar ödül OLMALI
        List<MapNode> mustReward = new List<MapNode>();
        List<MapNode> canBeAnything = new List<MapNode>();

        foreach (var node in rowNodes)
        {
            int streak = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;
            if (streak >= 2)
                mustReward.Add(node);
            else
                canBeAnything.Add(node);
        }

        // ─── Zorunlu ödülleri ata ───
        // Aynı row'da farklı ödül tipleri ver
        List<MapNodeType> usedRewardTypes = new List<MapNodeType>();
        foreach (var node in mustReward)
        {
            node.nodeType = PickDiverseRewardType(config, canRest, usedRewardTypes);
            usedRewardTypes.Add(node.nodeType);
        }

        // ─── Geri kalan node'lar ───
        foreach (var node in canBeAnything)
        {
            int streak = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;

            // Streak 1 ise (1 savaş yapılmış): %30 ödül şansı ver (erken ödül bazen güzel)
            // Streak 0 ise (yeni başlangıç veya ödülden sonra): combat veya elite
            if (streak == 1 && count >= 2 && Random.value < 0.25f)
            {
                node.nodeType = PickDiverseRewardType(config, canRest, usedRewardTypes);
                usedRewardTypes.Add(node.nodeType);
            }
            else
            {
                // Combat veya Elite
                node.nodeType = Random.value < config.eliteChance * 2f
                    ? MapNodeType.EliteCombat : MapNodeType.Combat;
            }
        }
    }

    /// <summary>
    /// Daha önce bu row'da kullanılmamış bir ödül tipi seç.
    /// Mümkünse Shop/Perk/Rest arasında çeşitlilik sağla.
    /// </summary>
    private static MapNodeType PickDiverseRewardType(
        MapLayerData config, bool canRest, List<MapNodeType> usedTypes)
    {
        // Kullanılabilir ödül tipleri ve ağırlıkları
        List<MapNodeType> candidates = new List<MapNodeType>();
        List<float> weights = new List<float>();

        if (!usedTypes.Contains(MapNodeType.Shop))
        {
            candidates.Add(MapNodeType.Shop);
            weights.Add(config.shopChance);
        }
        if (!usedTypes.Contains(MapNodeType.PerkSelection))
        {
            candidates.Add(MapNodeType.PerkSelection);
            weights.Add(config.perkChance);
        }
        if (canRest && !usedTypes.Contains(MapNodeType.Rest))
        {
            candidates.Add(MapNodeType.Rest);
            weights.Add(config.restChance);
        }

        // Hepsi kullanılmışsa fallback — tekrar seçebilir
        if (candidates.Count == 0)
        {
            candidates.Add(MapNodeType.Shop);
            weights.Add(config.shopChance);
            candidates.Add(MapNodeType.PerkSelection);
            weights.Add(config.perkChance);
            if (canRest)
            {
                candidates.Add(MapNodeType.Rest);
                weights.Add(config.restChance);
            }
        }

        // Ağırlıklı random
        float total = 0f;
        foreach (float w in weights) total += w;
        if (total <= 0f) return MapNodeType.PerkSelection;

        float roll = Random.value * total;
        float cum = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cum += weights[i];
            if (roll < cum) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    // ═══════════════════════════════════════════════════════
    // YOL AYRIMINDA FARKLI ÖDÜL TİPLERİ
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Bir parent'ın birden fazla child'ı ödülse → farklı ödül tipleri olsun.
    /// Sola Shop, sağa Perk gibi — oyuncuya gerçek bir seçim sun.
    /// </summary>
    private static void EnforceDiverseRewards(MapData map, MapLayerData config, int totalRows)
    {
        for (int r = 0; r < totalRows; r++)
        {
            List<MapNode> row = map.GetRow(r);
            foreach (var parent in row)
            {
                if (parent.childIds.Count < 2) continue;

                List<MapNode> rewardChildren = new List<MapNode>();
                foreach (int cid in parent.childIds)
                {
                    MapNode c = map.GetNode(cid);
                    if (c != null && rewardTypes.Contains(c.nodeType))
                        rewardChildren.Add(c);
                }

                if (rewardChildren.Count < 2) continue;

                // Aynı tipteki ödülleri farklılaştır
                HashSet<MapNodeType> usedHere = new HashSet<MapNodeType>();
                foreach (var child in rewardChildren)
                {
                    if (usedHere.Contains(child.nodeType))
                    {
                        // Bu tip zaten var — farklı bir şey seç
                        List<MapNodeType> usedList = new List<MapNodeType>(usedHere);
                        bool canRest = child.row >= 3;
                        child.nodeType = PickDiverseRewardType(config, canRest, usedList);
                    }
                    usedHere.Add(child.nodeType);
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
    /// </summary>
    private static void EnforceEliteReward(MapData map, MapLayerData config)
    {
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.EliteCombat) continue;
            if (node.childIds.Count == 0) continue;

            bool hasReward = false;
            List<MapNode> combatChildren = new List<MapNode>();

            foreach (int cid in node.childIds)
            {
                MapNode child = map.GetNode(cid);
                if (child == null) continue;
                if (rewardTypes.Contains(child.nodeType)) hasReward = true;
                else if (child.nodeType == MapNodeType.Combat) combatChildren.Add(child);
            }

            if (!hasReward && combatChildren.Count > 0)
            {
                MapNode target = combatChildren[Random.Range(0, combatChildren.Count)];
                bool canRest = target.row >= 3;
                target.nodeType = PickDiverseRewardType(config, canRest, new List<MapNodeType>());
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ARDIŞIK ÖDÜL YASAĞI
    // ═══════════════════════════════════════════════════════

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

            // Her next-row node'un en az 1 parent'ı olsun
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

    private static int FindClosestColumn(int sourceCol, int sourceCount, int targetCount)
    {
        if (sourceCount <= 1 || targetCount <= 1) return 0;

        float sourcePos = (float)sourceCol / (sourceCount - 1);

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
}
