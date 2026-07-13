using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    // Ödül node'ları — combat olmayan, oyuncuya fayda sağlayan
    private static readonly HashSet<MapNodeType> rewardTypes = new HashSet<MapNodeType>
    {
        MapNodeType.Rest, MapNodeType.Enchant, MapNodeType.Sacrifice
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
        // Kural: Arka arkaya max N çoklu (2-3 node) row olabilir,
        // sonra zorunlu 1 node row gelir. Kısa-uzun ritmi.
        int multiRowStreak = 0; // Ardışık çoklu row sayacı
        int maxStreak = config.maxMultiRowStreak; // Config'den (default 3)
        float singleChance = config.singleNodeChance; // Config'den (default 0.30)

        for (int r = 1; r < totalRows; r++)
        {
            int nodeCount;

            if (multiRowStreak >= maxStreak)
            {
                // N ardışık çoklu row'dan sonra → zorunlu tek node
                nodeCount = 1;
                multiRowStreak = 0;
            }
            else
            {
                // singleChance tek node, geri kalan 2 veya 3 node
                float roll = Random.value;
                float threeThreshold = 1f - config.threeNodeChance;
                nodeCount = roll < singleChance ? 1 : roll < threeThreshold ? 2 : 3;

                if (nodeCount == 1)
                    multiRowStreak = 0;
                else
                    multiRowStreak++;
            }

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

        // ─── Her oda tipinden en az 1 garanti ───
        EnforceMinimumRoomTypes(map, totalRows, config);

        // ─── SON GÜVENLİK: Ardışık ödül yasağı (tüm post-processing sonrası) ───
        EnforceNoConsecutiveRewards(map);

        // ─── SON GÜVENLİK: Layer başına en az 1 Rest (Campfire) garanti ───
        EnforceMinimumRest(map, totalRows);

        // ─── SON GÜVENLİK: Layer başına en az 1 Enchant garanti ───
        EnforceMinimumEnchant(map, totalRows);

        // ─── SON GÜVENLİK: Arka arkaya ASLA aynı node tipi gelmesin ───
        EnforceNoConsecutiveSameType(map, config);

        // ─── ELITE + TREASURE ÇİFTİ INJECT ───
        InjectEliteTreasurePairs(map, totalRows);

        // ─── EN SON: Aynı derinlikteki aynı tipteki node'ları birleştir ───
        MergeSameTypeAtSameDepth(map, totalRows);

        return map;
    }

    // ═══════════════════════════════════════════════════════
    // ELITE + TREASURE ÇİFTİ (OPSİYONEL BRANCH)
    // Çoklu node row'da (2+) bir Combat node'u Elite yapar.
    // Elite'den sonra Treasure node eklenir.
    // Diğer node'lar normal kalır → oyuncu Elite'den geçmeden
    // ilerleyebilir. Elite + Treasure her zaman opsiyonel yol.
    // ═══════════════════════════════════════════════════════

    private static void InjectEliteTreasurePairs(MapData map, int totalRows)
    {
        // Uygun çoklu-node row'ları bul (2+ node, row 3+, boss'tan en az 2 row uzak, en az 1 Combat)
        List<int> candidates = new List<int>();
        for (int r = 3; r <= totalRows - 3; r++)
        {
            List<MapNode> row = map.GetRow(r);
            if (row.Count < 2) continue; // Tek node row'da Elite OLMAZ — zorunlu olur

            // En az 1 Combat node olmalı (Elite'e dönüşecek)
            bool hasCombat = false;
            foreach (var n in row)
            {
                if (n.nodeType == MapNodeType.Combat) { hasCombat = true; break; }
            }
            if (!hasCombat) continue;

            // Row'da Elite olmayan en az 1 alternatif yol kalmalı
            // (2+ node zaten bunu garanti eder — biri Elite olunca diğeri kalır)
            candidates.Add(r);
        }

        if (candidates.Count == 0) return;

        // Ortaya yakın olanı seç — daha iyi pacing
        candidates.Sort((a, b) =>
            Mathf.Abs(a - totalRows / 2).CompareTo(Mathf.Abs(b - totalRows / 2)));
        int eliteRow = candidates[0];

        // Row'daki Combat node'lardan birini Elite yap
        List<MapNode> eliteRowNodes = map.GetRow(eliteRow);
        MapNode eliteNode = null;
        foreach (var n in eliteRowNodes)
        {
            if (n.nodeType == MapNodeType.Combat) { eliteNode = n; break; }
        }
        if (eliteNode == null) return; // Güvenlik
        eliteNode.nodeType = MapNodeType.EliteCombat;

        // ─── Treasure node oluştur ───
        // Elite'nin mevcut child'larını Treasure'a aktar,
        // Elite → sadece Treasure, Treasure → eski child'lar.
        // Diğer node'ların bağlantıları değişmez (bypass yolu korunur).

        List<int> eliteOldChildren = new List<int>(eliteNode.childIds);
        eliteNode.childIds.Clear();

        int treasureRow = eliteRow + 1;
        int newId = map.nodes.Count;

        MapNode treasureNode = new MapNode
        {
            id = newId,
            row = treasureRow,
            column = eliteNode.column, // Elite ile aynı sütunda görsel tutarlılık
            nodeType = MapNodeType.Treasure,
            visited = false
        };

        // Treasure → Elite'nin eski child'ları
        treasureNode.childIds.AddRange(eliteOldChildren);

        // Elite → sadece Treasure
        eliteNode.childIds.Add(treasureNode.id);

        map.nodes.Add(treasureNode);
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
            // Row 1: combat — ama çoklu row'daysa EnforceNoDuplicatesInRow farklılaştıracak
            if (node.row == 1) { node.nodeType = MapNodeType.Combat; continue; }
            // Row 2: artık shop yok, combat olsun
            if (node.row == 2) { node.nodeType = MapNodeType.Combat; continue; }
            if (node.row == totalRows - 1)
            {
                node.nodeType = MapNodeType.Combat;
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
            if (r >= 3 && r < totalRows)
            {
                AssignRowTypesPathAware(map, rowNodes, config, r, totalRows, incomingMaxStreak);
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

        // Ardışık ödül yasağı (ödül → ödül olmasın)
        EnforceNoConsecutiveRewards(map);

        // ─── SON GÜVENLİK: Çoklu row'da aynı tip ASLA olamaz ───
        // Post-processing kuralları tip değiştirebilir, bu yüzden en sonda tekrar kontrol
        EnforceNoDuplicatesInRow(map, config, totalRows);

        // ─── SON GÜVENLİK 2: Ardışık ödül yasağını tekrar uygula ───
        // EnforceNoDuplicatesInRow ödül eklemiş olabilir
        EnforceNoConsecutiveRewards(map);
    }

    /// <summary>
    /// Bir row'daki node'lara tip atar — parent'lardan gelen savaş streak'ini dikkate alarak.
    /// EN ÖNEMLİ KURAL: Çoklu row'da node'lar ASLA aynı tip olamaz.
    /// Seçimin her zaman anlamı olmalı.
    /// </summary>
    private static void AssignRowTypesPathAware(
        MapData map, List<MapNode> rowNodes, MapLayerData config,
        int row, int totalRows,
        Dictionary<int, int> incomingMaxStreak)
    {
        int count = rowNodes.Count;
        bool canRest = true;

        // Boss öncesi → hep combat (elite artık ayrı inject ediliyor)
        if (row == totalRows - 1)
        {
            for (int i = 0; i < count; i++)
                rowNodes[i].nodeType = MapNodeType.Combat;
            return;
        }

        // ─── Tek node → basit karar ───
        // TEK NODE ROW'DA ASLA ELITE YOK — oyuncunun alternatif yolu olmalı
        if (count == 1)
        {
            bool anyParentReward = HasRewardParent(map, rowNodes[0]);

            int streak = incomingMaxStreak.ContainsKey(rowNodes[0].id) ? incomingMaxStreak[rowNodes[0].id] : 0;
            if (anyParentReward)
            {
                rowNodes[0].nodeType = MapNodeType.Combat;
            }
            else if (streak >= 2)
            {
                rowNodes[0].nodeType = PickDiverseRewardType(config, canRest, new List<MapNodeType>());
            }
            else if (streak == 1 && Random.value < 0.45f)
            {
                rowNodes[0].nodeType = PickDiverseRewardType(config, canRest, new List<MapNodeType>());
            }
            else
            {
                rowNodes[0].nodeType = MapNodeType.Combat;
            }
            return;
        }

        // ═══════════════════════════════════════════════════════
        // ÇOKLU NODE — HER NODE FARKLI TİP OLMALI
        // Oyuncuya her zaman anlamlı bir seçim sun.
        // ═══════════════════════════════════════════════════════

        // Streak'e göre kaç tanesinin ödül olması gerektiğini hesapla
        int mustRewardCount = 0;
        foreach (var node in rowNodes)
        {
            int streak = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;
            if (streak >= 2) mustRewardCount++;
        }

        // Tüm node'lar ödül olmak zorundaysa → farklı ödül tipleri ver
        if (mustRewardCount >= count)
        {
            List<MapNodeType> used = new List<MapNodeType>();
            foreach (var node in rowNodes)
            {
                node.nodeType = PickDiverseRewardType(config, canRest, used);
                used.Add(node.nodeType);
            }
            return;
        }

        // Karışık row: bazıları ödül, bazıları risk
        // Ama hiçbiri aynı tip olmayacak!
        List<MapNodeType> assignedTypes = new List<MapNodeType>();

        // Önce zorunlu ödülleri ata
        foreach (var node in rowNodes)
        {
            int streak = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;
            bool parentReward = HasRewardParent(map, node);
            if (streak >= 2 && !parentReward)
            {
                node.nodeType = PickDiverseRewardType(config, canRest, assignedTypes);
                assignedTypes.Add(node.nodeType);
            }
        }

        // Geri kalanları ata — zaten atanmış tiplerden FARKLI olacak şekilde
        foreach (var node in rowNodes)
        {
            int streak = incomingMaxStreak.ContainsKey(node.id) ? incomingMaxStreak[node.id] : 0;
            if (streak >= 2 && !HasRewardParent(map, node)) continue; // Zaten atandı

            bool parentReward = HasRewardParent(map, node);

            // Parent ödülse → kesinlikle combat
            if (parentReward)
            {
                node.nodeType = MapNodeType.Combat;
                assignedTypes.Add(node.nodeType);
            }
            // Ödül verilmemiş node'a ne verelim?
            // Streak 1 + çoklu row → %45 şansla ödül (ama farklı tip)
            else if (streak == 1 && Random.value < 0.45f && !AllRewardsUsed(assignedTypes, canRest))
            {
                node.nodeType = PickDiverseRewardType(config, canRest, assignedTypes);
                assignedTypes.Add(node.nodeType);
            }
            else
            {
                node.nodeType = MapNodeType.Combat;
                assignedTypes.Add(node.nodeType);
            }
        }
    }

    /// <summary>Bu node'un parent'larından herhangi biri ödül mü?</summary>
    private static bool HasRewardParent(MapData map, MapNode node)
    {
        foreach (var pNode in map.nodes)
        {
            if (pNode.childIds.Contains(node.id) && rewardTypes.Contains(pNode.nodeType))
                return true;
        }
        return false;
    }

    /// <summary>Tüm ödül tipleri kullanıldı mı?</summary>
    private static bool AllRewardsUsed(List<MapNodeType> used, bool canRest)
    {
        if (canRest && !used.Contains(MapNodeType.Rest)) return false;
        if (!used.Contains(MapNodeType.Enchant)) return false;
        return true;
    }

    /// <summary>
    /// Daha önce bu row'da kullanılmamış bir ödül tipi seç.
    /// Mümkünse Rest/Enchant/Sacrifice arasında çeşitlilik sağla.
    /// </summary>
    private static MapNodeType PickDiverseRewardType(
        MapLayerData config, bool canRest, List<MapNodeType> usedTypes)
    {
        // Kullanılabilir ödül tipleri ve ağırlıkları
        List<MapNodeType> candidates = new List<MapNodeType>();
        List<float> weights = new List<float>();

        if (canRest && !usedTypes.Contains(MapNodeType.Rest))
        {
            candidates.Add(MapNodeType.Rest);
            weights.Add(config.restChance);
        }
        if (!usedTypes.Contains(MapNodeType.Enchant))
        {
            candidates.Add(MapNodeType.Enchant);
            weights.Add(config.enchantChance > 0f ? config.enchantChance : 0.10f);
        }
        if (!usedTypes.Contains(MapNodeType.Sacrifice))
        {
            candidates.Add(MapNodeType.Sacrifice);
            weights.Add(config.sacrificeChance > 0f ? config.sacrificeChance : 0.08f);
        }
        // Hepsi kullanılmışsa fallback — tekrar seçebilir
        if (candidates.Count == 0)
        {
            if (canRest)
            {
                candidates.Add(MapNodeType.Rest);
                weights.Add(config.restChance);
            }
            candidates.Add(MapNodeType.Enchant);
            weights.Add(config.enchantChance > 0f ? config.enchantChance : 0.10f);
            candidates.Add(MapNodeType.Sacrifice);
            weights.Add(config.sacrificeChance > 0f ? config.sacrificeChance : 0.08f);
        }

        // Ağırlıklı random
        float total = 0f;
        foreach (float w in weights) total += w;
        if (total <= 0f) return MapNodeType.Rest;

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
    /// Farklı ödül tipleri sunarak oyuncuya gerçek bir seçim sun.
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
    // AYNI DERİNLİKTE AYNI TİP → BİRLEŞTİR
    // ═══════════════════════════════════════════════════════

    private static void MergeSameTypeAtSameDepth(MapData map, int totalRows)
    {
        for (int r = 1; r < totalRows; r++)
        {
            List<MapNode> rowNodes = map.GetRow(r);
            if (rowNodes.Count < 2) continue;

            // Aynı tipteki node'ları grupla
            Dictionary<MapNodeType, List<MapNode>> typeGroups = new Dictionary<MapNodeType, List<MapNode>>();
            foreach (var node in rowNodes)
            {
                if (!typeGroups.ContainsKey(node.nodeType))
                    typeGroups[node.nodeType] = new List<MapNode>();
                typeGroups[node.nodeType].Add(node);
            }

            // Birleştirilecekleri topla (iteration sırasında list bozmamak için)
            List<System.Tuple<MapNode, MapNode>> mergeList = new List<System.Tuple<MapNode, MapNode>>();
            foreach (var kvp in typeGroups)
            {
                List<MapNode> group = kvp.Value;
                if (group.Count < 2) continue;
                MapNode keeper = group[0];
                for (int i = 1; i < group.Count; i++)
                    mergeList.Add(new System.Tuple<MapNode, MapNode>(keeper, group[i]));
            }

            foreach (var merge in mergeList)
            {
                MapNode keeper = merge.Item1;
                MapNode removed = merge.Item2;
                if (!map.nodes.Contains(removed)) continue;

                // removed'un child'larını keeper'a aktar
                foreach (int childId in removed.childIds)
                {
                    if (childId != keeper.id && !keeper.childIds.Contains(childId))
                        keeper.childIds.Add(childId);
                }

                // removed'a bağlı parent'ları keeper'a yönlendir
                for (int p = 0; p < map.nodes.Count; p++)
                {
                    MapNode pNode = map.nodes[p];
                    if (pNode == removed) continue;
                    int idx = pNode.childIds.IndexOf(removed.id);
                    if (idx >= 0)
                    {
                        pNode.childIds.RemoveAt(idx);
                        if (!pNode.childIds.Contains(keeper.id))
                            pNode.childIds.Add(keeper.id);
                    }
                }

                map.nodes.Remove(removed);
            }

            // Column indekslerini yeniden sırala
            List<MapNode> remaining = map.GetRow(r);
            remaining.Sort((a, b) => a.column.CompareTo(b.column));
            for (int c = 0; c < remaining.Count; c++)
                remaining[c].column = c;
        }

        // ─── ID Reindex ───
        // GetNode(id) id'yi list index olarak kullanıyor.
        // Node silindikten sonra id'ler ve childIds'ler yeniden eşlenmeli.
        Dictionary<int, int> idRemap = new Dictionary<int, int>();
        for (int i = 0; i < map.nodes.Count; i++)
        {
            idRemap[map.nodes[i].id] = i;
            map.nodes[i].id = i;
        }

        // childIds'leri yeni id'lerle güncelle
        foreach (var node in map.nodes)
        {
            for (int c = 0; c < node.childIds.Count; c++)
            {
                if (idRemap.ContainsKey(node.childIds[c]))
                    node.childIds[c] = idRemap[node.childIds[c]];
            }
        }

        // bossNodeId güncelle
        if (idRemap.ContainsKey(map.bossNodeId))
            map.bossNodeId = idRemap[map.bossNodeId];
    }

    // ═══════════════════════════════════════════════════════
    // ARDIŞIK AYNI TİP YASAĞI (Combat dahil her şey)
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Parent → child bağlantısında aynı nodeType varsa child'ı farklı bir tipe çevirir.
    /// Boss ve row 0 hariç. Combat arka arkaya gelirse birini EliteCombat veya reward yapar.
    /// </summary>
    private static void EnforceNoConsecutiveSameType(MapData map, MapLayerData config)
    {
        bool canRest = true;

        foreach (var parent in map.nodes)
        {
            if (parent.nodeType == MapNodeType.Boss) continue;

            foreach (int childId in parent.childIds)
            {
                MapNode child = map.GetNode(childId);
                if (child == null || child.nodeType == MapNodeType.Boss) continue;
                if (child.row <= 1) continue; // İlk 2 row'a dokunma

                if (child.nodeType != parent.nodeType) continue;

                // Aynı tip! Alternatif bul
                if (riskTypes.Contains(child.nodeType))
                {
                    // Ardışık combat → reward'a çevir
                    child.nodeType = PickDiverseRewardType(config, canRest, new List<MapNodeType> { parent.nodeType });
                }
                else
                {
                    // Reward tiplerinde: farklı bir reward seç
                    List<MapNodeType> exclude = new List<MapNodeType> { parent.nodeType };
                    child.nodeType = PickDiverseRewardType(config, canRest, exclude);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // SON GÜVENLİK — AYNI ROW'DA AYNI TİP YASAK
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Post-processing sonrası çoklu row'larda aynı tip oluşmuş olabilir.
    /// Bu geçiş en sonda çalışır ve aynı tipteki node'ları zorla değiştirir.
    /// Row bağlamına göre uygun alternatif seçer.
    /// </summary>
    private static void EnforceNoDuplicatesInRow(MapData map, MapLayerData config, int totalRows)
    {
        for (int r = 0; r <= totalRows; r++)
        {
            List<MapNode> rowNodes = map.GetRow(r);
            if (rowNodes.Count < 2) continue;

            // Boss row'a ve row 0'a dokunma (tek node zaten)
            if (rowNodes.Exists(n => n.nodeType == MapNodeType.Boss)) continue;

            HashSet<MapNodeType> seen = new HashSet<MapNodeType>();
            foreach (var node in rowNodes)
            {
                if (seen.Contains(node.nodeType))
                {
                    // Duplikat! Row'a göre uygun farklı bir tip seç.
                    bool canRest = node.row >= 3;
                    bool earlyRow = node.row <= 1; // Row 0-1: sadece combat/elite
                    bool preBoss = node.row == totalRows - 1;

                    if (earlyRow || preBoss)
                    {
                        // Erken row / boss öncesi: Combat fallback
                        node.nodeType = MapNodeType.Combat;
                    }
                    else
                    {
                        // Normal row: ödül veya combat — kullanılmamış olanı seç
                        bool parentIsReward = false;
                        foreach (var pNode in map.nodes)
                        {
                            if (pNode.childIds.Contains(node.id) && rewardTypes.Contains(pNode.nodeType))
                            { parentIsReward = true; break; }
                        }

                        if (parentIsReward)
                        {
                            node.nodeType = MapNodeType.Combat;
                        }
                        else
                        {
                            if (canRest && !seen.Contains(MapNodeType.Rest))
                                node.nodeType = MapNodeType.Rest;
                            else if (!seen.Contains(MapNodeType.Enchant))
                                node.nodeType = MapNodeType.Enchant;
                            else
                                node.nodeType = MapNodeType.Combat;
                        }
                    }
                }
                seen.Add(node.nodeType);
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

            // ─── Tek node → tüm next row'a bağlan ───
            if (currentRow.Count == 1)
            {
                MapNode node = currentRow[0];
                for (int j = 0; j < nextRow.Count; j++)
                {
                    node.childIds.Add(nextRow[j].id);
                    connectedChildren.Add(nextRow[j].id);
                }
            }
            // ─── Next row tek node → tüm current row'dan bağlantı ───
            else if (nextRow.Count == 1)
            {
                for (int i = 0; i < currentRow.Count; i++)
                {
                    currentRow[i].childIds.Add(nextRow[0].id);
                    connectedChildren.Add(nextRow[0].id);
                }
            }
            // ─── Aynı sayıda node → 1:1 bağlantı + ortadaki bitişiğe de bağlanabilir ───
            else if (currentRow.Count == nextRow.Count)
            {
                for (int i = 0; i < currentRow.Count; i++)
                {
                    // Kendi karşısına bağlan
                    currentRow[i].childIds.Add(nextRow[i].id);
                    connectedChildren.Add(nextRow[i].id);

                    // Ortadaki node (3'lü row'da col 1) bitişiğe de bağlanabilir
                    if (currentRow.Count == 3 && i == 1)
                    {
                        // Orta node: %50 sola, %50 sağa ek bağlantı
                        int extra = Random.value < 0.5f ? 0 : 2;
                        if (!currentRow[i].childIds.Contains(nextRow[extra].id))
                        {
                            currentRow[i].childIds.Add(nextRow[extra].id);
                            connectedChildren.Add(nextRow[extra].id);
                        }
                    }
                    else if (currentRow.Count == 2)
                    {
                        // 2'li row: %30 şansla karşı tarafa da bağlan (çapraz değil, bitişik)
                        // ama sadece WouldCross kontrolü geçerse
                        int other = 1 - i;
                        if (Random.value < 0.3f && !WouldCross(currentRow, currentRow[i], nextRow, other))
                        {
                            if (!currentRow[i].childIds.Contains(nextRow[other].id))
                            {
                                currentRow[i].childIds.Add(nextRow[other].id);
                                connectedChildren.Add(nextRow[other].id);
                            }
                        }
                    }
                }
            }
            // ─── Farklı sayıda node: şerit bazlı bağlantı ───
            else
            {
                for (int i = 0; i < currentRow.Count; i++)
                {
                    MapNode node = currentRow[i];

                    // Her node kendi oransal pozisyonuna en yakın hedefe bağlanır
                    int bestCol = FindClosestColumn(i, currentRow.Count, nextRow.Count);
                    bestCol = Mathf.Clamp(bestCol, 0, nextRow.Count - 1);

                    node.childIds.Add(nextRow[bestCol].id);
                    connectedChildren.Add(nextRow[bestCol].id);

                    // Sadece bitişik sütuna ek bağlantı (%40 şans), çapraz geçiş yok
                    if (Random.value < 0.4f && nextRow.Count > 1)
                    {
                        // Kenar node'lar sadece iç tarafa, orta node rastgele
                        int secondaryCol;
                        if (bestCol == 0)
                            secondaryCol = 1;
                        else if (bestCol == nextRow.Count - 1)
                            secondaryCol = bestCol - 1;
                        else
                            secondaryCol = bestCol + (Random.value < 0.5f ? -1 : 1);

                        if (secondaryCol != bestCol && !WouldCross(currentRow, node, nextRow, secondaryCol))
                        {
                            if (!node.childIds.Contains(nextRow[secondaryCol].id))
                            {
                                node.childIds.Add(nextRow[secondaryCol].id);
                                connectedChildren.Add(nextRow[secondaryCol].id);
                            }
                        }
                    }
                }

                // Bağlantısız kalan child'lar için en yakın parent'tan bağla
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



    /// <summary>
    /// Haritada her kritik oda tipinden en az 1 tane olmasını garanti eder.
    /// Eksik tipler için uygun Combat node'larını dönüştürür.
    /// </summary>
    private static void EnforceMinimumRoomTypes(MapData map, int totalRows, MapLayerData config)
    {
        // Garanti edilecek tipler (row 0 ve boss hariç node'larda)
        // Layer config'e göre devre dışı tipler listeye eklenmez
        List<MapNodeType> requiredList = new List<MapNodeType>();
        requiredList.Add(MapNodeType.Rest);
        requiredList.Add(MapNodeType.Enchant);
        MapNodeType[] requiredTypes = requiredList.ToArray();

        foreach (var reqType in requiredTypes)
        {
            bool exists = false;
            foreach (var node in map.nodes)
            {
                if (node.nodeType == reqType) { exists = true; break; }
            }
            if (exists) continue;

            // Bu tip yok — uygun bir Combat node'u dönüştür
            // İlk ve son row'u (row 0, boss row) atla
            // Ödül tipi ise: parent ve child'ları da ödül olmayan node seç
            bool isRewardType = rewardTypes.Contains(reqType);
            MapNode bestCandidate = null;
            foreach (var node in map.nodes)
            {
                if (node.nodeType != MapNodeType.Combat) continue;
                if (node.row <= 0 || node.row >= totalRows) continue;
                // Ardışık ödül yasağı: ödül tipiyse parent veya child ödülse atla
                if (isRewardType)
                {
                    if (HasRewardParent(map, node)) continue;
                    bool childReward = false;
                    foreach (int cid in node.childIds)
                    {
                        MapNode c = map.GetNode(cid);
                        if (c != null && rewardTypes.Contains(c.nodeType)) { childReward = true; break; }
                    }
                    if (childReward) continue;
                }
                // Ortaya yakın row'ları tercih et
                if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                    bestCandidate = node;
            }

            if (bestCandidate != null)
                bestCandidate.nodeType = reqType;
        }
    }



    // ═══════════════════════════════════════════════════════
    // LAYER BAŞINA EN AZ 1 REST (CAMPFİRE) GARANTİSİ
    // ═══════════════════════════════════════════════════════

    private static void EnforceMinimumRest(MapData map, int totalRows)
    {
        bool hasRest = false;
        foreach (var node in map.nodes)
        {
            if (node.nodeType == MapNodeType.Rest) { hasRest = true; break; }
        }
        if (hasRest) return;

        // Rest yok — uygun bir Combat node'u Rest'e çevir
        MapNode bestCandidate = null;
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.Combat) continue;
            if (node.row <= 0 || node.row >= totalRows) continue;

            if (HasRewardParent(map, node)) continue;
            bool childReward = false;
            foreach (int cid in node.childIds)
            {
                MapNode c = map.GetNode(cid);
                if (c != null && rewardTypes.Contains(c.nodeType)) { childReward = true; break; }
            }
            if (childReward) continue;

            if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                bestCandidate = node;
        }

        // Uygun aday yoksa yasağı gevşet — Rest KESİNLİKLE garanti olmalı
        if (bestCandidate == null)
        {
            foreach (var node in map.nodes)
            {
                if (node.nodeType != MapNodeType.Combat) continue;
                if (node.row <= 0 || node.row >= totalRows) continue;
                if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                    bestCandidate = node;
            }
        }

        if (bestCandidate != null)
            bestCandidate.nodeType = MapNodeType.Rest;
    }

    // ═══════════════════════════════════════════════════════
    // LAYER BAŞINA EN AZ 1 ENCHANT GARANTİSİ
    // ═══════════════════════════════════════════════════════

    private static void EnforceMinimumEnchant(MapData map, int totalRows)
    {
        bool hasEnchant = false;
        foreach (var node in map.nodes)
        {
            if (node.nodeType == MapNodeType.Enchant) { hasEnchant = true; break; }
        }
        if (hasEnchant) return;

        MapNode bestCandidate = null;
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.Combat) continue;
            if (node.row <= 1 || node.row >= totalRows) continue;

            if (HasRewardParent(map, node)) continue;
            bool childReward = false;
            foreach (int cid in node.childIds)
            {
                MapNode c = map.GetNode(cid);
                if (c != null && rewardTypes.Contains(c.nodeType)) { childReward = true; break; }
            }
            if (childReward) continue;

            if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                bestCandidate = node;
        }

        // Uygun aday yoksa yasagi gevset — ama yine çift satır
        if (bestCandidate == null)
        {
            foreach (var node in map.nodes)
            {
                if (node.nodeType != MapNodeType.Combat) continue;
                if (node.row <= 0 || node.row >= totalRows) continue;
                if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                    bestCandidate = node;
            }
        }

        if (bestCandidate != null)
            bestCandidate.nodeType = MapNodeType.Enchant;
    }

}
