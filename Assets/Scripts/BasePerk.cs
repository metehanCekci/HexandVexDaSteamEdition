using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public enum PerkRarity { Common, Rare, Epic, Legendary, Secret }

public abstract class BasePerk : MonoBehaviour
{
    [Header("Seviye Sistemi")]
    public int currentLevel = 1;
    public int maxLevel = 3;
    public string perkName;
    [TextArea] public string description;
    // Inspector'da girilen orijinal template (token'li). Runtime'da `description` overwrite edilse bile
    // token'lar korunur ki RebuildDescription tekrar cagrilabilsin.
    [System.NonSerialized] private string _descriptionTemplate;
    public Sprite icon;
    public int priority = 0;
    public bool isRerollPerk = false;
    /// <summary>true ise bu perk diger perklerin ModifyCombat'i bittikten sonra islenir.</summary>
    public bool processLast = false;

    /// <summary>
    /// true ise bu perk baska perklerin efektlerini tekrar tetikler (Mimetic Growth, Leftmost Resonance, Parasitic Chorus...).
    /// Retrigger perkleri diger retrigger perklerini retriggerlayamaz (infinite loop korumasi).
    /// Dice retrigger perkleri (Stelzer, Hanging Nerve, Sensory Overload) bunu ISARETLEMEZ — onlar zarlari retriggerlar, perkleri degil.
    /// </summary>
    public bool isPerkRetrigger = false;

    [Header("Rarity")]
    public PerkRarity rarity = PerkRarity.Common;

    // Perk havuzdan çekilirken gösterilebilir mi? (GeneSplice gibi koşullu perkler override eder)
    public virtual bool CanBeOffered() { return true; }

    /// <summary>
    /// Perk su an calisabilir durumda mi? (Ornegin en sagdaki Mimetic Growth'un kopyalayacagi komsusu yok)
    /// false donerse UI "INCOMPATIBLE" etiketi gosterir ve perk ModifyCombat'a skip edilir.
    /// </summary>
    public virtual bool IsIncompatible() { return false; }

    /// <summary>
    /// IsIncompatible() true oldugunda tooltip'te gosterilecek kisa sebep.
    /// </summary>
    public virtual string GetIncompatibleReason() { return "Incompatible"; }

