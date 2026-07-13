using UnityEngine;
using System.Collections.Generic;

public class AcidBloodPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "push",   GameKeywords.Action("Push") },
        { "spikes", GameKeywords.Status("spikes") },
        { "heal",   GameKeywords.Heal(currentLevel) }
    };

    public override void OnAcquire() { RebuildDescription(); }
}
