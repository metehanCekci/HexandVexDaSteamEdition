using UnityEngine;
using System;
using System.Collections.Generic;

public class DormantSporePerk : BasePerk
{
    public int storedExtraDices = 0;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Each skip stores {die} for your next attack.\nStored dice: {stored}";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "die",    "+1 die" },
        { "stored", $"+{storedExtraDices}" }
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
