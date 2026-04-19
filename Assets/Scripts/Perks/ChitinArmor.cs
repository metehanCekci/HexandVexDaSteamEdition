using UnityEngine;

public class ChitinArmorPerk : BasePerk
{
    private bool isEquipped = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        if (isEquipped) return; // Çift uygulamayı engelle
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
