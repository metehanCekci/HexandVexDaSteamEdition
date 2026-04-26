using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Invisible Joker — Epic. Balatro adaptasyonu.
/// 2 combat boyunca pasif olarak bekler (armed degil). 2. combat temizlendiginde "armed" olur.
/// Armed'ken oyuncu BASKA bir perki sattiginda: sahip oldugu (active + inventory) tum perkler
/// arasindan rastgele birinin kopyasini ekler ve kendini yok eder. Showman gerekmez —
/// duplicate iznini kendisi forceler (allowDuplicatePerk flag).
/// </summary>
public class InvisibleJokerPerk : BasePerk
{
    private const int CombatsRequired = 2;
    public int combatsPassed = 0;
    public bool armed = false;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "After {wait}, sell another implant to {effect}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "wait", armed ? "armed" : (CombatsRequired - combatsPassed) + " combats" },
        { "effect", "copy a random implant you own" }
    };

    public override void OnAcquire()
    {
        GameEvents.OnPerkSold += HandlePerkSold;
    }

    void OnDestroy()
    {
        GameEvents.OnPerkSold -= HandlePerkSold;
    }

    public override void OnLevelClear()
    {
        if (armed) return;
        combatsPassed++;
        if (combatsPassed >= CombatsRequired)
        {
            armed = true;
            TriggerVisualPop();
        }
        RebuildDescription();
    }

    private void HandlePerkSold(BasePerk soldPerk)
    {
        if (!armed) return;
        if (soldPerk == this) return; // kendini satarsa tetikleme
        StartCoroutine(CopyRoutine());
    }

    private IEnumerator CopyRoutine()
    {
        // 1 frame bekle ki sell akisi tamamen bitsin (Destroy + listeden cikar)
        yield return null;
        if (RunManager.instance == null) yield break;
        if (MergedShopManager.instance == null) yield break;

        // Sahip oldugumuz tum perk tipleri (kendimiz haric)
        HashSet<System.Type> ownedTypes = new HashSet<System.Type>();
        foreach (var p in RunManager.instance.activePerks)
            if (p != null && p != this) ownedTypes.Add(p.GetType());
        foreach (var p in RunManager.instance.inventoryPerks)
            if (p != null && p != this) ownedTypes.Add(p.GetType());

        if (ownedTypes.Count == 0) { SelfDestruct(); yield break; }

        // Bu tiplerin prefab'larini havuzlardan bul
        List<GameObject> candidatePrefabs = new List<GameObject>();
        var ms = MergedShopManager.instance;
        AddMatchingPrefabs(ms.commonPerks, ownedTypes, candidatePrefabs);
        AddMatchingPrefabs(ms.rarePerks, ownedTypes, candidatePrefabs);
        AddMatchingPrefabs(ms.epicPerks, ownedTypes, candidatePrefabs);
        AddMatchingPrefabs(ms.legendaryPerks, ownedTypes, candidatePrefabs);

        if (candidatePrefabs.Count == 0) { SelfDestruct(); yield break; }

        var chosen = candidatePrefabs[Random.Range(0, candidatePrefabs.Count)];

        // Showman olmadan da duplicate olarak eklensin diye one-shot flag
        RunManager.instance.allowDuplicatePerk = true;
        RunManager.instance.AddPerk(chosen);

        TriggerVisualPop();
        SelfDestruct();
    }

    private static void AddMatchingPrefabs(List<GameObject> pool, HashSet<System.Type> types, List<GameObject> output)
    {
        if (pool == null) return;
        foreach (var prefab in pool)
        {
            if (prefab == null) continue;
            var script = prefab.GetComponent<BasePerk>();
            if (script != null && types.Contains(script.GetType())) output.Add(prefab);
        }
    }

    private void SelfDestruct()
    {
        if (RunManager.instance == null) return;
        RunManager.instance.activePerks.Remove(this);
        RunManager.instance.inventoryPerks.Remove(this);
        OnUnequip();
        RunManager.instance.RefreshPerkUI();
        Destroy(gameObject);
    }
}
