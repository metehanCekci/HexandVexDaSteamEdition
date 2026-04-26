using UnityEngine;

public class VoodooParasitePerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "nearby", GameKeywords.Status("nearby") },
        { "curse",  GameKeywords.Action("voodoo curse") }
    };
}
