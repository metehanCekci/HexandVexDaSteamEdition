using UnityEngine;
using System.Collections.Generic;

public class ChitinArmorPerk : BasePerk
{
    private bool isEquipped = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Gain {dodge} dodge chance.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "dodge", "+30%" }
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
