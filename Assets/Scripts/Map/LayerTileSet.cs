using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance = 0.3f;
}

/// <summary>
/// Bir layer'ın tüm görsel ve prefab verilerini tutan ScriptableObject.
/// Her layer için ayrı bir asset oluştur (Layer1TileSet, Layer2TileSet vb.)
/// LevelGenerator bu asset'i okur — direkt field tutmaz.
/// </summary>
[CreateAssetMenu(menuName = "HexAndVex/Layer Tile Set")]
public class LayerTileSet : ScriptableObject
{
    [Header("Zemin")]
    public TileBase groundTile;
    public TileBase columnTile;

    [Header("Tehlike Tile'ı")]
    public TileBase hazardTile;       // Normal spike (null ise hazard spawn olmaz)
    public TileBase explosionTile;    // Patlayan tile (null ise hazardTile kullanılır)

    [Header("Scaffold (Çöken Platform)")]
    public TileBase scaffoldTile;
    public TileBase lowerScaffoldTile;
    [Range(0f, 1f)] public float scaffoldSpawnChance = 0.08f;

    [Header("Teleport Tile'ları")]
    public TileBase teleportTileA;
    public TileBase teleportTileB;

    [Header("Düşmanlar (sırayla kontrol edilir, ilk geçen spawn olur; hiçbiri geçmezse index 0)")]
    public EnemySpawnEntry[] enemies;

    [Header("Boss Prefabları (null ise bu layerda boss çıkmaz)")]
    public GameObject bossPrefab;
    public GameObject totemPrefab;
}
