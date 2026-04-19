using UnityEngine;
using System;
using System.Collections.Generic;

public class LootGlandPerk : BasePerk
{
    private bool bonusApplied = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Gain {bonus} bonus per kill for each level.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", $"+{2 * currentLevel} gold" }
    };

    public override void OnAcquire()
    {
        ApplyBonus();
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        RemoveBonus();
        base.Upgrade();
        ApplyBonus();
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        ApplyBonus();
    }

    public override void OnUnequip()
    {
        RemoveBonus();
    }

    private void ApplyBonus()
    {
        if (bonusApplied) return;
        bonusApplied = true;
        RunManager.instance.bonusGold += 2 * currentLevel;
    }

    private void RemoveBonus()
    {
        if (!bonusApplied) return;
        bonusApplied = false;
        RunManager.instance.bonusGold -= 2 * currentLevel;
    }
}
