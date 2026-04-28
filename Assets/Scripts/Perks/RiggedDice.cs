using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class RiggedDicePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "reroll",  GameKeywords.Action("rerolled") },
        { "highest", GameKeywords.Status("highest") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        if (ctx.payload.diceRolls.Count < 2) yield break;

        int minVal = ctx.payload.diceRolls.Min();
        int maxVal = ctx.payload.diceRolls.Max();

        if (minVal == maxVal) yield break;

        bool changed = false;
        int delta = 0;
        for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
        {
            if (ctx.payload.diceRolls[i] != maxVal)
            {
                delta += maxVal - ctx.payload.diceRolls[i];
                ctx.payload.diceRolls[i] = maxVal;
                changed = true;
            }
        }
        ctx.payload.ApplyAdd(delta);

        if (changed) ctx.AnimatePop(this);
    }
}
