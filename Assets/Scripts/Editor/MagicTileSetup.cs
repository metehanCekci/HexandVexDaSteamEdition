#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Magic Tile asset'lerini otomatik olusturur ve MagicTileManager'a atar.
/// Lower (column) + Upper (ground) tile asset'leri ayri ayri olusturulur.
/// Tools > Hex and Vex > Setup Magic Tile Assets
/// </summary>
public static class MagicTileSetup
{
    // Sprite yollari — lower (column katmani, 50x41)
    private static readonly (string color, string path)[] lowerSprites = {
        ("Red",    "Assets/Sprites/Tiles/RedMagicTile.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicTile.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicTile.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicTile.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicTile.aseprite"),
    };

    // Sprite yollari — upper (ground katmani, 50x25)
    private static readonly (string color, string path)[] upperSprites = {
        ("Red",    "Assets/Sprites/Tiles/UpRedMagic.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicUpper.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicUpper.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicUpper.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicUpper.aseprite"),
    };

    private static readonly Dictionary<string, MagicTileType> colorToType = new Dictionary<string, MagicTileType>
    {
        { "Red",    MagicTileType.Red },
        { "Blue",   MagicTileType.Blue },
        { "Green",  MagicTileType.Green },
        { "Yellow", MagicTileType.Yellow },
        { "Orange", MagicTileType.Orange },
    };

    [MenuItem("Tools/Hex and Vex/Setup Magic Tile Assets")]
    public static void SetupMagicTiles()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Create/update lower tile assets
        foreach (var (color, path) in lowerSprites)
        {
            string assetPath = $"Assets/Resources/{color}MagicTile.asset";
            CreateOrUpdateTileAsset(assetPath, path, color, "lower");
        }

        // Create/update upper tile assets
        foreach (var (color, path) in upperSprites)
        {
            string assetPath = $"Assets/Resources/{color}MagicUpperTile.asset";
            CreateOrUpdateTileAsset(assetPath, path, color, "upper");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignToManager();
        Debug.Log("[MagicTileSetup] Tum tile asset'ler olusturuldu ve MagicTileManager'a atandi!");
    }

    private static void CreateOrUpdateTileAsset(string assetPath, string spritePath, string color, string layer)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[MagicTileSetup] Sprite bulunamadi: {spritePath} ({color} {layer}) — sprite dosyasini olusturup tekrar calistir.");
            return;
        }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }

        tile.sprite = sprite;
        tile.color = Color.white;
        EditorUtility.SetDirty(tile);

        Debug.Log($"[MagicTileSetup] {color} {layer} tile -> {assetPath}");
    }

    private static void AssignToManager()
    {
        MagicTileManager mgr = Object.FindFirstObjectByType<MagicTileManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[MagicTileSetup] MagicTileManager bulunamadi! Sahneye ekle.");
            return;
        }

        // Build entries array — one per MagicTileType
        var entries = new List<MagicTileManager.MagicTileEntry>();
        foreach (var kvp in colorToType)
        {
            string color = kvp.Key;
            MagicTileType type = kvp.Value;

            TileBase ground = LoadTile($"{color}MagicUpperTile");
            TileBase column = LoadTile($"{color}MagicTile");

            entries.Add(new MagicTileManager.MagicTileEntry
            {
                type = type,
                groundTile = ground,
                columnTile = column,
            });
        }

        mgr.tileEntries = entries.ToArray();
        mgr.RebuildLookup();
        EditorUtility.SetDirty(mgr);

        Debug.Log($"[MagicTileSetup] {entries.Count} entry MagicTileManager'a atandi.");
    }

    private static TileBase LoadTile(string name)
    {
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Resources/{name}.asset");
        if (tile == null)
            Debug.LogWarning($"[MagicTileSetup] Tile asset bulunamadi: {name} — sprite eksik olabilir.");
        return tile;
    }
}
#endif
