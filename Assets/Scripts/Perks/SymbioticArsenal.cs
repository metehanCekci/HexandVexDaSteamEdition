using System.Collections.Generic;
using System.Globalization;

public class SymbioticArsenalPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Mult(0.25f + 0.25f * currentLevel) }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        if (InventoryManager.instance == null) return;

        int filledSlots = InventoryManager.instance.OccupiedSlotCount();
        if (filledSlots <= 0) return;

        float bonusPerSlot = 0.25f + (0.25f * currentLevel);
        // Eski model: multiplier += X -> efektif carpan (1+X). Balatro modelinde ApplyMult(1+X).
        payload.ApplyMult(1f + bonusPerSlot * filledSlots);
        TriggerVisualPop();
    }
}
