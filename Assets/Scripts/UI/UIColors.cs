using System;
using UnityEngine;

/// <summary>
/// Merkezi renk registry. Runtime editable — setter cagirinca OnChanged firlar,
/// dinleyiciler kendini refresh eder.
/// Property isimleri Hex string dondurur (rich-text tag'leri icin default kullanim).
/// Color versiyonu icin *Color suffix'li property'leri kullan (sprite/image icin).
/// </summary>
public static class UIColors
{
    public static event Action OnChanged;

    // ── Gameplay semantic ──
    static Color _gold       = HexToColor("#FFD700");
    static Color _chips      = HexToColor("#4FC3F7"); // "+N" mavi
    static Color _mult       = HexToColor("#FF5252"); // "xN" kirmizi
    static Color _damage     = HexToColor("#FF8A65"); // flat damage — turuncu-kirmizi (mult'tan farkli)
    static Color _heal       = HexToColor("#4CAF50");
    static Color _cantAfford = HexToColor("#E53935");

    // ── Rarity ──
    static Color _common     = HexToColor("#BDBDBD");
    static Color _rare       = HexToColor("#4FC3F7");
    static Color _epic       = HexToColor("#AB47BC");
    static Color _legendary  = HexToColor("#FFB300");
    static Color _secret     = HexToColor("#E040FB");

    // ── Health bar ──
    static Color _hpFull  = HexToColor("#4CAF50");
    static Color _hpMid   = HexToColor("#FFEB3B");
    static Color _hpLow   = HexToColor("#E53935");
    static Color _hpElite = HexToColor("#FF9800");
    static Color _hpBoss  = HexToColor("#B71C1C");

    // ── Gameplay semantic: HEX (default, rich-text icin) ──
    public static string Gold       => ToHex(_gold);
    public static string Chips      => ToHex(_chips);
    public static string Mult       => ToHex(_mult);
    public static string Damage     => ToHex(_damage);
    public static string Heal       => ToHex(_heal);
    public static string CantAfford => ToHex(_cantAfford);

    // ── Gameplay semantic: COLOR (sprite/image icin) + setter'lar ──
    public static Color GoldColor       { get => _gold;       set { _gold = value;       OnChanged?.Invoke(); } }
    public static Color ChipsColor      { get => _chips;      set { _chips = value;      OnChanged?.Invoke(); } }
    public static Color MultColor       { get => _mult;       set { _mult = value;       OnChanged?.Invoke(); } }
    public static Color DamageColor     { get => _damage;     set { _damage = value;     OnChanged?.Invoke(); } }
    public static Color HealColor       { get => _heal;       set { _heal = value;       OnChanged?.Invoke(); } }
    public static Color CantAffordColor { get => _cantAfford; set { _cantAfford = value; OnChanged?.Invoke(); } }

    // ── Rarity (Color + Hex) ──
    public static Color CommonColor    { get => _common;    set { _common = value;    OnChanged?.Invoke(); } }
    public static Color RareColor      { get => _rare;      set { _rare = value;      OnChanged?.Invoke(); } }
    public static Color EpicColor      { get => _epic;      set { _epic = value;      OnChanged?.Invoke(); } }
    public static Color LegendaryColor { get => _legendary; set { _legendary = value; OnChanged?.Invoke(); } }
    public static Color SecretColor    { get => _secret;    set { _secret = value;    OnChanged?.Invoke(); } }

    public static string CommonH    => ToHex(_common);
    public static string RareH      => ToHex(_rare);
    public static string EpicH      => ToHex(_epic);
    public static string LegendaryH => ToHex(_legendary);
    public static string SecretH    => ToHex(_secret);

    // ── Health bar ──
    public static Color HpFullColor  { get => _hpFull;  set { _hpFull = value;  OnChanged?.Invoke(); } }
    public static Color HpMidColor   { get => _hpMid;   set { _hpMid = value;   OnChanged?.Invoke(); } }
    public static Color HpLowColor   { get => _hpLow;   set { _hpLow = value;   OnChanged?.Invoke(); } }
    public static Color HpEliteColor { get => _hpElite; set { _hpElite = value; OnChanged?.Invoke(); } }
    public static Color HpBossColor  { get => _hpBoss;  set { _hpBoss = value;  OnChanged?.Invoke(); } }

    // ── Helpers ──
    public static Color GetRarityColor(PerkRarity r)
    {
        switch (r)
        {
            case PerkRarity.Rare:      return _rare;
            case PerkRarity.Epic:      return _epic;
            case PerkRarity.Legendary: return _legendary;
            case PerkRarity.Secret:    return _secret;
            default:                   return _common;
        }
    }

    public static string GetRarityHex(PerkRarity r) => ToHex(GetRarityColor(r));

    public static Color GetAffordabilityColor(bool canAfford) => canAfford ? _gold : _cantAfford;
    public static string GetAffordabilityHex(bool canAfford) => canAfford ? Gold : CantAfford;

    /// <summary>HP ratio (0..1) + isElite/isBoss → uygun renk.</summary>
    public static Color GetHealthBar(float ratio, bool isElite = false, bool isBoss = false)
    {
        if (isBoss)  return _hpBoss;
        if (isElite) return _hpElite;
        if (ratio > 0.6f) return Color.Lerp(_hpMid, _hpFull, (ratio - 0.6f) / 0.4f);
        if (ratio > 0.3f) return Color.Lerp(_hpLow, _hpMid, (ratio - 0.3f) / 0.3f);
        return _hpLow;
    }

    // ── Hex utility ──
    public static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.white;
    }

    public static string ToHex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
