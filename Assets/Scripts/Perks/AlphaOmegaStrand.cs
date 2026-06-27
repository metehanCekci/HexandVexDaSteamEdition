using System.Collections;
using System.Collections.Generic;

public class AlphaOmegaStrandPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(2 * currentLevel) }
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
        if (ctx.payload.diceRolls.Count == 0) yield break;

        int bonus = 2 * currentLevel;
        int delta = 0;

        ctx.payload.diceRolls[0] += bonus;
        delta += bonus;

        if (ctx.payload.diceRolls.Count > 1)
        {
            ctx.payload.diceRolls[ctx.payload.diceRolls.Count - 1] += bonus;
            delta += bonus;
        }
        ctx.payload.ApplyAdd(delta);
        ctx.AnimatePop(this);
    }
}
