using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
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
            int nodeCount = Random.Range(config.minNodesPerRow, config.maxNodesPerRow + 1);

            for (int c = 0; c < nodeCount; c++)
            {
                MapNodeType type = PickNodeType(config, r, totalRows);

                MapNode node = new MapNode
                {
                    id = nextId++,
                    row = r,
                    column = c,
                    nodeType = type,
                    visited = false
                };
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

        // ─── Enforce no consecutive shops on any path ───
        EnforceNoConsecutiveShops(map);

        return map;
    }

    private static MapNodeType PickNodeType(MapLayerData config, int row, int totalRows)
    {
        // Row 1: always combat
        if (row <= 1) return MapNodeType.Combat;

        // Pre-boss row: always combat or elite
        if (row == totalRows - 1)
        {
            return Random.value < config.eliteChance * 2f ? MapNodeType.EliteCombat : MapNodeType.Combat;
        }

        // Rest only from row 3+
        bool canRest = row >= 3;

        float roll = Random.value;
        float cumulative = 0f;

        cumulative += config.eliteChance;
        if (roll < cumulative) return MapNodeType.EliteCombat;

        cumulative += config.shopChance;
        if (roll < cumulative) return MapNodeType.Shop;

        cumulative += config.perkChance;
        if (roll < cumulative) return MapNodeType.PerkSelection;

        if (canRest)
        {
            cumulative += config.restChance;
            if (roll < cumulative) return MapNodeType.Rest;
        }

        cumulative += config.eventChance;
        if (roll < cumulative) return MapNodeType.Event;

        return MapNodeType.Combat;
    }

    private static void GenerateConnections(MapData map, int totalRows)
    {
        for (int r = 0; r < totalRows; r++)
        {
            List<MapNode> currentRow = map.GetRow(r);
            List<MapNode> nextRow = map.GetRow(r + 1);

            if (currentRow.Count == 0 || nextRow.Count == 0) continue;

            // Track which next-row nodes have at least one parent
            HashSet<int> connectedChildren = new HashSet<int>();

            // For each node in current row, connect to 1-2 nodes in next row
            for (int i = 0; i < currentRow.Count; i++)
            {
                MapNode node = currentRow[i];

                // Calculate the "natural" column range this node maps to in the next row
                float ratio = currentRow.Count <= 1 ? 0.5f : (float)i / (currentRow.Count - 1);
                int targetCol = Mathf.RoundToInt(ratio * (nextRow.Count - 1));

                // Always connect to the closest node
                int primaryChild = Mathf.Clamp(targetCol, 0, nextRow.Count - 1);
                node.childIds.Add(nextRow[primaryChild].id);
                connectedChildren.Add(nextRow[primaryChild].id);

                // 60% chance to also connect to an adjacent node (if it doesn't cross)
                if (Random.value < 0.6f && nextRow.Count > 1)
                {
                    int secondaryCol = primaryChild + (Random.value < 0.5f ? -1 : 1);
                    secondaryCol = Mathf.Clamp(secondaryCol, 0, nextRow.Count - 1);

                    if (secondaryCol != primaryChild && !WouldCross(currentRow, node, nextRow, secondaryCol))
                    {
                        node.childIds.Add(nextRow[secondaryCol].id);
                        connectedChildren.Add(nextRow[secondaryCol].id);
                    }
                }
            }

            // Ensure every next-row node has at least one parent
            for (int j = 0; j < nextRow.Count; j++)
            {
                if (!connectedChildren.Contains(nextRow[j].id))
                {
                    // Find closest parent in current row
                    float ratio = nextRow.Count <= 1 ? 0.5f : (float)j / (nextRow.Count - 1);
                    int closestParent = Mathf.Clamp(Mathf.RoundToInt(ratio * (currentRow.Count - 1)), 0, currentRow.Count - 1);

                    currentRow[closestParent].childIds.Add(nextRow[j].id);
                }
            }
        }
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

                // Check crossing: if fromIdx < i, then targetCol should not be < otherCol
                // and vice versa
                if (fromIdx < i && targetCol > otherCol) return true;
                if (fromIdx > i && targetCol < otherCol) return true;
            }
        }
        return false;
    }

    private static void PruneUnreachable(MapData map)
    {
        // BFS from start to find all reachable nodes
        HashSet<int> reachable = new HashSet<int>();
        Queue<int> queue = new Queue<int>();

        // Start node is always id 0
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

        // Remove unreachable nodes
        map.nodes.RemoveAll(n => !reachable.Contains(n.id));

        // Clean up child references to removed nodes
        foreach (var node in map.nodes)
        {
            node.childIds.RemoveAll(id => !reachable.Contains(id));
        }
    }

    private static void EnforceNoConsecutiveShops(MapData map)
    {
        // For each shop node, check if any of its children are also shops
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
