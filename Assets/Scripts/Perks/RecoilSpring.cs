using UnityEngine;

public class RecoilSpringPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "attack", GameKeywords.Action("attacking") },
        { "echo",   GameKeywords.Action("attack again") }
    };
}
