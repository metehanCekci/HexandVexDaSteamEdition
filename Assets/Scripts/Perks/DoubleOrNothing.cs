using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DoubleOrNothingPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", GameKeywords.Mult(2) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        int total = ctx.payload.diceRolls.Sum();
        if (total % 2 != 0) yield break;

        ctx.payload.ApplyMult(2f);
        ctx.AnimatePop(this);
    }
}
