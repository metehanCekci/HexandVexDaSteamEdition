#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Magic Tile asset'lerini tek bir aseprite palette dosyasindan otomatik olusturur ve
/// MagicTileManager'a atar.
///
/// Palette: Assets/Sprites/Tiles/EnchantedTiles.aseprite
/// Slice sirasi (Aseprite slice adi "EnchantedTiles_N"):
///   0-4: {Green,Blue,Red,Yellow,Orange} Upper  -> groundTile slot (mid overlay, sorting 50)
///   5-9: {Green,Blue,Red,Yellow,Orange} Full   -> columnTile slot (sorting -1)
///
/// Foreground (Katman*) slot artik kullanilmiyor — 2 katmanli sistem.
///
/// Tools > Hex and Vex > Setup Magic Tile Assets
/// </summary>
public static class MagicTileSetup
{
    private const string PalettePath = "Assets/Sprites/Tiles/EnchantedTiles.aseprite";

    // Slice index -> (color, slot) mapping (sliceIndex = 0..9)
    private static readonly MagicTileType[] sliceColorOrder = {
        MagicTileType.Green,   // 0
        MagicTileType.Blue,    // 1
        MagicTileType.Red,     // 2
        MagicTileType.Yellow,  // 3
        MagicTileType.Orange,  // 4
        MagicTileType.Green,   // 5
        MagicTileType.Blue,    // 6
        MagicTileType.Red,     // 7
        MagicTileType.Yellow,  // 8
        MagicTileType.Orange,  // 9
    };

    // Slots 0..4 = upper (ground), 5..9 = full (column)
    private static bool IsUpperSlice(int sliceIndex) => sliceIndex < 5;

    [MenuItem("Tools/Hex and Vex/Setup Magic Tile Assets")]
    public static void SetupMagicTiles()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Load all sub-sprites from the palette
        var sprites = LoadPaletteSprites();
        if (sprites == null)
        {
            Debug.LogError($"[MagicTileSetup] Palette bulunamadi: {PalettePath}");
            return;
        }
        if (sprites.Count < 10)
        {
            Debug.LogError($"[MagicTileSetup] Palette'te 10 sprite bekleniyor, {sprites.Count} bulundu. Slice yapisini kontrol et.");
            foreach (var kv in sprites)
                Debug.Log($"  - {kv.Key}");
            return;
        }

        // For each of the 10 sub-sprites, create/update its Tile asset
        for (int i = 0; i < 10; i++)
        {
            Sprite s = GetSpriteByIndex(sprites, i);
            if (s == null)
            {
                Debug.LogWarning($"[MagicTileSetup] Slice #{i} bulunamadi (EnchantedTiles_{i}).");
                continue;
            }

            MagicTileType color = sliceColorOrder[i];
            string slot = IsUpperSlice(i) ? "upper" : "full";
            string assetPath = IsUpperSlice(i)
                ? $"Assets/Resources/{color}MagicUpperTile.asset"
                : $"Assets/Resources/{color}MagicTile.asset";

            CreateOrUpdateTileAsset(assetPath, s, color.ToString(), slot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AssignToManager();
        Debug.Log("[MagicTileSetup] Tum tile asset'ler palette'ten olusturuldu ve MagicTileManager'a atandi!");
    }

    [MenuItem("Tools/Hex and Vex/Clean Old Magic Tile Assets")]
    public static void CleanOldAssets()
    {
        if (!EditorUtility.DisplayDialog("Clean Old Magic Tile Assets",
            "Assets/Resources altindaki ESKI magic tile .asset'leri silinecek:\n" +
            "- Katman{Color}Tile.asset (foreground, artik kullanilmiyor)\n\n" +
            "Aktif olan {Color}MagicUpperTile.asset ve {Color}MagicTile.asset DOKUNULMAYACAK.\n\n" +
            "Devam?", "Sil", "Iptal"))
            return;

        string[] colors = { "Red", "Blue", "Green", "Yellow", "Orange" };
        int removed = 0;
        foreach (var c in colors)
        {
            string p = $"Assets/Resources/Katman{c}Tile.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(p) != null)
            {
                AssetDatabase.DeleteAsset(p);
                removed++;
                Debug.Log($"[MagicTileSetup] Silindi: {p}");
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MagicTileSetup] {removed} eski asset silindi.");
    }

    private static Dictionary<string, Sprite> LoadPaletteSprites()
    {
        var result = new Dictionary<string, Sprite>();
        var all = AssetDatabase.LoadAllAssetsAtPath(PalettePath);
        if (all == null || all.Length == 0) return null;
        foreach (var o in all)
        {
            if (o is Sprite s)
                result[s.name] = s;
        }
        return result.Count > 0 ? result : null;
    }

    private static Sprite GetSpriteByIndex(Dictionary<string, Sprite> sprites, int index)
    {
        // Aseprite importer adlandirmasi: "EnchantedTiles_0", "EnchantedTiles_1", ...
        // Ama importer bazen base-name kullanmadan sadece slice adini verebilir.
        // Hem "EnchantedTiles_N" hem "{sliceAdi}" varyantlarini dene.
        string[] candidates = {
            $"EnchantedTiles_{index}",
            $"EnchantedTiles {index}",
        };
        foreach (var name in candidates)
            if (sprites.TryGetValue(name, out var s)) return s;

        // Fallback: sirala ve index'e gore al (isim disinda siralama sağlayamazsak)
        if (sprites.Count >= 10)
        {
            var sorted = new List<Sprite>(sprites.Values);
            sorted.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            if (index < sorted.Count) return sorted[index];
        }
        return null;
    }

    private static void CreateOrUpdateTileAsset(string assetPath, Sprite sprite, string color, string slot)
    {
        if (sprite == null)
        {
            Debug.LogWarning($"[MagicTileSetup] Sprite null: {color} {slot}");
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

        Debug.Log($"[MagicTileSetup] {color} {slot} -> {assetPath} (sprite: {sprite.name})");
    }

    private static void AssignToManager()
    {
        MagicTileManager mgr = Object.FindFirstObjectByType<MagicTileManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[MagicTileSetup] MagicTileManager bulunamadi! Sahneye ekle.");
            return;
        }

        var entries = new List<MagicTileManager.MagicTileEntry>();
        foreach (MagicTileType type in System.Enum.GetValues(typeof(MagicTileType)))
        {
            TileBase ground = LoadTile($"{type}MagicUpperTile");
            TileBase column = LoadTile($"{type}MagicTile");

            entries.Add(new MagicTileManager.MagicTileEntry
            {
                type = type,
                groundTile = ground,
                columnTile = column,
                foregroundTile = null, // artik kullanilmiyor
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
