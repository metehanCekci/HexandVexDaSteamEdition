using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeneSplicePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
    }

    public override bool CanBeOffered()
    {
        if (RunManager.instance == null) return true;
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk != this && perk.maxLevel > 1)
                return true;
        }
        return RunManager.instance.activePerks.Count == 0;
    }

    public override void OnAcquire()
    {
        // GeneSplice alındığında: kısa süre gözükür, sonra animasyonla parçalanıp
        // dönüştüğü perke dönüşür ve kendisi yok olur
        StartCoroutine(SpliceAndTransform());
    }

    public override void Upgrade()
    {
        base.Upgrade();
        StartCoroutine(SpliceAndTransform());
    }

    private IEnumerator SpliceAndTransform()
    {
        // Önce hangi perki vereceğimizi belirle
        BasePerk targetPerk = null;
        GameObject newPerkPrefab = null;

        List<BasePerk> upgradablePerks = new List<BasePerk>();
        foreach (var perk in RunManager.instance.activePerks)
        {
            if (perk != this && perk.maxLevel > 1 && perk.currentLevel < perk.maxLevel)
                upgradablePerks.Add(perk);
        }

        if (upgradablePerks.Count > 0)
        {
            // Mevcut bir perki upgrade et
            targetPerk = upgradablePerks[Random.Range(0, upgradablePerks.Count)];
        }
        else
        {
            // Yeni perk ver
            if (LevelUpManager.instance != null)
                newPerkPrefab = LevelUpManager.instance.GetRandomPerkByRarity(false);
        }

        // ActivePerkBar'da GeneSplice ikonunu bul ve parçalanma animasyonu oynat
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.TriggerSpliceAnimation(this);

        // Animasyon süresi boyunca bekle
        yield return new WaitForSeconds(0.6f);

        // Efekti uygula
        if (targetPerk != null)
        {
            targetPerk.Upgrade();
            Debug.Log($"✨ Gene Splice: {targetPerk.perkName} güçlendirildi! (Yeni Seviye: {targetPerk.currentLevel})");

            // Hedef perkte parlama animasyonu
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerLevelUpAnimForPerk(targetPerk);
            if (ActivePerkBar.instance != null)
                ActivePerkBar.instance.TriggerPopForPerk(targetPerk);
        }
        else if (newPerkPrefab != null)
        {
            Debug.Log("⚠️ Güçlendirilecek yetenek yok, Gene Splice yeni bir yetenek hediye ediyor!");
            RunManager.instance.AddPerk(newPerkPrefab);
        }

        // GeneSplice'ı listeden kaldır ve yok et
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
