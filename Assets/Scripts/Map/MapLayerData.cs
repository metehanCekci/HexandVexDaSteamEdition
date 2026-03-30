using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "HexAndVex/Map Layer Data")]
public class MapLayerData : ScriptableObject
{
    public string layerName = "Layer 1";
    public int totalRows = 8;
    public int minNodesPerRow = 2;
    public int maxNodesPerRow = 3;
    [Range(0f, 1f)] public float threeNodeChance = 0.15f;

    [Header("Node Type Weights")]
    [Range(0f, 1f)] public float shopChance = 0.12f;
    [Range(0f, 1f)] public float perkChance = 0.15f;
    [Range(0f, 1f)] public float restChance = 0.10f;
    [Range(0f, 1f)] public float eliteChance = 0.10f;
    [Range(0f, 1f)] public float enchantChance = 0.10f;
    // Combat fills the remainder

    [Header("Tile Set")]
    public LayerTileSet tileSet;
}
