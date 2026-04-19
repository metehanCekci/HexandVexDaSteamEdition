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
    [TextArea] public string description;
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
    /// Item kullanıldığında çağrılır. true dönerse item tüketilmiş demektir.
    /// </summary>
    public abstract bool Use();

    public virtual Dictionary<string, object> GetDescValues() { return null; }

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

        if (lower.Contains("gold"))
            hex = UIColors.Gold;
        else if (s[0] == 'x' || s[0] == 'X')
            hex = UIColors.Mult;
        else if (s[0] == '+' || s[0] == '-')
            hex = UIColors.Chips;
        else if (lower.Contains("hp") || lower.Contains("heal"))
            hex = UIColors.Heal;
        else if (lower.Contains("damage"))
            hex = UIColors.Damage;
        else
            hex = UIColors.Chips;

        return $"<color=#{hex}>{s}</color>";
    }
}
