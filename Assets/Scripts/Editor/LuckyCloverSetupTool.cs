using UnityEngine;
using UnityEditor;

public static class LuckyCloverSetupTool
{
    [MenuItem("Tools/Setup Lucky Clover Item")]
    public static void Setup()
    {
        // 1. ScriptableObject asset olustur
        string folder = "Assets/ScriptableObjects/Items";
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Items");

        string assetPath = $"{folder}/LuckyClover.asset";
        LuckyClover existing = AssetDatabase.LoadAssetAtPath<LuckyClover>(assetPath);

        if (existing == null)
        {
            LuckyClover item = ScriptableObject.CreateInstance<LuckyClover>();
            item.itemName = "Lucky Clover";
            item.description = "Equalizes all perk rarity chances on the next perk selection screen. Use during perk selection to re-roll with equal odds.";
            item.price = 8;
            item.itemType = ItemType.Consumable;
            AssetDatabase.CreateAsset(item, assetPath);
            AssetDatabase.SaveAssets();
            existing = item;
            Debug.Log("LuckyClover asset olusturuldu: " + assetPath);
        }
        else
        {
            Debug.Log("LuckyClover asset zaten mevcut: " + assetPath);
        }

        // 2. Shopmanager'in itemPool'una ekle
        Shopmanager shop = Object.FindFirstObjectByType<Shopmanager>();
        if (shop != null)
        {
            if (!shop.itemPool.Contains(existing))
            {
                shop.itemPool.Add(existing);
                EditorUtility.SetDirty(shop);
                Debug.Log("LuckyClover Shopmanager itemPool'una eklendi.");
            }
            else
            {
                Debug.Log("LuckyClover zaten itemPool'da mevcut.");
            }
        }
        else
        {
            Debug.LogWarning("Sahnede Shopmanager bulunamadi — item pool'a manuel eklemen gerekebilir.");
        }

        // 3. ShopDealer varsa onun icin de kontrol et
        ShopDealer dealer = Object.FindFirstObjectByType<ShopDealer>();
        if (dealer != null)
        {
            // ShopDealer, Shopmanager'in pool'unu kullanir — ekstra islem gerekmez
            Debug.Log("ShopDealer mevcut — Shopmanager pool'unu paylasir, ek islem gerekmez.");
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Selection.activeObject = existing;
        Debug.Log("=== Lucky Clover kurulumu tamamlandi! ===");
    }
}
