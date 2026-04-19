using UnityEngine;
using System.Collections.Generic;

public class AcidBloodPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Pushing an enemy into spikes heals you for {amount}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "amount", $"{currentLevel} HP" }
    };

    public override void OnAcquire() { RebuildDescription(); }
}
