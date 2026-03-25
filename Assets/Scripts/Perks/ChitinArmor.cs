using UnityEngine;

public class ChitinArmorPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
        RunManager.instance.dodgeChance += 0.30f;
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        RunManager.instance.dodgeChance += 0.30f;
    }

    public override void OnUnequip()
    {
        RunManager.instance.dodgeChance -= 0.30f;
    }
}
