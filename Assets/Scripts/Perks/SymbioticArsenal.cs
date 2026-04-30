using System.Collections;
using System.Collections.Generic;

public class SymbioticArsenalPerk : BasePerk
{
    private ScaledValue _bonusPerItem = new ScaledValue(baseValue: 0.5f, perLevel: 0.25f);
    public ScaledValue BonusPerItemValue => _bonusPerItem;

    public float GetBonusPerItem() => _bonusPerItem.Get(currentLevel);

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Mult(GetBonusPerItem()) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        if (InventoryManager.instance == null) yield break;

        int readyItems = CountReadyItems();
        if (readyItems <= 0) yield break;

        ctx.payload.ApplyMult(1f + GetBonusPerItem() * readyItems);
        ctx.AnimatePop(this);
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
