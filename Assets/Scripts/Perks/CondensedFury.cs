using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Condensed Fury — Tek zar atilir, kalanlar otomatik retrigger olur.
/// Mimetic ile replay olunca: ekstra retrigger katkisi cogalir.
/// </summary>
public class CondensedFuryPerk : BasePerk
{
    [System.NonSerialized] public int pendingRetriggerCount = 0;
    private int replayCount = 0;

    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "diceDelta", GameKeywords.Minus(1) },
        { "mult",      GameKeywords.Mult(2) }
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

                int n = Mathf.Max(0, pendingRetriggerCount);
                if (n <= 0) yield break;
                // Replay her uygulamada yine n ekstra retrigger ister.
                ctx.RequestExtraDicePass(ctx.diceIndex, n * (1 + replayCount));
                break;
        }
    }
}
