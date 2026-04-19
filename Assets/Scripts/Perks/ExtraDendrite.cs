using UnityEngine;
using System.Collections.Generic;

public class ExtraDendritePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 999;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Roll {extraDice} extra die.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "extraDice", $"+{currentLevel}" }
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
