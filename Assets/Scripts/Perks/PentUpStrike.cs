using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pent-Up Strike (Epic)
/// Normal saldiri 0 hasar verir (knockback kalir), zar toplamini biriktirir.
/// Skip ile saldirdiginda biriken tum hasari tek seferde verir.
/// Mimetic ile replay olunca iki kez release / iki kez biriktirir.
/// </summary>
public class PentUpStrikePerk : BasePerk
{
    [HideInInspector] public long storedDamage = 0;
    [HideInInspector] public long storedStacks = 0;
    [HideInInspector] public bool isReleasing = false;

    // Snapshot — orijinal pass'te dondurulur, replay bundan okur ki state mutasyonu
    // her cagride dogru sekilde uygulansin.
    private long releaseSnapshot = 0;
    private bool releaseSnapshotMode = false;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attack",  GameKeywords.Action("Attacks") },
        { "zero",    GameKeywords.Plus(0, "damage") },
        { "push",    GameKeywords.Action("knockback") },
        { "skip",    GameKeywords.Action("Skip") },
        { "percent", GameKeywords.Plus(50 + currentLevel * 50, "%") },
        { "stored",  GameKeywords.Counter($"{storedDamage} damage") },
        { "stacks",  GameKeywords.Counter(storedStacks.ToString()) }
    };

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
            // Orijinal pass: snapshot'i dondur
            releaseSnapshotMode = isReleasing;
            releaseSnapshot = storedDamage;
        }

        if (releaseSnapshotMode)
        {
            // Release: snapshot'tan bonus uygula. Mimetic ile replay'de aynisi tekrar.
            double bonus = releaseSnapshot * (0.5 + currentLevel * 0.5);
            ctx.payload.ApplyAdd(bonus);
            ctx.AnimatePop(this);

            if (!ctx.isReplay)
            {
                // Sifirlama sadece orijinalde
                storedDamage = 0;
                storedStacks = 0;
                isReleasing = false;
                RebuildDescription();
            }
        }
        else
        {
            // Biriktir: snapshot'taki zar toplamini biriktir.
            // Mimetic ile replay'de ayni miktari bir kez daha biriktirir (iki kez stack).
            long diceSum = ctx.payload.diceRolls.Sum();
            storedDamage += diceSum;
            storedStacks++;

            // Damage kovasini sifirla — processLast=true oldugumuz icin onceki perkler bitti.
            for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
                ctx.payload.diceRolls[i] = 0;
            ctx.payload.runningDamage = 0.0;

            RebuildDescription();
            ctx.AnimatePop(this);
        }
    }

    public override void OnSkip()
    {
        if (storedDamage > 0)
            isReleasing = true;
    }

    public override void OnLevelStart()
    {
        storedDamage = 0;
        storedStacks = 0;
        isReleasing = false;
        RebuildDescription();
    }
}
