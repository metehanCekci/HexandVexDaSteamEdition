using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeneSplicePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Upgrade a random perk, then consume itself.";
        RebuildDescription();
    }

    public override bool CanBeOffered()
    {
        if (RunManager.instance == null) return false;
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk != this && perk.maxLevel > 1 && perk.currentLevel < perk.maxLevel)
                return true;
        }
        foreach (var perk in RunManager.instance.inventoryPerks)
        {
            if (perk != this && perk.maxLevel > 1 && perk.currentLevel < perk.maxLevel)
                return true;
        }
        return false;
    }

    public override void OnAcquire()
    {
        StartCoroutine(SpliceAndTransform());
    }

    public override void Upgrade()
    {
        base.Upgrade();
        StartCoroutine(SpliceAndTransform());
    }

    private IEnumerator SpliceAndTransform()
    {
        BasePerk targetPerk = null;

        List<BasePerk> upgradablePerks = new List<BasePerk>();
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk != this && perk.maxLevel > 1 && perk.currentLevel < perk.maxLevel)
                upgradablePerks.Add(perk);
        }
        foreach (var perk in RunManager.instance.inventoryPerks)
        {
            if (perk != this && perk.maxLevel > 1 && perk.currentLevel < perk.maxLevel)
                upgradablePerks.Add(perk);
        }

        if (upgradablePerks.Count == 0)
        {
            RemoveSelf();
            yield break;
        }

        targetPerk = upgradablePerks[Random.Range(0, upgradablePerks.Count)];

        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.TriggerSpliceAnimation(this);

        yield return new WaitForSeconds(0.6f);

        if (targetPerk != null)
        {
            targetPerk.Upgrade();
            Debug.Log($"Gene Splice: {targetPerk.perkName} güçlendirildi! (Yeni Seviye: {targetPerk.currentLevel})");

            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerLevelUpAnimForPerk(targetPerk);
            if (ActivePerkBar.instance != null)
                ActivePerkBar.instance.TriggerPopForPerk(targetPerk);
        }

        yield return new WaitForSeconds(0.2f);
        RemoveSelf();
    }

    private void RemoveSelf()
    {
        if (RunManager.instance != null)
        {
            RunManager.instance.activePerks.Remove(this);
            RunManager.instance.inventoryPerks.Remove(this);
            RunManager.instance.RefreshPerkUI();
        }
        Destroy(gameObject);
    }
}
