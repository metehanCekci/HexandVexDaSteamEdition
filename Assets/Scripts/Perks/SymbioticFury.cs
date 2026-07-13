using System.Collections;
using System.Collections.Generic;

public class SymbioticFuryPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", GameKeywords.Crit("multiply") },
        { "add",  GameKeywords.Status("adding") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        ctx.payload.multiplyInsteadOfAdd = true;
    }
}
