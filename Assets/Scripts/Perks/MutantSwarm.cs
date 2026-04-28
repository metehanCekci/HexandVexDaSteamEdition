using System.Collections;
using System.Collections.Generic;

public class MutantSwarmPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Mult(0.5f * currentLevel) }
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

        float bonusPerDie = 0.5f * currentLevel;
        float extraMult = 1.0f + (ctx.payload.diceRolls.Count * bonusPerDie);

        ctx.payload.ApplyMult(extraMult);
        ctx.AnimatePop(this);
    }
}
