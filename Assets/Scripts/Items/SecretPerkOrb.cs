using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/SecretPerkOrb", fileName = "SecretPerkOrb")]
public class SecretPerkOrb : BaseItem
{
    [Header("Secret Perk Havuzu")]
    public List<GameObject> secretPerkPool = new List<GameObject>();

    void OnEnable()
    {
        itemName = "??? Orb";
        description = $"A mysterious orb pulsing with unknown energy. Grants a <color=#{UIColors.GetRarityHex(PerkRarity.Secret)}>secret mutation</color>.";
        price = 404;
        itemType = ItemType.Instant;
    }

    /// <summary>
    /// Verilecek secret perk kaldı mı? Shop bu metodu kontrol eder.
    /// </summary>
    public bool HasAvailableSecrets()
    {
        if (RunManager.instance == null || secretPerkPool.Count == 0) return false;

        foreach (var perkPrefab in secretPerkPool)
        {
            if (perkPrefab == null) continue;
            BasePerk prefabScript = perkPrefab.GetComponent<BasePerk>();
            if (prefabScript == null) continue;

            BasePerk existing = RunManager.instance.activePerks.Find(p => p.GetType() == prefabScript.GetType());
            if (existing == null || existing.currentLevel < existing.maxLevel) return true;
        }
        return false;
    }

    public override bool Use()
    {
        Debug.Log($"[SecretPerkOrb] Use() called. RunManager={RunManager.instance != null}, poolCount={secretPerkPool?.Count ?? -1}");

        if (RunManager.instance == null || secretPerkPool == null || secretPerkPool.Count == 0)
        {
            Debug.LogWarning($"[SecretPerkOrb] Early exit: RunManager={RunManager.instance != null}, pool={secretPerkPool?.Count ?? -1}");
            return false;
        }

        List<GameObject> available = new List<GameObject>();
        foreach (var perkPrefab in secretPerkPool)
        {
            if (perkPrefab == null) { Debug.Log("[SecretPerkOrb] pool entry is NULL, skipping"); continue; }
            BasePerk prefabScript = perkPrefab.GetComponent<BasePerk>();
            if (prefabScript == null) { Debug.Log($"[SecretPerkOrb] {perkPrefab.name} has no BasePerk, skipping"); continue; }

            BasePerk existing = RunManager.instance.activePerks.Find(p => p.GetType() == prefabScript.GetType());
            if (existing != null && existing.currentLevel >= existing.maxLevel)
            {
                Debug.Log($"[SecretPerkOrb] {prefabScript.perkName} already maxed, skipping");
                continue;
            }

            available.Add(perkPrefab);
        }

        Debug.Log($"[SecretPerkOrb] available={available.Count}");

        if (available.Count == 0) return false;

        // Rastgele bir secret perk seç
        GameObject chosen = available[Random.Range(0, available.Count)];
        BasePerk chosenPerk = chosen.GetComponent<BasePerk>();
        Debug.Log($"[SecretPerkOrb] Chosen: {chosenPerk?.perkName ?? chosen.name}");

        // Sinematik animasyon varsa: önce animasyonu göster, sonra perk'i ver
        if (SecretPerkCinematic.instance != null)
        {
            SecretPerkCinematic.instance.Play(chosenPerk);
        }

        RunManager.instance.AddPerk(chosen);
        Debug.Log($"[SecretPerkOrb] AddPerk done for {chosenPerk?.perkName ?? chosen.name}");
        return true;
    }
}
