using System.Collections;
using System.Collections.Generic;

public class MomentumEnginePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(1) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        if (TurnManager.instance == null || TurnManager.instance.hexesMovedThisTurn <= 0) yield break;

        int stepsTaken = TurnManager.instance.hexesMovedThisTurn;

        for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
            ctx.payload.diceRolls[i] += stepsTaken;
        ctx.payload.ApplyAdd(stepsTaken * ctx.payload.diceRolls.Count);
        ctx.AnimatePop(this);
    }
}
