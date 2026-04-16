#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Magic Tile asset'lerini otomatik olusturur ve MagicTileManager'a atar.
/// 3 katman destegi: ground (arka) + column (sutun) + foreground (on, karakter arkasinda)
/// Tools > Hex and Vex > Setup Magic Tile Assets
/// </summary>
public static class MagicTileSetup
{
    // Ground (upper/back) sprites — karakter onunde, groundMap (sorting 0)
    private static readonly (string color, string path)[] groundSprites = {
        ("Red",    "Assets/Sprites/Tiles/UpRedMagic.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicUpper.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicUpper.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicUpper.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicTileUpper.aseprite"),
    };

    // Column (lower/full) sprites — columnMap (sorting -1)
    private static readonly (string color, string path)[] columnSprites = {
        ("Red",    "Assets/Sprites/Tiles/RedMagicTile.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicTile.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicTile.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicTile.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicTile.aseprite"),
    };

    // Foreground (katman/front) sprites — karakter arkasinda, MagicOverlay (sorting 200)
    // Yellow haric — aninda consume edildigi icin katman gereksiz
    private static readonly (string color, string path)[] foregroundSprites = {
        ("Red",    "Assets/Sprites/Tiles/KatmanRedTile.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/KatmanBlueTile.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/KatmanGreenTile.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/KatmanOrangeTile.aseprite"),
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

        // Create/update ground tile assets
        foreach (var (color, path) in groundSprites)
            CreateOrUpdateTileAsset($"Assets/Resources/{color}MagicUpperTile.asset", path, color, "ground");

        // Create/update column tile assets
        foreach (var (color, path) in columnSprites)
            CreateOrUpdateTileAsset($"Assets/Resources/{color}MagicTile.asset", path, color, "column");

        // Create/update foreground tile assets
        foreach (var (color, path) in foregroundSprites)
            CreateOrUpdateTileAsset($"Assets/Resources/Katman{color}Tile.asset", path, color, "foreground");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignToManager();
        Debug.Log("[MagicTileSetup] Tum tile asset'ler olusturuldu ve MagicTileManager'a atandi!");
    }

    private static void CreateOrUpdateTileAsset(string assetPath, string spritePath, string color, string layer)
    {
        // .aseprite dosyalarinda sprite sub-asset olarak import edilir
        // Once direkt dene, basarisiz olursa sub-asset'ler arasinda ara
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(spritePath))
            {
                if (obj is Sprite s) { sprite = s; break; }
            }
        }
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

        // Build sets of which colors have ground/foreground sprites
        var hasGround = new HashSet<string>();
        foreach (var (color, _) in groundSprites) hasGround.Add(color);
        var hasForeground = new HashSet<string>();
        foreach (var (color, _) in foregroundSprites) hasForeground.Add(color);

        // Build entries array — one per MagicTileType
        // Only load tiles that exist in the sprite arrays (prevents stale assets)
        var entries = new List<MagicTileManager.MagicTileEntry>();
        foreach (var kvp in colorToType)
        {
            string color = kvp.Key;
            MagicTileType type = kvp.Value;

            TileBase ground = hasGround.Contains(color) ? LoadTile($"{color}MagicUpperTile") : null;
            TileBase column = LoadTile($"{color}MagicTile");
            TileBase foreground = hasForeground.Contains(color) ? LoadTile($"Katman{color}Tile") : null;

            entries.Add(new MagicTileManager.MagicTileEntry
            {
                type = type,
                groundTile = ground,
                columnTile = column,
                foregroundTile = foreground,
            });
        }

        mgr.tileEntries = entries.ToArray();
        mgr.RebuildLookup();
        EditorUtility.SetDirty(mgr);

        Debug.Log($"[MagicTileSetup] {entries.Count} entry MagicTileManager'a atandi.");
    }

    private static TileBase LoadTile(string name)
    {
        return AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Resources/{name}.asset");
    }
}
#endif
