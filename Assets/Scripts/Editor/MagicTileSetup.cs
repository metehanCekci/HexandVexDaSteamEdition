#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;

/// <summary>
/// Magic Tile asset'lerini otomatik olusturur ve MagicTileManager'a atar.
/// Lower (column) + Upper (ground) tile asset'leri ayri ayri olusturulur.
/// Tools > Hex and Vex > Setup Magic Tile Assets
/// </summary>
public static class MagicTileSetup
{
    // Sprite yollari — lower (column katmani)
    private static readonly (string color, string path)[] lowerSprites = {
        ("Red",    "Assets/Sprites/Tiles/RedMagicTile.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicTile.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicTile.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicTile.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicTile.aseprite"),
    };

    // Sprite yollari — upper (ground katmani)
    private static readonly (string color, string path)[] upperSprites = {
        ("Red",    "Assets/Sprites/Tiles/UpRedMagic.aseprite"),
        ("Blue",   "Assets/Sprites/Tiles/BlueMagicUpper.aseprite"),
        ("Green",  "Assets/Sprites/Tiles/GreenMagicUpper.aseprite"),
        ("Yellow", "Assets/Sprites/Tiles/YellowMagicUpper.aseprite"),
        ("Orange", "Assets/Sprites/Tiles/OrangeMagicUpper.aseprite"),
    };

    [MenuItem("Tools/Hex and Vex/Setup Magic Tile Assets")]
    public static void SetupMagicTiles()
    {
        // Resources klasoru yoksa olustur
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // ── Lower tile asset'leri olustur/guncelle ──
        foreach (var (color, path) in lowerSprites)
        {
            string assetPath = $"Assets/Resources/{color}MagicTile.asset";
            CreateOrUpdateTileAsset(assetPath, path, color, "lower");
        }

        // ── Upper tile asset'leri olustur/guncelle ──
        foreach (var (color, path) in upperSprites)
        {
            string assetPath = $"Assets/Resources/{color}MagicUpperTile.asset";
            CreateOrUpdateTileAsset(assetPath, path, color, "upper");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── MagicTileManager'a ata ──
        AssignToManager();

        Debug.Log("Magic Tile asset'leri olusturuldu ve MagicTileManager'a atandi!");
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

        Debug.Log($"[MagicTileSetup] {color} {layer} tile -> {assetPath} (sprite: {spritePath})");
    }

    private static void AssignToManager()
    {
        MagicTileManager mgr = Object.FindFirstObjectByType<MagicTileManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[MagicTileSetup] MagicTileManager bulunamadi! Sahneye ekle.");
            return;
        }

        // Lower tiles
        mgr.redTile    = LoadTile("RedMagicTile");
        mgr.blueTile   = LoadTile("BlueMagicTile");
        mgr.greenTile  = LoadTile("GreenMagicTile");
        mgr.yellowTile = LoadTile("YellowMagicTile");
        mgr.orangeTile = LoadTile("OrangeMagicTile");

        // Upper tiles
        mgr.redUpperTile    = LoadTile("RedMagicUpperTile");
        mgr.blueUpperTile   = LoadTile("BlueMagicUpperTile");
        mgr.greenUpperTile  = LoadTile("GreenMagicUpperTile");
        mgr.yellowUpperTile = LoadTile("YellowMagicUpperTile");
        mgr.orangeUpperTile = LoadTile("OrangeMagicUpperTile");

        EditorUtility.SetDirty(mgr);
        Debug.Log("[MagicTileSetup] MagicTileManager'a tum tile asset'ler atandi.");
    }

    private static TileBase LoadTile(string name)
    {
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Resources/{name}.asset");
        if (tile == null)
            Debug.LogWarning($"[MagicTileSetup] Tile asset bulunamadi: {name}");
        return tile;
    }
}
#endif
