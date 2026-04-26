using UnityEngine;
using System.Collections.Generic;

public class NeuroAimPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "crit", GameKeywords.CritPlus(25 * currentLevel, "crit chance") }
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
