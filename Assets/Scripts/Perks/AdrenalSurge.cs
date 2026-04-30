using System.Collections;
using System.Collections.Generic;

public class AdrenalSurgePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attack", GameKeywords.Action("attack") },
        { "mult",   GameKeywords.Mult(2) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        ctx.payload.ApplyMult(2.0f);
        ctx.AnimatePop(this);
    }
}
