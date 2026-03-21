using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CreateNewPerkPrefabs
{
    [MenuItem("HexAndVex/Create New Perk Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePerk<VolatileRollPerk>("VolatileRoll", "Volatile Roll",
            "Her zar %50 şansla 1 veya 6 olur. Yüksek risk, yüksek ödül.");

        CreatePerk<SporeCloudPerk>("SporeCloud", "Spore Cloud",
            "Hasar aldığında etrafındaki düşmanları stunlar. Duman yarıçapı level ile büyür.");

        CreatePerk<PerkLeechPerk>("PerkLeech", "Perk Leech",
            "Elite düşman öldürdüğünde bir perk parçası kazan. 3 parça = rastgele perk.");

        CreatePerk<PyrogenicGlandsPerk>("PyrogenicGlands", "Pyrogenic Glands",
            "Saldırıların düşmanları yakar. 2 tur boyunca hasar verir.");

        CreatePerk<HydraulicImpactPerk>("HydraulicImpact", "Hydraulic Impact",
            "Knockback ile duvara çarpan düşmanlar max HP'lerinin %50'si kadar hasar alır.");

        CreatePerk<SlipperySecretionPerk>("SlipperySecretion", "Slippery Secretion",
            "Geçtiğin hücrelerde mucus izi bırakır. Düşmanlar giriş yönünde ekstra 1 hex kayar.");

        CreatePerk<ViralCystsPerk>("ViralCysts", "Viral Cysts",
            "Saldırı düşmana cyst yerleştirir. Skip ile patlat. Hasar işaretli düşman sayısıyla artar.");

        CreatePerk<HypertrophicShellPerk>("HypertrophicShell", "Hypertrophic Shell",
            "Max HP'ni kalıcı olarak +1 artırır. (Maks: 10 HP)");

        CreatePerk<SynapticAnchorPerk>("SynapticAnchor", "Synaptic Anchor",
            "İlk Skip pozisyonuna anchor koyar. İkinci Skip anchor'a teleport eder.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("9 yeni perk prefab'ı oluşturuldu: Assets/Prefabs/Perks/");
    }

    [MenuItem("HexAndVex/Create New Perk Collection Entries")]
    public static void CreateCollectionEntries()
    {
        // Load database
        PerkCollectionDatabase db = AssetDatabase.LoadAssetAtPath<PerkCollectionDatabase>(
            "Assets/Resources/PerkCollectionDatabase.asset");
        if (db == null)
        {
            Debug.LogError("PerkCollectionDatabase bulunamadı!");
            return;
        }

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources/CollectionEntries"))
            AssetDatabase.CreateFolder("Assets/Resources", "CollectionEntries");

        // Get existing perkIds to avoid duplicates
        HashSet<string> existingIds = new HashSet<string>();
        foreach (var entry in db.entries)
        {
            if (entry != null) existingIds.Add(entry.perkId);
        }

        int added = 0;

        added += CreateEntry(db, existingIds, "VolatileRoll", "Volatile Roll", PerkRarity.Rare,
            "Dice that defy probability — each face is either salvation or doom. The organism's neural pathways have been rewired to embrace chaos.",
            UnlockCondition.Default, 1, "", "Başlangıçta açık.");

        added += CreateEntry(db, existingIds, "SporeCloud", "Spore Cloud", PerkRarity.Rare,
            "A defensive fungal colony embedded in the host's epidermis. When threatened, it releases paralytic spores in a dense cloud.",
            UnlockCondition.KillEnemies, 100, "", "Toplam 100 düşman öldür.");

        added += CreateEntry(db, existingIds, "PerkLeech", "Perk Leech", PerkRarity.Legendary,
            "A parasitic organism that feeds on the essence of powerful foes. Each elite kill leaves behind a crystallized fragment of their strength.",
            UnlockCondition.KillEnemiesSingleRun, 20, "", "Tek run'da 20 düşman öldür.");

        added += CreateEntry(db, existingIds, "PyrogenicGlands", "Pyrogenic Glands", PerkRarity.Common,
            "Bio-engineered glands that coat weapons in an exothermic compound. The flames linger, consuming flesh over time.",
            UnlockCondition.Default, 1, "", "Başlangıçta açık.");

        added += CreateEntry(db, existingIds, "HydraulicImpact", "Hydraulic Impact", PerkRarity.Epic,
            "Hydraulic augmentation that amplifies knockback force exponentially. When a target has nowhere to go, the impact is devastating.",
            UnlockCondition.PushEnemyIntoSpike, 10, "", "Toplam 10 düşmanı spike'a it.");

        added += CreateEntry(db, existingIds, "SlipperySecretion", "Slippery Secretion", PerkRarity.Rare,
            "A mucus-producing organ that leaves a slick, frictionless trail. Enemies who step on it lose their footing and slide uncontrollably.",
            UnlockCondition.MoveTiles, 500, "", "Toplam 500 tile yürü.");

        added += CreateEntry(db, existingIds, "ViralCysts", "Viral Cysts", PerkRarity.Epic,
            "Weaponized bio-cysts that embed themselves in enemy tissue. When detonated, the chain reaction scales with the number of infected hosts.",
            UnlockCondition.SkipTurns, 50, "", "Toplam 50 kez tur atla.");

        added += CreateEntry(db, existingIds, "HypertrophicShell", "Hypertrophic Shell", PerkRarity.Common,
            "A calcified growth that reinforces the host's vital organs. Each layer adds another wall between you and death.",
            UnlockCondition.CompleteRuns, 3, "", "3 run tamamla.");

        added += CreateEntry(db, existingIds, "SynapticAnchor", "Synaptic Anchor", PerkRarity.Legendary,
            "A neural implant that creates a quantum entanglement between two points in space. The anchor remembers, and the body follows.",
            UnlockCondition.ClearLevels, 30, "", "Toplam 30 level temizle.");

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"{added} yeni collection entry oluşturuldu ve database'e eklendi.");
    }

    private static int CreateEntry(PerkCollectionDatabase db, HashSet<string> existingIds,
        string perkId, string perkName, PerkRarity rarity, string loreText,
        UnlockCondition condition, int requiredAmount, string conditionParam, string unlockHint)
    {
        if (existingIds.Contains(perkId))
        {
            Debug.Log($"[SKIP] {perkId} collection entry zaten mevcut.");
            return 0;
        }

        // Load perk prefab
        string prefabPath = $"Assets/Prefabs/Perks/{perkId}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WARN] {perkId} prefab bulunamadı: {prefabPath}");
            return 0;
        }

        // Create ScriptableObject
        PerkCollectionData data = ScriptableObject.CreateInstance<PerkCollectionData>();
        data.perkPrefab = prefab;
        data.perkId = perkId;
        data.loreText = loreText;
        data.rarity = rarity;
        data.unlockCondition = condition;
        data.requiredAmount = requiredAmount;
        data.conditionParam = conditionParam;
        data.unlockHint = unlockHint;

        string assetPath = $"Assets/Resources/CollectionEntries/{perkId}_CollectionEntry.asset";
        AssetDatabase.CreateAsset(data, assetPath);

        // Add to database
        db.entries.Add(data);
        existingIds.Add(perkId);

        Debug.Log($"[OK] {perkId} collection entry oluşturuldu.");
        return 1;
    }

    private static void CreatePerk<T>(string fileName, string perkName, string description) where T : BasePerk
    {
        string path = $"Assets/Prefabs/Perks/{fileName}.prefab";

        // Zaten varsa atla
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"[SKIP] {fileName} zaten mevcut.");
            return;
        }

        GameObject obj = new GameObject(fileName);
        T perk = obj.AddComponent<T>();
        perk.perkName = perkName;
        perk.description = description;

        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);

        Debug.Log($"[OK] {fileName} prefab oluşturuldu.");
    }
}
