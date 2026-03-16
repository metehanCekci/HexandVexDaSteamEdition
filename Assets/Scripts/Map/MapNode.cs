using System.Collections.Generic;

[System.Serializable]
public class MapNode
{
    public int id;
    public int row;
    public int column;
    public MapNodeType nodeType;
    public List<int> childIds = new List<int>();
    public bool visited;
}
