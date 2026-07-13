using UnityEngine;
using System.Collections.Generic;

public class HyperCortexPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "crit", GameKeywords.CritPlus(50 * currentLevel, "crit damage") }
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
