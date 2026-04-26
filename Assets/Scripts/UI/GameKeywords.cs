using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Description'larda gecen semantic anahtar kelime/deger registry.
/// Perk description'lari bu metodlari cagirarak renkli + runtime-tweak'lenebilir string uretir.
///
/// RENK KURALLARI (kullanici tarafindan kesin tanimlanmis):
///   MAVI    (Chips)     -> + ile baslayan zara/damage'a deger ekleyen ifadeler.       Plus(n, suffix)
///   KIRMIZI (Mult/Crit) -> X ile baslayan carpan ifadeleri ve kritik vurus.            Mult(n, suffix), Crit(n, suffix)
///   SARI    (Gold)      -> gold ifadeleri.                                            Gold(n), PlusGold(n)
///   YESIL   (Health)    -> HP / heal / health ifadeleri.                              Heal(n), Hp(n)
///   MOR     (Retrigger) -> retrigger / "triggers again" / "X more times" ifadeleri.   Retrigger(text)
///   TURUNCU (Action)    -> skip / kill / attack / push / level cleared / burn vb.     Action(text)
///   BEYAZ   (Status)    -> shield / dodge / spike / stun / sayaclar (currently: X/Y). Status(text), Counter(text)
///
/// X HER ZAMAN BUYUK harfle yazilir (X4, X3 — x4 degil).
/// </summary>
public static class GameKeywords
{
    // ── Global modifiers (joker entegrasyonu icin) ──
    public static float GoldGainMultiplier = 1f;  // "N gold" degerine uygulanir
    public static int   PlusValueBonus     = 0;   // "+N" degerlerine eklenir
    public static float MultValueBonus     = 0f;  // "XN" degerlerine eklenir

    public static event Action OnChanged;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WireColorRefresh()
    {
        UIColors.OnChanged -= RefreshAllDescriptions;
        UIColors.OnChanged += RefreshAllDescriptions;
    }

    // =============================================================================
    // MAVI / Chips — zara/damage'a EKLEME
    // =============================================================================

    /// <summary>"+5 damage" gibi — SADECE "+5" mavi, "damage" beyaz.</summary>
    public static string Plus(int baseValue, string suffix = "")
    {
        int final = baseValue + PlusValueBonus;
        string colored = $"<color=#{UIColors.Chips}>+{final}</color>";
        return string.IsNullOrEmpty(suffix) ? colored : $"{colored} {suffix}";
    }

    /// <summary>"+1.5 damage" gibi — SADECE "+1.5" mavi.</summary>
    public static string PlusF(float baseValue, string suffix = "")
    {
        float final = baseValue + PlusValueBonus;
        string num = FormatNum(final);
        string colored = $"<color=#{UIColors.Chips}>+{num}</color>";
        return string.IsNullOrEmpty(suffix) ? colored : $"{colored} {suffix}";
    }

    /// <summary>"-3 damage" gibi — SADECE "-3" mavi.</summary>
    public static string Minus(int baseValue, string suffix = "")
    {
        string colored = $"<color=#{UIColors.Chips}>-{baseValue}</color>";
        return string.IsNullOrEmpty(suffix) ? colored : $"{colored} {suffix}";
    }

    // =============================================================================
    // KIRMIZI / Mult — zar carpani veya kritik vurus
    // =============================================================================

    /// <summary>"X2 damage" gibi — SADECE "X2" kirmizi, "damage" beyaz. X HER ZAMAN BUYUK.</summary>
    public static string Mult(float baseValue, string suffix = "")
    {
        float final = baseValue + MultValueBonus;
        string num = FormatNum(final);
        string colored = $"<color=#{UIColors.Mult}>X{num}</color>";
        return string.IsNullOrEmpty(suffix) ? colored : $"{colored} {suffix}";
    }

    /// <summary>"X2" int versiyonu.</summary>
    public static string Mult(int baseValue, string suffix = "")
    {
        return Mult((float)baseValue, suffix);
    }

    /// <summary>"Critical Hit" gibi serbest kirmizi crit ifadesi.</summary>
    public static string Crit(string text)
    {
        return $"<color=#{UIColors.Mult}>{text}</color>";
    }

    /// <summary>"+25% crit chance" gibi — SADECE "+25%" kirmizi, "crit chance" beyaz.</summary>
    public static string CritPlus(int percent, string suffix)
    {
        string colored = $"<color=#{UIColors.Mult}>+{percent}%</color>";
        return string.IsNullOrEmpty(suffix) ? colored : $"{colored} {suffix}";
    }

