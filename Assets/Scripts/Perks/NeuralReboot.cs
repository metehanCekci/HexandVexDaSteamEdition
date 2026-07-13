using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NeuralRebootPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "low", GameKeywords.Counter("3") },
        { "reroll", GameKeywords.Action("rerolled") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        int delta = 0;
        for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
        {
            if (ctx.payload.diceRolls[i] <= 3)
            {
                int oldVal = ctx.payload.diceRolls[i];
                int safety = 0;
                while (ctx.payload.diceRolls[i] <= 3 && safety < 100)
                {
                    ctx.payload.diceRolls[i] = Random.Range(1, 7);
                    safety++;
                }
                delta += ctx.payload.diceRolls[i] - oldVal;
            }
        }
        ctx.payload.ApplyAdd(delta);
        if (delta != 0) ctx.AnimatePop(this);
    }
}
