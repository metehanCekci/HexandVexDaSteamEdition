using System.Collections.Generic;

[System.Serializable]
public class MapData
{
    public int layerIndex;
    public List<MapNode> nodes = new List<MapNode>();
    public int currentNodeId = -1;
    public int bossNodeId = -1;

    public MapNode GetNode(int id)
    {
        if (id < 0 || id >= nodes.Count) return null;
        return nodes[id];
    }

    public List<MapNode> GetRow(int row)
    {
        List<MapNode> result = new List<MapNode>();
        foreach (var node in nodes)
        {
            if (node.row == row) result.Add(node);
        }
        return result;
    }

    public List<MapNode> GetReachableNodes()
    {
        List<MapNode> reachable = new List<MapNode>();
        if (currentNodeId < 0) return reachable;

        MapNode current = GetNode(currentNodeId);
        if (current == null) return reachable;

        foreach (int childId in current.childIds)
        {
            MapNode child = GetNode(childId);
            if (child != null) reachable.Add(child);
        }
        return reachable;
    }
}