    // =============================================================================
    // SARI / Gold
    // =============================================================================

    /// <summary>"5 gold" — SADECE "5" sari, "gold" beyaz.</summary>
    public static string Gold(int baseValue)
    {
        int final = Mathf.RoundToInt(baseValue * GoldGainMultiplier);
        return $"<color=#{UIColors.Gold}>{final}</color> gold";
    }

    /// <summary>"+2 gold" — SADECE "+2" sari, "gold" beyaz.</summary>
    public static string PlusGold(int baseValue)
    {
        int final = Mathf.RoundToInt((baseValue + PlusValueBonus) * GoldGainMultiplier);
        return $"<color=#{UIColors.Gold}>+{final}</color> gold";
    }

    /// <summary>Sari serbest gold ifadesi (ornek: "free", "all gold").</summary>
    public static string GoldText(string text)
    {
        return $"<color=#{UIColors.Gold}>{text}</color>";
    }

    // =============================================================================
    // YESIL / Health
    // =============================================================================

    /// <summary>"5 HP" — SADECE "5" yesil, "HP" beyaz.</summary>
    public static string Hp(int v) => $"<color=#{UIColors.Heal}>{v}</color> HP";

    /// <summary>"heal 5 HP" — SADECE "5" yesil, "heal" ve "HP" beyaz.</summary>
    public static string Heal(int v) => $"heal <color=#{UIColors.Heal}>{v}</color> HP";

    /// <summary>Yesil serbest health ifadesi (ornek: "full HP", "max HP", "missing HP").</summary>
    public static string HealthText(string text)
    {
        return $"<color=#{UIColors.Heal}>{text}</color>";
    }

    // =============================================================================
    // MOR / Retrigger
    // =============================================================================

    /// <summary>Sadece "retrigger"/"retriggers" kelimesi MOR+BOLD, geri kalan etraf beyaz/normal kalir.
    /// Kelimeyi kendin yaz, helper sadece o kelimeyi sarar.
    /// Ornek: Retrigger("retriggers") + " twice" -> mor "retriggers" + beyaz " twice".</summary>
    public static string Retrigger(string retriggerWordOnly)
    {
        return $"<color=#{UIColors.Retrigger}><b>{retriggerWordOnly}</b></color>";
    }

    /// <summary>"retriggers N more time(s)" sablonu — sadece "retriggers" kelimesi mor, geri kalan beyaz.</summary>
    public static string RetriggerN(int n)
    {
        string colored = $"<color=#{UIColors.Retrigger}><b>retriggers</b></color>";
        return n == 1 ? $"{colored} once" : $"{colored} {n} more times";
    }

    // =============================================================================
    // TURUNCU / Action — skip / kill / attack / push / level cleared / burn vb.
    // =============================================================================

    /// <summary>Action keyword'unu turuncu + bold ile vurgular.</summary>
    public static string Action(string text)
    {
        return $"<color=#{UIColors.Action}><b>{text}</b></color>";
    }

    // =============================================================================
    // BEYAZ-BOLD / Status — shield / dodge / spike / stun / sayaclar
    // =============================================================================

    /// <summary>Status keyword'unu beyaz-bold ile vurgular.</summary>
    public static string Status(string text)
    {
        return $"<color=#{UIColors.Status}><b>{text}</b></color>";
    }

    /// <summary>Sayac/durum gosterimi: "Currently: 5", "0/120", vs. — beyaz-bold.</summary>
    public static string Counter(string text)
    {
        return $"<color=#{UIColors.Status}><b>{text}</b></color>";
    }

    // =============================================================================
    // Yardimci
    // =============================================================================

    private static string FormatNum(float v)
    {
        if (Mathf.Approximately(v % 1f, 0f)) return ((int)v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ── Setter'lar (joker entegrasyonu) ──
    public static void SetGoldMultiplier(float m)
    {
        GoldGainMultiplier = m;
        Fire();
    }

    public static void SetPlusBonus(int b)
    {
        PlusValueBonus = b;
        Fire();
    }

    public static void SetMultBonus(float b)
    {
        MultValueBonus = b;
        Fire();
    }

    static void Fire()
    {
        OnChanged?.Invoke();
        RefreshAllDescriptions();
    }

    /// <summary>Tum aktif perk/item description'larini yeniden hesaplatir.</summary>
    public static void RefreshAllDescriptions()
    {
        if (RunManager.instance == null) return;
        foreach (var p in RunManager.instance.activePerks)    p?.RebuildDescription();
        foreach (var p in RunManager.instance.inventoryPerks) p?.RebuildDescription();
    }
}
