using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Photovoltaic Pulse — Rare. Ilk zarin degeri kadar multiplier uygular.
/// Ilk zar her retriggerlandiginda multiplier'i bir kez daha ARDISIK uygular.
/// </summary>
public class PhotovoltaicPulsePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "first",  GameKeywords.Status("first die") },
        { "triggers", GameKeywords.Retrigger("triggers") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        switch (ctx.eventType)
        {
            case CombatEventType.OnAttack:
                if (ctx.currentPerk != this) yield break;
                if (ctx.payload.diceRolls.Count == 0) yield break;
                int first = ctx.payload.diceRolls[0];
                if (first <= 0) yield break;
                ctx.payload.ApplyMult(first);
                ctx.AnimateDie(0);
                ctx.AnimatePop(this);
                yield return ctx.WaitFor(0.3f);
                break;

            case CombatEventType.OnDiceScored:
                if (ctx.retrigCountSoFar == 0) yield break;
                if (ctx.diceIndex != 0) yield break;
                if (ctx.diceValue <= 0) yield break;
                ctx.payload.ApplyMult(ctx.diceValue);
                ctx.AnimatePop(this);
                break;
        }
    }
}
