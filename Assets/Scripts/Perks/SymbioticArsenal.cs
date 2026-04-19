using UnityEngine;

public class SymbioticArsenalPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        processLast = true;
    }

    /// <summary>
    /// Dolu her item slotu basina +0.5x multiplier, seviye basina +0.25x ekstra.
    /// Lv1: 0.5x per slot, Lv2: 0.75x per slot, Lv3: 1.0x per slot
    /// </summary>
    public override void ModifyCombat(CombatPayload payload)
    {
        if (InventoryManager.instance == null) return;

        int filledSlots = InventoryManager.instance.OccupiedSlotCount();
        if (filledSlots <= 0) return;

        float bonusPerSlot = 0.25f + (0.25f * currentLevel);
        payload.multiplier += bonusPerSlot * filledSlots;
        TriggerVisualPop();
    }
}
