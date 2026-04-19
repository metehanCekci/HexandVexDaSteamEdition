using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Cascade Protocol (Legendary)
/// Her saldırının zar toplamı (kritik/multiplier HARİÇ) bir sonraki saldırıya flat bonus olarak eklenir.
/// Lv1: %100, Lv2: %125, Lv3: %150 birikim oranı.
/// Oda bitince VEYA hasar alınca sıfırlanır.
/// </summary>
public class CascadeProtocolPerk : BasePerk
{
    private long accumulatedDamage = 0;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Each attack's dice sum carries forward as flat bonus to the next attack ({percent}). Resets on damage taken or level clear.\nAccumulated: {accumulated}";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "percent",     $"{100 + (currentLevel - 1) * 25}%" },
        { "accumulated", $"{accumulatedDamage} damage" }
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
            payload.flatBonus += (long)System.Math.Min(bonus, long.MaxValue - payload.flatBonus);
            TriggerVisualPop();
        }

        // Bu saldırının zar toplamını birikime ekle (kritik/mult hariç, sadece raw dice)
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
