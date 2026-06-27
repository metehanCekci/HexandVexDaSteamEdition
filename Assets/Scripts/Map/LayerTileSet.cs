using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class EnemySpawnEntry
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance = 0.3f;
}

/// <summary>
/// Eşleşen ground + column çifti.
/// Aynı index'teki ground ve column her zaman birlikte kullanılır.
/// </summary>
[System.Serializable]
public class GroundVariant
{
    public TileBase groundTile;
    public TileBase columnTile;
    [Min(0f)] public float weight = 1f;
}

/// <summary>
/// Bir layer'ın tüm görsel ve prefab verilerini tutan ScriptableObject.
/// Her layer için ayrı bir asset oluştur (Layer1TileSet, Layer2TileSet vb.)
/// LevelGenerator bu asset'i okur — direkt field tutmaz.
/// </summary>
[CreateAssetMenu(menuName = "HexAndVex/Layer Tile Set")]
public class LayerTileSet : ScriptableObject
{
    [Header("Zemin Varyantları (ground + column çifti, weight ile oran)")]
    public GroundVariant[] groundVariants = new GroundVariant[]
    {
        new GroundVariant { weight = 1f }
    };

    /// <summary>
    /// Weight'e göre rastgele bir GroundVariant seçer.
    /// Dizi boşsa veya toplam weight 0 ise null döner.
    /// </summary>
    public GroundVariant PickRandomVariant()
    {
        if (groundVariants == null || groundVariants.Length == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < groundVariants.Length; i++)
            totalWeight += groundVariants[i].weight;

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < groundVariants.Length; i++)
        {
            cumulative += groundVariants[i].weight;
            if (roll < cumulative) return groundVariants[i];
        }
        return groundVariants[groundVariants.Length - 1];
    }

    // Geriye uyumluluk — eski kodda ts.groundTile / ts.columnTile kullanan yerler için
    public TileBase groundTile => groundVariants != null && groundVariants.Length > 0 ? groundVariants[0].groundTile : null;
    public TileBase columnTile => groundVariants != null && groundVariants.Length > 0 ? groundVariants[0].columnTile : null;

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
