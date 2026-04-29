using System.Collections;
using System.Collections.Generic;

public class ToxinEdgePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(currentLevel) }
    };

    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        int delta = 0;
        for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
        {
            ctx.payload.diceRolls[i] += currentLevel;
            delta += currentLevel;
        }
        ctx.payload.ApplyAdd(delta);
        ctx.AnimatePop(this);
    }
}
