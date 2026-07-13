using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cascade Protocol (Legendary)
/// Her saldirinin zar toplami bir sonraki saldiriya flat bonus olarak eklenir.
/// Lv1: %100, Lv2: %125, Lv3: %150 birikim orani.
/// Oda bitince VEYA hasar alinca sifirlanir.
/// Mimetic ile replay olunca: bonus 2x uygulanir, ama birikim sadece orijinalde 1 kez yapilir.
/// </summary>
public class CascadeProtocolPerk : BasePerk
{
    private long accumulatedDamage = 0;
    private bool subscribed = false;

    // Replay aktif — sadece bonus 2x, birikim 1x.
    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attack",      GameKeywords.Action("attack") },
        { "percent",     $"{100 + (currentLevel - 1) * 25}%" },
        { "cleared",     GameKeywords.Action("level cleared") },
        { "accumulated", GameKeywords.Counter($"{accumulatedDamage} damage") }
    };

    public override void OnAcquire()
    {
        Subscribe();
        RebuildDescription();
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        accumulatedDamage = 0;
        RebuildDescription();
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        if (accumulatedDamage > 0)
        {
            double bonus = accumulatedDamage * (1.0 + (currentLevel - 1) * 0.25);
            ctx.payload.ApplyAdd(bonus);
            ctx.AnimatePop(this);
        }

        // Birikim sadece orijinalde — replay birikime eklemez.
        if (!ctx.isReplay)
        {
            long diceSum = ctx.payload.diceRolls.Sum();
            accumulatedDamage += diceSum;
            RebuildDescription();
        }
    }

    public override void OnLevelStart()
    {
        Subscribe();
        accumulatedDamage = 0;
        RebuildDescription();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.health.OnDamaged += OnPlayerDamaged;
            subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (TurnManager.instance != null && TurnManager.instance.player != null
            && TurnManager.instance.player.health != null)
        {
            TurnManager.instance.player.health.OnDamaged -= OnPlayerDamaged;
        }
        subscribed = false;
    }

    private void OnPlayerDamaged(long remainingHP)
    {
        accumulatedDamage = 0;
        RebuildDescription();
    }
}
