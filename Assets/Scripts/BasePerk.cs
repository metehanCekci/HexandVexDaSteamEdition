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

    // ============================================================================
    // DESCRIPTION TEMPLATE (Inspector'dan yazilir, kod karismaz)
    // ============================================================================
    // Bu field perk'in aciklama sablonudur. Inspector'da diledigin gibi yaz, kod ezmez.
    //
    // ── INLINE HIGHLIGHT TAG'LERI (RENK ADI ile, kolay okunur) ──
    //   [white]skip[/white]         -> beyaz+bold (skip/kill/attack/push/level/dodge/shield/stun/spike/etc)
    //   [orange]burn[/orange]       -> turuncu+bold (SADECE burn/fire)
    //   [purple]retriggers[/purple] -> mor+bold (sadece "retriggers" kelimesi)
    //   [red]X4[/red]               -> kirmizi (mult, X carpan)
    //   [blue]+5 damage[/blue]      -> mavi (chips, +damage)
    //   [yellow]5 gold[/yellow]     -> sari (gold)
    //   [green]5 HP[/green]         -> yesil (HP/heal)
    //
    // Ornek inline kullanim (Inspector'a yaz):
    //   "Each consecutive [white]kill[/white] doubles damage. Streak: {streak}"
    //   "Attacks [orange]burn[/orange] enemies for {dmg} per turn."
    //
    // ── TOKEN'lar (dinamik degerler icin, GetDescValues doldurur) ──
    // Icine TOKEN'lar koyabilirsin: { token_adi } seklinde. Token'lari perk'in
    // GetDescValues() metodu doldurur ve renkleri otomatik gelir.
    //
    // ----------------- HAZIR HELPER'LAR (perkin GetDescValues icinde kullanilir) -----------------
    // RENK KURALI: sadece SAYI/DEGER renkli, suffix beyaz. Bazi helper'lar tum kelimeyi renkler.
    //
    // MAVI (zara/damage'a EKLEME):
    //   GameKeywords.Plus(5, "damage")        -> "+5 damage" (sadece +5 mavi)
    //   GameKeywords.PlusF(1.5f, "damage")    -> "+1.5 damage"
    //   GameKeywords.Minus(3)                 -> "-3"
    //
    // KIRMIZI (carpan / kritik):
    //   GameKeywords.Mult(2, "damage")        -> "X2 damage" (sadece X2 kirmizi)
    //   GameKeywords.Mult(1.5f)               -> "X1.5"
    //   GameKeywords.Crit("Critical Hit")     -> "Critical Hit" (tum kelime kirmizi)
    //   GameKeywords.CritPlus(25, "crit chance") -> "+25% crit chance" (sadece +25% kirmizi)
    //
    // SARI (gold):
    //   GameKeywords.Gold(5)                  -> "5 gold"
    //   GameKeywords.PlusGold(2)              -> "+2 gold"
    //   GameKeywords.GoldText("free")         -> "free" (tum kelime sari)
    //
    // YESIL (HP / heal):
    //   GameKeywords.Hp(5)                    -> "5 HP"
    //   GameKeywords.Heal(5)                  -> "heal 5 HP"
    //   GameKeywords.HealthText("max HP")     -> "max HP" (tum kelime yesil)
    //
    // MOR + bold (RETRIGGER mekanigi — Hanging Nerve, Mimetic gibi):
    //   GameKeywords.Retrigger("retriggers twice")  -> mor+bold serbest text
    //   GameKeywords.RetriggerN(2)            -> "retriggers 2 more times"
    //
    // TURUNCU + bold (ACTION keyword'leri — skip/kill/attack/push/level cleared/burn):
    //   GameKeywords.Action("skip")           -> "skip" (turuncu+bold)
    //
    // BEYAZ + bold (STATUS / sayaclar — shield/dodge/spike/stun/stack):
    //   GameKeywords.Status("dodge")          -> "dodge" (beyaz+bold)
    //   GameKeywords.Counter("5/30")          -> "5/30" (beyaz+bold sayac)
    //
    // ORNEK DESCRIPTION (Inspector'a yaz):
    //   "Each {kill} grants {gold} per {level}."
    // GetDescValues() icinde:
    //   { "kill",  GameKeywords.Action("kill") }
    //   { "gold",  GameKeywords.PlusGold(2) }
    //   { "level", GameKeywords.Action("level") }
    // ============================================================================
    [TextArea(3, 6)] public string description;
    // Cache (RebuildDescription token'li ham template'i hatirlasin diye).
    [System.NonSerialized] private string _descriptionTemplate;

    /// <summary>
    /// Runtime'da uretilen, renkli/highlight'li description (UI bunu okur).
    /// `description` field'i ASLA dokunulmaz — Inspector'da temiz kalir.
    /// </summary>
    [System.NonSerialized] public string renderedDescription;
    public Sprite icon;
    public int priority = 0;
    public bool isRerollPerk = false;
    /// <summary>true ise bu perk diger perklerin OnAttack'i bittikten sonra islenir (PentUp tarzi).</summary>
    public bool processLast = false;

    /// <summary>
    /// DEPRECATED — yeni event-driven pipeline'da kullanilmiyor.
    /// Geri uyumluluk icin field korunuyor (prefab serialization). Mimetic/Leftmost/Parasitic
    /// artik perk-retrigger isteklerini ctx.RequestPerkReplay() ile yapar.
    /// </summary>
    public bool isPerkRetrigger = false;

    /// <summary>
    /// Bu perk Mimetic/Leftmost/Parasitic tarafindan replay edilebilir mi?
    /// Default true — basit "+X / xY" perkler replayde dogru calisir.
    /// false dondurmek istisna durumlarda gerekir:
    ///   - Perk state tuketiyorsa (PentUpStrike: storedDamage release; CascadeProtocol: accumulatedDamage rotate)
    ///   - Perk runtime resource'a yaziyorsa (FatalSightProtocol: criticalChance -> criticalDamageMultiplier)
    /// Bu perkler kendileri Mimetic/Leftmost ile retriggerlanmaz, ama BAGIMSIZ olarak yine calisir.
    /// </summary>
    public virtual bool CanBeRetriggeredByPerks => true;

    [Header("Rarity")]
    public PerkRarity rarity = PerkRarity.Common;

    // Perk havuzdan çekilirken gösterilebilir mi? (GeneSplice gibi koşullu perkler override eder)
    public virtual bool CanBeOffered() { return true; }

    /// <summary>
    /// Perk su an calisabilir durumda mi? (Ornegin en sagdaki Mimetic Growth'un kopyalayacagi komsusu yok)
    /// false donerse UI "INCOMPATIBLE" etiketi gosterir ve perk OnEvent'e skip edilir.
    /// </summary>
    public virtual bool IsIncompatible() { return false; }

    /// <summary>
    /// IsIncompatible() true oldugunda tooltip'te gosterilecek kisa sebep.
    /// </summary>
    public virtual string GetIncompatibleReason() { return "Incompatible"; }

    // 1. Perk satın alındığında / seçildiğinde 1 kez çalışır
    public virtual void OnAcquire() { }

    // ======================================================
    // EVENT-DRIVEN PIPELINE ENTRY POINT (Balatro-style)
    // ======================================================
    // Tum combat event'leri bu metoddan akar. Perk ctx.eventType'a bakarak
    // ne yapacagina kendi karar verir. Animasyon icin yield return ctx.WaitFor(saniye)
    // ve ctx.AnimatePop(this) cagir. Retrigger istegi:
    //   ctx.RequestExtraDicePass(diceIndex, count)  -> dice retrigger
    //   ctx.RequestPerkReplay(targetPerk)           -> perk retrigger
    //
    // OnAttack event'inde ctx.currentPerk == this kontrol et — yoksa her perkin
    // OnAttack'inda senin perk de tetiklenir. Diger event'lerde gereksiz.
    public virtual IEnumerator OnEvent(CombatContext ctx) { yield break; }

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

    // Description'i yeniden insa eder (seviye atlayinca veya GameKeywords renk/deger degisince cagrilir).
    // SONUC: `renderedDescription` (runtime-only) field'ina yazilir. `description` field'ina ASLA DOKUNULMAZ.
    public virtual void RebuildDescription() => RebuildDescription(includeStack: true);

    /// <summary>
    /// includeStack=false ise [stack]...[/stack] icindeki "Currently: X" gibi runtime sayaclari
    /// SILINIR. Shop'ta perk satin alirken kullanilir (oyuncu henuz sahip degil, sayac anlamsiz).
    /// includeStack=true varsayilan — sahip olunan perk tooltip'leri bunu gosterir.
    /// </summary>
    public virtual void RebuildDescription(bool includeStack)
    {
        if (string.IsNullOrEmpty(description))
        {
            renderedDescription = "";
            return;
        }

        string template = includeStack ? StripStackTags(description, keepContent: true)
                                       : StripStackTags(description, keepContent: false);

        var values = GetDescValues();
        string built = (values == null || values.Count == 0)
            ? template
            : ApplyTokens(template, values);

        renderedDescription = ApplyInlineHighlights(built);
    }

    /// <summary>
    /// [stack]...[/stack] aralarini ya korur (sadece tag'leri silip iceriği bırakır)
    /// ya da iceriği ile birlikte komple siler. Cevreleyen newline'lari da temizler ki
    /// bos satir kalmasin.
    /// </summary>
    private static string StripStackTags(string s, bool keepContent)
    {
        if (string.IsNullOrEmpty(s)) return s;
        const string open = "[stack]";
        const string close = "[/stack]";

        int idx = 0;
        var sb = new System.Text.StringBuilder(s.Length);
        while (true)
        {
            int o = s.IndexOf(open, idx, System.StringComparison.Ordinal);
            if (o < 0) { sb.Append(s, idx, s.Length - idx); break; }
            int c = s.IndexOf(close, o + open.Length, System.StringComparison.Ordinal);
            if (c < 0) { sb.Append(s, idx, s.Length - idx); break; }

            int chunkStart = idx;
            int chunkEnd = o;

            if (keepContent)
            {
                sb.Append(s, chunkStart, chunkEnd - chunkStart);
                sb.Append(s, o + open.Length, c - (o + open.Length));
                idx = c + close.Length;
            }
            else
            {
                // Iceriği komple sil. Once'ki newline ve close'tan sonraki newline'i da topla.
                while (chunkEnd > chunkStart && (s[chunkEnd - 1] == '\n' || s[chunkEnd - 1] == '\r' || s[chunkEnd - 1] == ' '))
                    chunkEnd--;
                sb.Append(s, chunkStart, chunkEnd - chunkStart);
                int after = c + close.Length;
                while (after < s.Length && (s[after] == '\n' || s[after] == '\r' || s[after] == ' '))
                    after++;
                idx = after;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Inspector'da yazilan inline highlight tag'lerini hex renkli + bold spans'e cevirir.
    /// Tag'ler:
    ///   [a]text[/a]  -> ACTION turuncu+bold (skip/kill/attack/push/level/burn)
    ///   [s]text[/s]  -> STATUS beyaz+bold (shield/dodge/spike/stun)
    ///   [r]text[/r]  -> RETRIGGER mor+bold (retriggers/triggers again)
    ///   [c]text[/c]  -> COUNTER beyaz+bold (sayaclar)
    ///   [m]text[/m]  -> MULT kirmizi (X2, x4, sabit carpan ifadeleri)
    ///   [p]text[/p]  -> PLUS mavi (+5 sabit damage ifadeleri)
    ///   [g]text[/g]  -> GOLD sari
    ///   [h]text[/h]  -> HP yesil
    /// Dinamik degerler icin token sistemi (GetDescValues) kullan; sabit highlight'lar icin bunlar.
    /// </summary>
    private static string ApplyInlineHighlights(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Renk-isimli tag'ler (insan-okunur, Inspector'da kolay yazilir):
        //   [white]...[/white]   -> beyaz+bold (status: shield/dodge/skip/kill/level vb.)
        //   [orange]...[/orange] -> turuncu+bold (action: burn)
        //   [purple]...[/purple] -> mor+bold (retrigger)
        //   [red]...[/red]       -> kirmizi (mult, X carpan)
        //   [blue]...[/blue]     -> mavi (chips, +damage)
        //   [yellow]...[/yellow] -> sari (gold)
        //   [green]...[/green]   -> yesil (HP/heal)
        text = ReplaceTag(text, "white",  UIColors.Status,    bold: true);
        text = ReplaceTag(text, "orange", UIColors.Action,    bold: true);
        text = ReplaceTag(text, "purple", UIColors.Retrigger, bold: true);
        text = ReplaceTag(text, "red",    UIColors.Mult,      bold: false);
        text = ReplaceTag(text, "blue",   UIColors.Chips,     bold: false);
        text = ReplaceTag(text, "yellow", UIColors.Gold,      bold: false);
        text = ReplaceTag(text, "green",  UIColors.Heal,      bold: false);

        // Eski tek-harf tag'ler (geri uyumluluk):
        text = ReplaceTag(text, "a", UIColors.Action,    bold: true);
        text = ReplaceTag(text, "s", UIColors.Status,    bold: true);
        text = ReplaceTag(text, "r", UIColors.Retrigger, bold: true);
        text = ReplaceTag(text, "c", UIColors.Status,    bold: true);
        text = ReplaceTag(text, "m", UIColors.Mult,      bold: false);
        text = ReplaceTag(text, "p", UIColors.Chips,     bold: false);
        text = ReplaceTag(text, "g", UIColors.Gold,      bold: false);
        text = ReplaceTag(text, "h", UIColors.Heal,      bold: false);
        return text;
    }

    private static string ReplaceTag(string s, string tag, string hex, bool bold)
    {
        string open = $"[{tag}]";
        string close = $"[/{tag}]";
        int idx = 0;
        StringBuilder sb = null;
        while (true)
        {
            int o = s.IndexOf(open, idx);
            if (o < 0) break;
            int c = s.IndexOf(close, o + open.Length);
            if (c < 0) break;
            sb ??= new StringBuilder(s.Length + 32);
            sb.Append(s, idx, o - idx);
            string inner = s.Substring(o + open.Length, c - o - open.Length);
            string colored = bold ? $"<color=#{hex}><b>{inner}</b></color>" : $"<color=#{hex}>{inner}</color>";
            sb.Append(colored);
            idx = c + close.Length;
        }
        if (sb == null) return s;
        sb.Append(s, idx, s.Length - idx);
        return sb.ToString();
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

        // YENI SISTEM: auto-detect KAPALI. Perkler GameKeywords helper'lari (Plus/Mult/Gold/Heal/
        // Retrigger/Action/Status/Crit) ile renkli string uretir. Bu metod gelen string'i oldugu
        // gibi gecirir — helper string'leri zaten <color> tag iceriyor, plain string'ler beyaz kalir.
        // Eski perkler helper'a cevrilince renkli gozukur, henuz cevrilmemis olanlar beyaz olur.
        return s;
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