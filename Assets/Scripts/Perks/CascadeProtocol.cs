using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cascade Protocol (Legendary)
/// Her saldÄ±rÄ±nÄ±n zar toplamÄ± (kritik/multiplier HARÄ°Ã‡) bir sonraki saldÄ±rÄ±ya flat bonus olarak eklenir.
/// Lv1: %100, Lv2: %125, Lv3: %150 birikim oranÄ±.
/// Oda bitince VEYA hasar alÄ±nca sÄ±fÄ±rlanÄ±r.
/// </summary>
public class CascadeProtocolPerk : BasePerk
{
    private long accumulatedDamage = 0;
    private bool subscribed = false;

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

    public override void ModifyCombat(CombatPayload payload)
    {
        if (accumulatedDamage > 0)
        {
            double bonus = accumulatedDamage * (1.0 + (currentLevel - 1) * 0.25);
            payload.ApplyAdd(bonus);
            TriggerVisualPop();
        }

        // Bu saldÄ±rÄ±nÄ±n zar toplamÄ±nÄ± birikime ekle (kritik/mult hariÃ§, sadece raw dice)
        long diceSum = payload.diceRolls.Sum();
        accumulatedDamage += diceSum;
        RebuildDescription();
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
