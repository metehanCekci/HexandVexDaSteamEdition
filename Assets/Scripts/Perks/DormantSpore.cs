using UnityEngine;
using System;
using System.Collections.Generic;

public class DormantSporePerk : BasePerk
{
    public int storedExtraDices = 0;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "skip",   GameKeywords.Action("skip") },
        { "die",    GameKeywords.Plus(1, "die") },
        { "attack", GameKeywords.Action("attack") },
        { "stored", GameKeywords.Counter(storedExtraDices.ToString()) }
    };

    public int ConsumeStoredDice()
    {
        int dice = storedExtraDices;
        storedExtraDices = 0;
        RebuildDescription();
        return dice;
    }

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnSkip()
    {
        storedExtraDices++;
        RebuildDescription();
        TriggerVisualPop();
    }

    public override void OnLevelStart()
    {
        RebuildDescription();
    }
}
