using UnityEngine;
using System.Collections.Generic;
using System.Text;

public enum ItemType
{
    Consumable,   // Goes into inventory/hotbar, used during combat (FragMine, SurgeBoot, etc.)
    Instant       // Applied immediately on purchase, never enters inventory (SecretPerkOrb, MutationCatalyst)
}

public abstract class BaseItem : ScriptableObject
{
    public string itemName;

    // ============================================================================
    // DESCRIPTION TEMPLATE (Inspector'dan yazilir, kod karismaz)
    // ============================================================================
    // Bu field item'in aciklama sablonudur. Inspector'da diledigin gibi yaz.
    //
    // ── INLINE HIGHLIGHT TAG'LERI (sabit kelimeler icin) ──
    //   [a]burn[/a]      -> TURUNCU+bold (SADECE burn/fire icin)
    //   [s]heal[/s]      -> beyaz+bold STATUS (kill/attack/use/dodge/shield/stun/spike vb.)
    //   [r]retriggers[/r]-> mor+bold RETRIGGER
    //   [c]5/30[/c]      -> beyaz+bold COUNTER (sayaclar)
    //   [m]X4[/m]        -> kirmizi MULT (sabit carpan)
    //   [p]+5[/p]        -> mavi PLUS (sabit damage ekleme)
    //   [g]5 gold[/g]    -> sari GOLD
    //   [h]5 HP[/h]      -> yesil HP
    //
    // {token_adi} -> dinamik deger, GetDescValues() doldurur (GameKeywords helper'lari kullanin).
    // ============================================================================
    [TextArea(3, 6)] public string description;
    [System.NonSerialized] private string _descriptionTemplate;
    public int price;
    public Sprite icon;

    [Header("Inventory Behavior")]
    [Tooltip("Consumable = goes to hotbar on purchase. Instant = applied immediately on purchase.")]
    public ItemType itemType = ItemType.Consumable;

    /// <summary>
    /// Runtime flag: true ise bu item bu combat'ta zaten kullanıldı.
    /// Combat bittiğinde InventoryManager tarafından resetlenir.
    /// </summary>
    [System.NonSerialized] public bool usedThisCombat;

    /// <summary>
    /// Bu combat'ta usedThisCombat=true olduktan sonra ekstra kullanim hakki.
    /// ExtraAmmo perki tarafindan combat basinda set edilir.
    /// 0 = ek hak yok (varsayilan).
    /// </summary>
    [System.NonSerialized] public int extraUses;

    /// <summary>
    /// Item kullanıldığında çağrılır. true dönerse item tüketilmiş demektir.
    /// </summary>
    public abstract bool Use();

    public virtual Dictionary<string, object> GetDescValues() { return null; }

    public virtual void RebuildDescription()
    {
        // Template'i her seferinde mevcut description'dan al — Inspector'da yeni token'li sablon
        // varsa onu kabul et. Eger token'siz ise (zaten doldurulmus) cache'lenmis template'i kullan.
        bool descLooksLikeTemplate = !string.IsNullOrEmpty(description) && description.Contains("{");
        if (descLooksLikeTemplate)
            _descriptionTemplate = description;
        else if (string.IsNullOrEmpty(_descriptionTemplate))
        {
            if (string.IsNullOrEmpty(description)) return;
            _descriptionTemplate = description;
        }

        var values = GetDescValues();
        string built;
        if (values == null || values.Count == 0)
            built = _descriptionTemplate;
        else
            built = ApplyTokens(_descriptionTemplate, values);

        // Inline highlight tag'lerini renkli spans'e cevir.
        description = ApplyInlineHighlights(built);
    }

    private static string ApplyInlineHighlights(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
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

        // YENI SISTEM: auto-detect KAPALI. Item'lar GameKeywords helper'lari ile renkli string uretir.
        // Bu metod gelen string'i oldugu gibi gecirir.
        return s;
    }
}