    /// <summary>
    /// Tek bir zar islendikten sonra cagrilir. Dice retrigger perkleri burada zarin tekrar
    /// islenip islenmeyecegine karar verir. Geri donus degeri: bu zar icin kac ekstra retrigger
    /// yapilacak (0 = retrigger yok).
    /// </summary>
    public virtual int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload) { return 0; }

    // 1. Perk satın alındığında / seçildiğinde 1 kez çalışır
    public virtual void OnAcquire() { }

    // 2. Her saldırı yapıldığında, hasar hesaplanırken çalışır
    public virtual void ModifyCombat(CombatPayload payload) { }

    // 2b. ModifyCombat sonrası adım adım animasyonlu efekt (CarrionFeeder gibi perkler için)
    public virtual IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI) { yield break; }

    // 3. Tur geçildiğinde (Skip) çalışır
    public virtual void OnSkip() { }

    // Her yeni levele/odaya geçildiğinde çalışır
    public virtual void OnLevelStart() { }

    // Level temizlendiğinde (tüm düşmanlar öldüğünde) çalışır
    public virtual void OnLevelClear() { }

    // Düşman öldüğünde çalışır
    public virtual void OnEnemyKilled(EnemyMovement enemy) { }

    // Shop reroll yapıldığında çalışır
    public virtual void OnShopReroll() { }

    // LetsGoAgain perki tetiklendiğinde çalışır (ModifyCombat dışı ekstra efektler)
    public virtual void OnLetsGoAgain() { }

    // Perk aktif slotlara taşındığında çalışır
    public virtual void OnEquip() { }

    // Perk envanterden (stash) alana taşındığında çalışır
    public virtual void OnUnequip() { }

    /// <summary>Perk çıkarılabilir mi? false dönerse unequip/swap engellenir.</summary>
    public virtual bool CanUnequip() { return true; }

    // ======================================================
    // İŞTE YENİ EKLENEN KISIM BURASI KANKA:
    // Ancient Blessing bu komutu çağıracak. Diğer perkler de bu komutu alınca ne yapacaklarını bilecek.
    public virtual void UpgradePerk() { }

    // Inspector'da description'a token yazar (orn: "Deal {mult} damage, gain {goldPerKill}.")
    // Perk bu metodu override edip token -> deger esleme dondurur.
    // Deger formatina gore otomatik renklendirilir:
    //   "xN"       -> MultHex  (kirmizi)
    //   "+N" / "N" -> ChipsHex (mavi)
    //   icinde "gold" -> GoldHex (sari) (+N gold ise hem + hem gold tonu, gold wrap oncelikli)
    //   "N HP"     -> HealHex
    //   "N damage" -> DamageHex
    public virtual Dictionary<string, object> GetDescValues() { return null; }

    // Returns GetDescValues() as if currentLevel were `level`. Used for level-up diff UI.
    public Dictionary<string, object> GetDescValuesForLevel(int level)
    {
        int saved = currentLevel;
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        var result = GetDescValues();
        currentLevel = saved;
        return result;
    }

    // Public wrapper so UI code can reuse the same colorization pipeline for diff values.
    public static string ColorizeValue(object raw) => Colorize(raw);

    // Description'i yeniden insa eder (seviye atlayinca veya GameKeywords renk/deger degisince cagrilir)
    public virtual void RebuildDescription()
    {
        if (string.IsNullOrEmpty(_descriptionTemplate))
        {
            if (string.IsNullOrEmpty(description)) return;
            _descriptionTemplate = description;
        }

        var values = GetDescValues();
        if (values == null || values.Count == 0)
        {
            // Token yoksa template'i oldugu gibi biraktir.
            description = _descriptionTemplate;
            return;
        }

        description = ApplyTokens(_descriptionTemplate, values);
    }

    private static string ApplyTokens(string template, Dictionary<string, object> values)
    {
        StringBuilder sb = new StringBuilder(template.Length + 64);
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] != '{')
            {
                int end = template.IndexOf('}', i + 1);
                if (end > i)
                {
                    string key = template.Substring(i + 1, end - i - 1);
                    if (values.TryGetValue(key, out object raw))
                    {
                        sb.Append(Colorize(raw));
                        i = end + 1;
                        continue;
                    }
                }
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static string Colorize(object raw)
    {
        if (raw == null) return "";
        string s = raw.ToString();
        if (s.Length == 0) return s;

        string lower = s.ToLowerInvariant();
        string hex;

        // Gold ifadesi varsa sari (oncelikli, "+N gold" dahil)
        if (lower.Contains("gold"))
            hex = UIColors.Gold;
        // "x2", "x1.5" gibi mult ifadeleri -> kirmizi
        else if (s[0] == 'x' || s[0] == 'X')
            hex = UIColors.Mult;
        // "+3", "-1" gibi chips -> mavi
        else if (s[0] == '+' || s[0] == '-')
            hex = UIColors.Chips;
        // "N HP", "heal" -> yesil
        else if (lower.Contains("hp") || lower.Contains("heal"))
            hex = UIColors.Heal;
        // "N damage" -> damage rengi
        else if (lower.Contains("damage"))
            hex = UIColors.Damage;
        // ciplak sayi / % degeri -> chips mavi
        else
            hex = UIColors.Chips;

        return $"<color=#{hex}>{s}</color>";
    }

    public virtual void Upgrade()
    {
        if (currentLevel >= maxLevel) return;
        currentLevel++;
        Debug.Log($"{perkName} seviye atladı! Yeni Seviye: {currentLevel}");
        RebuildDescription();
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