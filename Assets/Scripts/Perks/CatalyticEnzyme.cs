using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CatalyticEnzymePerk : BasePerk
{
    private int skipStacks = 0;

    // Snapshot — orijinal pass'te dondurulur, replay'ler ayni stack degerini uygular,
    // sifirlama orijinal pass'in sonunda yapilir.
    private int stacksSnapshot = 0;

    // Replay aktif — Mimetic catalytic'i replay edince ekstra carpan uygular.
    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "skip",   GameKeywords.Action("skip") },
        { "per",    GameKeywords.Mult(1.3f) },
        { "attack", GameKeywords.Action("attack") },
        { "count",  GameKeywords.Counter(skipStacks.ToString()) },
        { "bonus",  GameKeywords.Mult(1f + (skipStacks * 0.3f)) }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnSkip()
    {
        skipStacks++;
        RebuildDescription();
        TriggerVisualPop();
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        if (!ctx.isReplay)
            stacksSnapshot = skipStacks;

        if (stacksSnapshot <= 0) yield break;

        ctx.payload.ApplyMult(1f + (stacksSnapshot * 0.3f));
        ctx.AnimatePop(this);

        // Sifirlama sadece orijinal pass'te
        if (!ctx.isReplay)
        {
            skipStacks = 0;
            RebuildDescription();
        }
    }

    public override void OnLevelStart()
    {
        skipStacks = 0;
        RebuildDescription();
    }
}
