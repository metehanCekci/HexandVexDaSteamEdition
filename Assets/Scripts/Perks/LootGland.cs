using UnityEngine;
using System;
using System.Collections.Generic;

public class LootGlandPerk : BasePerk
{
    private bool bonusApplied = false;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.PlusGold(2 * currentLevel) },
        { "kill",  GameKeywords.Action("kill") }
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
