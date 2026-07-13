using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool: Tools → Setup Shop Dealer
/// Creates a ShopDealer prefab and assigns it to LevelGenerator.shopDealerPrefab.
/// You can then tweak the prefab's sprite, color, animation settings in the Inspector.
/// </summary>
public static class ShopDealerSetupTool
{
    [MenuItem("Tools/Setup Shop Dealer")]
    public static void SetupShopDealer()
    {
        // Ensure Prefabs folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        string prefabPath = "Assets/Prefabs/ShopDealer.prefab";

        // Create the dealer GameObject
        GameObject dealerGO = new GameObject("ShopDealer");

        // SpriteRenderer
        SpriteRenderer sr = dealerGO.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.85f, 0.2f, 1f);
        sr.sortingOrder = 5;

        // ShopDealer component
        ShopDealer dealer = dealerGO.AddComponent<ShopDealer>();
        dealer.dealerColor = new Color(1f, 0.85f, 0.2f, 1f);
        dealer.bounceAmplitude = 0.05f;
        dealer.bounceSpeed = 2f;
        dealer.sortingOrder = 5;

        // Delete old prefab if exists
        AssetDatabase.DeleteAsset(prefabPath);

        // Save as prefab
        bool success;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(dealerGO, prefabPath, out success);
        Object.DestroyImmediate(dealerGO);

        if (!success)
        {
            Debug.LogError("ShopDealer prefab kaydedilemedi: " + prefabPath);
            return;
        }

        Debug.Log("ShopDealer prefab oluşturuldu: " + prefabPath);

        // shopDealerPrefab artık LevelGenerator'dan kaldırıldı — sadece log bas
        Debug.Log($"ShopDealer prefab oluşturuldu: {prefab.name}. GenerateShopArena direkt placeholder kullandığı için manuel atama gerekmez.");

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // Select the prefab for editing
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log("=== Shop Dealer kurulumu tamamlandı! Prefab'ı seçip sprite atayabilirsiniz. ===");
    }
}
