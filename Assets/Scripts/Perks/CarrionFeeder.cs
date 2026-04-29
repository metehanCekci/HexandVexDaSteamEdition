using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarrionFeederPerk : BasePerk
{
    private int killStreak = 0;
    private bool pendingReset = false;

    // Snapshot orijinal pass'te dondurulur, replay'de ayni streak'i uygular.
    private int streakSnapshot = 0;

    private int MaxStacks => currentLevel;

    // Replay aktif — Mimetic ile her replay killStreak kadar x2 uygulanir.
    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues()
    {
        float currentMultiplier = killStreak > 0 ? Mathf.Pow(2, killStreak) : 1;
        float maxMultiplier = Mathf.Pow(2, MaxStacks);
        return new Dictionary<string, object>
        {
            { "kill",    GameKeywords.Action("kill") },
            { "kill2",   GameKeywords.Action("kill") },
            { "max",     GameKeywords.Mult(maxMultiplier) },
            { "streak",  GameKeywords.Counter($"{killStreak}/{MaxStacks}") },
            { "current", GameKeywords.Mult(currentMultiplier) }
        };
    }

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        if (!ctx.isReplay)
        {
            // Reset/snapshot mantigi sadece orijinalde
            if (pendingReset)
                killStreak = 0;
            pendingReset = true;
            streakSnapshot = killStreak;
            RebuildDescription();
        }

        if (streakSnapshot <= 0) yield break;

        for (int i = 0; i < streakSnapshot; i++)
        {
            ctx.payload.ApplyMult(2f);
            ctx.AnimatePop(this);
            ctx.RefreshTotal();
            yield return ctx.WaitFor(0.3f);
        }

        if (!ctx.isReplay)
            RebuildDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (killStreak < MaxStacks)
            killStreak++;
        pendingReset = false;
        RebuildDescription();
        TriggerVisualPop();
    }
}
