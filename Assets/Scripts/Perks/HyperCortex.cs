using UnityEngine;
using System.Collections.Generic;

public class HyperCortexPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Gain {crit} crit damage.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "crit", $"+{50 * currentLevel}%" }
    };

    public override void OnAcquire()
    {
        RunManager.instance.criticalDamageMultiplier += 0.5f;
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();

        RunManager.instance.criticalDamageMultiplier += 0.5f;
        TriggerVisualPop();
    }
}
