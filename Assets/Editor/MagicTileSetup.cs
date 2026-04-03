using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using System.IO;

public class MagicTileSetup
{
    [MenuItem("Tools/Hex and Vex/Setup Magic Tiles (Full)")]
    public static void SetupAll()
    {
        CreateTileAssets();
        UpdateLayerDataAssets();
        Debug.Log("[MagicTileSetup] Full setup complete!");
        EditorUtility.DisplayDialog("Magic Tile Setup",
            "Setup complete!\n\n" +
            "1. Tile assets created in Assets/Resources/\n" +
            "2. MapLayerData assets updated (enchantChance = 0.10)\n\n" +
            "You can now play-test the Enchant system.",
            "OK");
    }

    // ─────────────────────────────────────────────
    // 1. Sprite'lardan Tile asset'leri oluştur
    // ─────────────────────────────────────────────

    [MenuItem("Tools/Hex and Vex/Create Magic Tile Assets")]
    public static void CreateTileAssets()
    {
        string resourcesDir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        string[] names = { "RedMagicTile", "BlueMagicTile", "GreenMagicTile", "YellowMagicTile", "OrangeMagicTile" };
        string spritePath = "Assets/Sprites/Tiles";
        int created = 0;

        foreach (string name in names)
        {
            string tilePath = $"{resourcesDir}/{name}.asset";

            // Zaten varsa atla
            if (AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null)
            {
                Debug.Log($"[MagicTileSetup] {name} already exists, skipping.");
                continue;
            }

            // Sprite'i bul — aseprite import'u sprite uretir
            Sprite sprite = FindSpriteForTile(spritePath, name);

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;

            AssetDatabase.CreateAsset(tile, tilePath);
            created++;

            if (sprite != null)
                Debug.Log($"[MagicTileSetup] Created {tilePath} with sprite '{sprite.name}'");
            else
                Debug.LogWarning($"[MagicTileSetup] Created {tilePath} WITHOUT sprite — assign manually in Inspector: {tilePath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MagicTileSetup] {created} tile asset(s) created in {resourcesDir}/");
    }

    private static Sprite FindSpriteForTile(string folder, string tileName)
    {
        // tileName = "RedMagicTile" → sprite dosyası "RedMagicTile.aseprite" veya "RedMagicTile.png"
        // Aseprite import sonrası sprite sub-asset olarak gelir

        // Aseprite dosyasini dene
        string asepritePath = $"{folder}/{tileName}.aseprite";
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
        if (subAssets != null)
        {
            foreach (var obj in subAssets)
            {
                if (obj is Sprite s) return s;
            }
        }

        // PNG/JPG dene
        string[] exts = { ".png", ".jpg", ".tga", ".psd" };
        foreach (string ext in exts)
        {
            string path = $"{folder}/{tileName}{ext}";
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;
        }

        // Son care: tum Assets/Sprites altinda ara
        string[] guids = AssetDatabase.FindAssets($"t:Sprite {tileName}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            // Sub-asset olabilir
            Object[] allSub = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in allSub)
            {
                if (obj is Sprite subSp) return subSp;
            }
        }

        return null;
    }

    // ─────────────────────────────────────────────
    // 2. MapLayerData asset'lerinde enchantChance ayarla
    // ─────────────────────────────────────────────

    // ─────────────────────────────────────────────
    // 3. EnchantNodeUI + Canvas'ı sahneye ekle
    // ─────────────────────────────────────────────

    [MenuItem("Tools/Hex and Vex/Add Enchant Canvas to Scene")]
    public static void AddEnchantCanvas()
    {
        // Zaten varsa uyar
        if (Object.FindFirstObjectByType<EnchantNodeUI>() != null)
        {
            EditorUtility.DisplayDialog("Enchant Canvas", "EnchantNodeUI already exists in the scene.", "OK");
            return;
        }

        GameObject go = new GameObject("EnchantNodeUI");
        go.AddComponent<EnchantNodeUI>();
        Undo.RegisterCreatedObjectUndo(go, "Add EnchantNodeUI");
        UnityEditor.Selection.activeGameObject = go;

        Debug.Log("[MagicTileSetup] EnchantNodeUI added to scene. Canvas will be built at runtime on first Show().");
        EditorUtility.DisplayDialog("Enchant Canvas",
            "EnchantNodeUI GameObject added to the scene.\n\n" +
            "The canvas is built procedurally at runtime when Show() is first called.",
            "OK");
    }

    [MenuItem("Tools/Hex and Vex/Update Layer Data (Enchant Chance)")]
    public static void UpdateLayerDataAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:MapLayerData");
        int updated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MapLayerData data = AssetDatabase.LoadAssetAtPath<MapLayerData>(path);
            if (data == null) continue;

            // enchantChance 0 ise (eski eventChance'ten kalan default) 0.10'a set et
            if (data.enchantChance < 0.01f)
            {
                data.enchantChance = 0.10f;
                EditorUtility.SetDirty(data);
                updated++;
                Debug.Log($"[MagicTileSetup] Updated {path}: enchantChance = 0.10");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MagicTileSetup] {updated} MapLayerData asset(s) updated.");
    }
}
