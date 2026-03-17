using UnityEngine;
using System.Collections;

public enum PerkRarity { Common, Rare, Epic, Legendary, Secret }

public abstract class BasePerk : MonoBehaviour
{
    [Header("Seviye Sistemi")]
    public int currentLevel = 1;
    public int maxLevel = 3;
    public string perkName;
    [TextArea] public string description;
    public Sprite icon;
    public int priority = 0;
    public bool isRerollPerk = false;

    [Header("Rarity")]
    public PerkRarity rarity = PerkRarity.Common;

    // Perk havuzdan çekilirken gösterilebilir mi? (GeneSplice gibi koşullu perkler override eder)
    public virtual bool CanBeOffered() { return true; }

    // 1. Perk satın alındığında / seçildiğinde 1 kez çalışır
    public virtual void OnAcquire() { }

    // 2. Her saldırı yapıldığında, hasar hesaplanırken çalışır
    public virtual void ModifyCombat(CombatPayload payload) { }

    // 3. Tur geçildiğinde (Skip) çalışır
    public virtual void OnSkip() { }

    // Her yeni levele/odaya geçildiğinde çalışır
    public virtual void OnLevelStart() { }

    // Level temizlendiğinde (tüm düşmanlar öldüğünde) çalışır
    public virtual void OnLevelClear() { }

    // Düşman öldüğünde çalışır
    public virtual void OnEnemyKilled(EnemyAI enemy) { }

    // Shop reroll yapıldığında çalışır
    public virtual void OnShopReroll() { }

    // Perk aktif slotlara taşındığında çalışır
    public virtual void OnEquip() { }

    // Perk envanterden (stash) alana taşındığında çalışır
    public virtual void OnUnequip() { }

    // ======================================================
    // İŞTE YENİ EKLENEN KISIM BURASI KANKA:
    // Ancient Blessing bu komutu çağıracak. Diğer perkler de bu komutu alınca ne yapacaklarını bilecek.
    public virtual void UpgradePerk() { }

    public virtual void Upgrade()
    {
        if (currentLevel >= maxLevel) return;
        currentLevel++;
        Debug.Log($"{perkName} seviye atladı! Yeni Seviye: {currentLevel}");
    }
    // ======================================================

    // Gorsel geri bildirim: Perk calistiginda ekranda ziplar
    public void TriggerVisualPop()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(PopAnimation());

        // ActivePerkBar'da da ikon animasyonu tetikle
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.TriggerPopForPerk(this);
    }

    private IEnumerator PopAnimation()
    {
        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();
        CameraController.ShakeLight();
        Transform tr = transform;
        Vector3 endScale = Vector3.one;

        float duration = 0.12f;
        float elapsed = 0f;

        tr.localScale = new Vector3(1.5f, 1.5f, 1.5f);

        while (elapsed < duration)
        {
            float tParam = elapsed / duration;
            tParam = 1f - (1f - tParam) * (1f - tParam);
            tr.localScale = Vector3.Lerp(new Vector3(1.5f, 1.5f, 1.5f), endScale, tParam);
            elapsed += Time.deltaTime;
            yield return null;
        }
        tr.localScale = endScale;
    }
}