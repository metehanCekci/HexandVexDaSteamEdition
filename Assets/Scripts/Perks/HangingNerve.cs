using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Hanging Nerve — Epic. Ilk atilan zar bir kez daha retriggerlanir.
/// Mimetic ile replay olunca: ilk zar 2 ekstra retrigger (1 + replayCount).
/// </summary>
public class HangingNervePerk : BasePerk
{
    private int replayCount = 0;

    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "retrigger", GameKeywords.RetriggerN(1) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        switch (ctx.eventType)
        {
            case CombatEventType.BeforeCombat:
                replayCount = 0;
                break;

            case CombatEventType.OnAttack:
                if (ctx.currentPerk != this) yield break;
                if (ctx.isReplay) replayCount++;
                break;

            case CombatEventType.OnDiceScored:
                if (ctx.retrigCountSoFar != 0) yield break;
                if (ctx.diceIndex != 0) yield break;
                ctx.RequestExtraDicePass(ctx.diceIndex, 1 + replayCount);
                break;
        }
    }
}
