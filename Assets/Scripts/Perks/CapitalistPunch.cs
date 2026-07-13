using System.Collections;
using System.Collections.Generic;

public class CapitalistPunchPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "cost",  GameKeywords.Gold(5) },
        { "bonus", GameKeywords.Plus(1, "damage") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        if (RunManager.instance == null) yield break;

        int bonus = RunManager.instance.currentGold / 5;
        if (bonus <= 0) yield break;

        for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
            ctx.payload.diceRolls[i] += bonus;
        ctx.payload.ApplyAdd(bonus * ctx.payload.diceRolls.Count);
        ctx.AnimatePop(this);
    }
}
