using System.Collections.Generic;

public class SymbioticArsenalPerk : BasePerk
{
    // Lvl1 = +0.50 per ready item, Lvl2 = +0.75, Lvl3 = +1.00. Joker perkler scale edebilir.
    private ScaledValue _bonusPerItem = new ScaledValue(baseValue: 0.5f, perLevel: 0.25f);
    public ScaledValue BonusPerItemValue => _bonusPerItem;

    public float GetBonusPerItem() => _bonusPerItem.Get(currentLevel);

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Mult(GetBonusPerItem()) }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        if (InventoryManager.instance == null) return;

        int readyItems = CountReadyItems();
        if (readyItems <= 0) return;

        payload.ApplyMult(1f + GetBonusPerItem() * readyItems);
        TriggerVisualPop();
    }

    private static int CountReadyItems()
    {
        int count = 0;
        for (int i = 0; i < InventoryManager.instance.SlotCount; i++)
        {
            var item = InventoryManager.instance.GetItem(i);
            if (item != null && !item.usedThisCombat) count++;
        }
        return count;
    }
}
