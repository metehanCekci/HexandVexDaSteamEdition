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
        // Kural: Arka arkaya max 2 çoklu (2-3 node) row olabilir,
        // sonra zorunlu 1 node row gelir. Kısa-uzun ritmi.
        int multiRowStreak = 0; // Ardışık çoklu row sayacı

        for (int r = 1; r < totalRows; r++)
        {
            int nodeCount;

            if (multiRowStreak >= 2)
            {
                // 2 ardışık çoklu row'dan sonra → zorunlu tek node
                nodeCount = 1;
                multiRowStreak = 0;
            }
            else
            {
                // %40 tek node, %50 iki node, %10 üç node
                float roll = Random.value;
                nodeCount = roll < 0.40f ? 1 : roll < 0.90f ? 2 : 3;

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
        EnforceMinimumRoomTypes(map, totalRows);

        // ─── Layer başına en az 2 perk garanti ───
        EnforceMinimumPerkCount(map, totalRows, 2);

        // ─── İlk savaştan sonra perk garanti ───
        EnforcePerkAfterFirstCombat(map);

        // ─── SON GÜVENLİK: Ardışık ödül yasağı (tüm post-processing sonrası) ───
        EnforceNoConsecutiveRewards(map);

        // ─── SON GÜVENLİK: Layer başına en az 1 Shop garanti ───
        EnforceMinimumShop(map, totalRows);

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
            // Row 1: combat — ama çoklu row'daysa EnforceNoDuplicatesInRow farklılaştıracak
            if (node.row == 1) { node.nodeType = MapNodeType.Combat; continue; }
            if (node.row == totalRows - 1)
            {
                // Tek node row'daysa elite koyma
                List<MapNode> rowNodes = map.GetRow(node.row);
                if (rowNodes.Count <= 1)
                    node.nodeType = MapNodeType.Combat;
                else
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

        // Elite arkasında ödül garanti
        EnforceEliteReward(map, config);

        // Ardışık ödül yasağı (ödül → ödül olmasın)
        EnforceNoConsecutiveRewards(map);

        // Ardışık shop yasağı
        EnforceNoConsecutiveShops(map);

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
        bool canRest = row >= 3;

        // Boss öncesi → combat/elite ama yine farklı olsunlar
        if (row == totalRows - 1)
        {
            if (count == 1)
            {
                // Tek node — elite zorunlu olmamalı
                rowNodes[0].nodeType = MapNodeType.Combat;
            }
            else
            {
                // En az biri elite, en az biri normal combat — seçim anlamlı
                int eliteIdx = Random.Range(0, count);
                for (int i = 0; i < count; i++)
                    rowNodes[i].nodeType = (i == eliteIdx) ? MapNodeType.EliteCombat : MapNodeType.Combat;
            }
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

            // Parent ödülse → kesinlikle savaş
            if (parentReward)
            {
                if (assignedTypes.Contains(MapNodeType.Combat) && !assignedTypes.Contains(MapNodeType.EliteCombat))
                    node.nodeType = MapNodeType.EliteCombat;
                else if (assignedTypes.Contains(MapNodeType.EliteCombat) && !assignedTypes.Contains(MapNodeType.Combat))
                    node.nodeType = MapNodeType.Combat;
                else
                    node.nodeType = Random.value < config.eliteChance * 2f ? MapNodeType.EliteCombat : MapNodeType.Combat;
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
                // Risk node: combat veya elite — ama zaten atanmış tipten farklı
                if (assignedTypes.Contains(MapNodeType.Combat) && !assignedTypes.Contains(MapNodeType.EliteCombat))
                    node.nodeType = MapNodeType.EliteCombat;
                else if (assignedTypes.Contains(MapNodeType.EliteCombat) && !assignedTypes.Contains(MapNodeType.Combat))
                    node.nodeType = MapNodeType.Combat;
                else
                    node.nodeType = Random.value < config.eliteChance * 2f ? MapNodeType.EliteCombat : MapNodeType.Combat;
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
        if (!used.Contains(MapNodeType.Shop)) return false;
        if (!used.Contains(MapNodeType.PerkSelection)) return false;
        if (canRest && !used.Contains(MapNodeType.Rest)) return false;
        return true;
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
                        // Erken row / boss öncesi: Combat ↔ Elite arası değiştir
                        if (!seen.Contains(MapNodeType.EliteCombat))
                            node.nodeType = MapNodeType.EliteCombat;
                        else if (!seen.Contains(MapNodeType.Combat))
                            node.nodeType = MapNodeType.Combat;
                        // 3 node ve ikisi de kullanıldıysa — Combat fallback
                        else
                            node.nodeType = MapNodeType.Combat;
                    }
                    else
                    {
                        // Normal row: ödül veya risk — kullanılmamış olanı seç
                        // Ama önce parent'lardan biri ödül mü kontrol et — ardışık ödül yasağını korumak için
                        bool parentIsReward = false;
                        foreach (var pNode in map.nodes)
                        {
                            if (pNode.childIds.Contains(node.id) && rewardTypes.Contains(pNode.nodeType))
                            { parentIsReward = true; break; }
                        }

                        if (parentIsReward)
                        {
                            // Parent ödülse → sadece risk tipi seç
                            if (!seen.Contains(MapNodeType.EliteCombat))
                                node.nodeType = MapNodeType.EliteCombat;
                            else if (!seen.Contains(MapNodeType.Combat))
                                node.nodeType = MapNodeType.Combat;
                            else
                                node.nodeType = MapNodeType.Combat;
                        }
                        else
                        {
                            // Parent risk → ödül tipleri de seçilebilir
                            if (!seen.Contains(MapNodeType.Shop))
                                node.nodeType = MapNodeType.Shop;
                            else if (!seen.Contains(MapNodeType.PerkSelection))
                                node.nodeType = MapNodeType.PerkSelection;
                            else if (canRest && !seen.Contains(MapNodeType.Rest))
                                node.nodeType = MapNodeType.Rest;
                            else if (!seen.Contains(MapNodeType.EliteCombat))
                                node.nodeType = MapNodeType.EliteCombat;
                            else if (!seen.Contains(MapNodeType.Combat))
                                node.nodeType = MapNodeType.Combat;
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

    /// <summary>
    /// Haritada her kritik oda tipinden en az 1 tane olmasını garanti eder.
    /// Eksik tipler için uygun Combat node'larını dönüştürür.
    /// </summary>
    private static void EnforceMinimumRoomTypes(MapData map, int totalRows)
    {
        // Garanti edilecek tipler (row 0 ve boss hariç node'larda)
        MapNodeType[] requiredTypes = new MapNodeType[]
        {
            MapNodeType.Shop,
            MapNodeType.PerkSelection,
            MapNodeType.EliteCombat
        };

        // Rest sadece yeterli row varsa garanti
        if (totalRows >= 4)
            requiredTypes = new MapNodeType[]
            {
                MapNodeType.Shop,
                MapNodeType.PerkSelection,
                MapNodeType.EliteCombat,
                MapNodeType.Rest
            };

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
                // Elite tek node row'a konmamalı — oyuncunun alternatifi olmalı
                if (reqType == MapNodeType.EliteCombat && map.GetRow(node.row).Count <= 1) continue;
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

    /// <summary>
    /// Tüm post-processing sonrası Shop silinmiş olabilir.
    /// En az 1 Shop garanti eder — ardışık ödül yasağını koruyarak.
    /// </summary>
    private static void EnforceMinimumShop(MapData map, int totalRows)
    {
        bool hasShop = false;
        foreach (var node in map.nodes)
        {
            if (node.nodeType == MapNodeType.Shop) { hasShop = true; break; }
        }
        if (hasShop) return;

        // Shop yok — uygun bir Combat node'u Shop'a çevir
        MapNode bestCandidate = null;
        foreach (var node in map.nodes)
        {
            if (node.nodeType != MapNodeType.Combat) continue;
            if (node.row <= 1 || node.row >= totalRows) continue;

            // Ardışık ödül yasağı: parent veya child ödülse atla
            if (HasRewardParent(map, node)) continue;
            bool childReward = false;
            foreach (int cid in node.childIds)
            {
                MapNode c = map.GetNode(cid);
                if (c != null && rewardTypes.Contains(c.nodeType)) { childReward = true; break; }
            }
            if (childReward) continue;

            // Ortaya yakın row'ları tercih et
            if (bestCandidate == null || Mathf.Abs(node.row - totalRows / 2) < Mathf.Abs(bestCandidate.row - totalRows / 2))
                bestCandidate = node;
        }

        if (bestCandidate != null)
            bestCandidate.nodeType = MapNodeType.Shop;
    }

    // ═══════════════════════════════════════════════════════
    // LAYER BAŞINA EN AZ N PERK GARANTİSİ
    // ═══════════════════════════════════════════════════════

    private static void EnforceMinimumPerkCount(MapData map, int totalRows, int minCount)
    {
        // Mevcut perk sayısını say
        int perkCount = 0;
        foreach (var node in map.nodes)
        {
            if (node.nodeType == MapNodeType.PerkSelection) perkCount++;
        }

        // Yeterince varsa çık
        while (perkCount < minCount)
        {
            // Uygun bir Combat node'u PerkSelection'a çevir
            // Row 0, row 1, boss row'u atla. Ardışık ödül yasağını koru.
            MapNode bestCandidate = null;
            int bestScore = int.MaxValue;

            foreach (var node in map.nodes)
            {
                if (node.nodeType != MapNodeType.Combat) continue;
                if (node.row <= 1 || node.row >= totalRows) continue;

                // Ardışık ödül yasağı: parent veya child ödülse atla
                if (HasRewardParent(map, node)) continue;
                bool childReward = false;
                foreach (int cid in node.childIds)
                {
                    MapNode c = map.GetNode(cid);
                    if (c != null && rewardTypes.Contains(c.nodeType)) { childReward = true; break; }
                }
                if (childReward) continue;

                // Ortaya yakın olanı tercih et
                int score = Mathf.Abs(node.row - totalRows / 2);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = node;
                }
            }

            if (bestCandidate != null)
            {
                bestCandidate.nodeType = MapNodeType.PerkSelection;
                perkCount++;
            }
            else
            {
                break; // Uygun aday kalmadı
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // İLK SAVAŞTAN SONRA PERK GARANTİSİ
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Row 0 (ilk savaş) sonrasında en az bir patikada PerkSelection olmalı.
    /// Row 1'deki node'lardan birini (veya tek node row'da tek node'u)
    /// PerkSelection'a çevirir.
    /// </summary>
    private static void EnforcePerkAfterFirstCombat(MapData map)
    {
        // Row 0'ın child'larını bul
        MapNode startNode = null;
        foreach (var node in map.nodes)
        {
            if (node.row == 0) { startNode = node; break; }
        }
        if (startNode == null || startNode.childIds.Count == 0) return;

        // Child'larda zaten perk var mı?
        bool hasPerk = false;
        foreach (int cid in startNode.childIds)
        {
            MapNode child = map.GetNode(cid);
            if (child != null && child.nodeType == MapNodeType.PerkSelection)
            { hasPerk = true; break; }
        }
        if (hasPerk) return;

        // Tek child varsa onu PerkSelection yap
        if (startNode.childIds.Count == 1)
        {
            MapNode child = map.GetNode(startNode.childIds[0]);
            if (child != null) child.nodeType = MapNodeType.PerkSelection;
            return;
        }

        // Birden fazla child varsa birini PerkSelection yap (tercihen Combat olanı)
        foreach (int cid in startNode.childIds)
        {
            MapNode child = map.GetNode(cid);
            if (child != null && child.nodeType == MapNodeType.Combat)
            {
                child.nodeType = MapNodeType.PerkSelection;
                return;
            }
        }

        // Combat child yoksa herhangi birini çevir (boss veya elite değilse)
        foreach (int cid in startNode.childIds)
        {
            MapNode child = map.GetNode(cid);
            if (child != null && child.nodeType != MapNodeType.Boss)
            {
                child.nodeType = MapNodeType.PerkSelection;
                return;
            }
        }
    }
}
