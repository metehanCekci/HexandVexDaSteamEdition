using System;
using UnityEngine;

/// <summary>
/// Description'larda gecen semantic anahtar kelime/deger registry.
/// Perk description'lari bu metodlari cagirarak renkli+runtime-tweak'lenebilir string uretir.
/// Joker perkler global modifier'lari degistirir → tum description'lar yeniden hesaplanir.
/// </summary>
public static class GameKeywords
{
    // ── Global modifiers (joker entegrasyonu icin) ──
    public static float GoldGainMultiplier = 1f;  // "N gold" degerine uygulanir
    public static int   PlusValueBonus     = 0;   // "+N" degerlerine eklenir
    public static float MultValueBonus     = 0f;  // "xN" degerlerine eklenir

    public static event Action OnChanged;

    // Renk degisince de tum description'lari yeniden hesaplat
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void WireColorRefresh()
    {
        UIColors.OnChanged -= RefreshAllDescriptions;
        UIColors.OnChanged += RefreshAllDescriptions;
    }

    // ── Keyword formatters ──

    /// <summary>"+3 damage" gibi mavi chip ifadesi. suffix bos olabilir.</summary>
    public static string Plus(int baseValue, string suffix = "")
    {
        int final = baseValue + PlusValueBonus;
        string body = string.IsNullOrEmpty(suffix) ? $"+{final}" : $"+{final} {suffix}";
        return $"<color=#{UIColors.Chips}>{body}</color>";
    }

    /// <summary>"x2 damage" gibi kirmizi mult ifadesi.</summary>
    public static string Times(float baseValue, string suffix = "")
    {
        float final = baseValue + MultValueBonus;
        string num = final % 1f == 0 ? ((int)final).ToString() : final.ToString("0.##");
        string body = string.IsNullOrEmpty(suffix) ? $"x{num}" : $"x{num} {suffix}";
        return $"<color=#{UIColors.Mult}>{body}</color>";
    }

    /// <summary>"5 gold" sari ifade. GoldGainMultiplier uygulanir.</summary>
    public static string Gold(int baseValue)
    {
        int final = Mathf.RoundToInt(baseValue * GoldGainMultiplier);
        return $"<color=#{UIColors.Gold}>{final} gold</color>";
    }

    /// <summary>"+2 gold" gibi — +N vurgusu gold renginde.</summary>
    public static string PlusGold(int baseValue)
    {
        int final = Mathf.RoundToInt((baseValue + PlusValueBonus) * GoldGainMultiplier);
        return $"<color=#{UIColors.Gold}>+{final} gold</color>";
    }

    public static string Damage(int v) => $"<color=#{UIColors.Damage}>{v} damage</color>";
    public static string DamageWord()  => $"<color=#{UIColors.Damage}>damage</color>";
    public static string Heal(int v)   => $"<color=#{UIColors.Heal}>{v} HP</color>";
    public static string Dice(int v)   => $"<color=#{UIColors.Chips}>+{v} die{(v == 1 ? "" : "s")}</color>";

    /// <summary>Ham sayiyi chip renginde sarar (suffix icin kullanilabilir).</summary>
    public static string Number(int v) => $"<color=#{UIColors.Chips}>{v}</color>";

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
