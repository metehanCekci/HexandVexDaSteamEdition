using UnityEngine;
using System.Collections.Generic;

public class ExtraDendritePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "extraDice", GameKeywords.Plus(currentLevel, "extra die") }
    };

    public override void OnAcquire()
    {
        RunManager.instance.baseDiceCount += 1;
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();

        RunManager.instance.baseDiceCount += 1;
        TriggerVisualPop();
    }
}
