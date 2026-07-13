using UnityEngine;
using System.Collections.Generic;

public class BribePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "free", GameKeywords.GoldText("free") }
    };
}
