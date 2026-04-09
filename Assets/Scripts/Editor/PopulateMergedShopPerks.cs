using UnityEngine;
using UnityEditor;
using System.IO;

public static class PopulateMergedShopPerks
{
    [MenuItem("Tools/Hex and Vex/Populate MergedShop Perk Lists")]
    public static void Populate()
    {
        var shop = Object.FindObjectOfType<MergedShopManager>(true);
        if (shop == null)
        {
            Debug.LogError("MergedShopManager not found in scene!");
            return;
        }

        shop.commonPerks.Clear();
        shop.rarePerks.Clear();
        shop.epicPerks.Clear();
        shop.legendaryPerks.Clear();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Perks" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BasePerk perk = prefab.GetComponent<BasePerk>();
            if (perk == null) continue;

            switch (perk.rarity)
            {
                case PerkRarity.Common:    shop.commonPerks.Add(prefab); break;
                case PerkRarity.Rare:      shop.rarePerks.Add(prefab); break;
                case PerkRarity.Epic:      shop.epicPerks.Add(prefab); break;
                case PerkRarity.Legendary: shop.legendaryPerks.Add(prefab); break;
                case PerkRarity.Secret:    break; // Secret perks are not in the shop pool
            }
            count++;
        }

        EditorUtility.SetDirty(shop);
        Debug.Log($"[MergedShop] Populated perk lists from {count} prefabs: " +
                  $"Common={shop.commonPerks.Count}, Rare={shop.rarePerks.Count}, " +
                  $"Epic={shop.epicPerks.Count}, Legendary={shop.legendaryPerks.Count}");
    }
}
