using UnityEngine;

/// <summary>
/// Neural Hijack (Legendary)
/// DÃ¼ÅŸmanÄ± baÅŸka bir dÃ¼ÅŸmana ittiÄŸinde, itilen dÃ¼ÅŸman taraf deÄŸiÅŸtirir.
/// Dost dÃ¼ÅŸman: 3 HP, oyuncunun zarlarÄ±yla hasar verir, Ã¶lene kadar yaÅŸar.
/// Bir kez dost olan dÃ¼ÅŸman bir daha dÃ¼ÅŸman olamaz.
/// </summary>
public class NeuralHijackPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "push", GameKeywords.Action("Push") }
    };
}
