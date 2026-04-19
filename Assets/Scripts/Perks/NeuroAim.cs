using UnityEngine;
using System.Collections.Generic;

public class NeuroAimPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Gain {crit} crit chance.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "crit", $"+{25 * currentLevel}%" }
    };

    public override void OnAcquire()
    {
        RunManager.instance.criticalChance += 0.25f;
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();

        RunManager.instance.criticalChance += 0.25f;
        TriggerVisualPop();
    }
}
