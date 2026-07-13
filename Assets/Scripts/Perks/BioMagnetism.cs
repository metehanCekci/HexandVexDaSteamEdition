using UnityEngine;
using System.Collections.Generic;

public class BioMagnetismPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "pull",       GameKeywords.Action("Pull") },
        { "levelStart", GameKeywords.Action("level start") }
    };

}