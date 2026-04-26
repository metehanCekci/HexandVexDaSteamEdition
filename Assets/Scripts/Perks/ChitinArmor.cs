using UnityEngine;
using System.Collections.Generic;

public class ChitinArmorPerk : BasePerk
{
    private bool isEquipped = false;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "dodge", GameKeywords.Status("dodge") }
    };

    public override void OnAcquire()
    {
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        if (isEquipped) return;
        isEquipped = true;
        RunManager.instance.dodgeChance += 0.30f;
    }

    public override void OnUnequip()
    {
        if (!isEquipped) return;
        isEquipped = false;
        RunManager.instance.dodgeChance -= 0.30f;
    }
}
