using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sensory Overload — Rare. 5 veya 6 gelen her zar bir kez daha retriggerlanir.
/// Mimetic ile replay olunca: 1 + replayCount ekstra retrigger.
/// </summary>
public class SensoryOverloadPerk : BasePerk
{
    private int replayCount = 0;

    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "five",  GameKeywords.Status("5") },
        { "six",   GameKeywords.Status("6") },
        { "extra", GameKeywords.RetriggerN(1) }
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
                if (ctx.diceValue != 5 && ctx.diceValue != 6) yield break;
                ctx.RequestExtraDicePass(ctx.diceIndex, 1 + replayCount);
                break;
        }
    }
}
