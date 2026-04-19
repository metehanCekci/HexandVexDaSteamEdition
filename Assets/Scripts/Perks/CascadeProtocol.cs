using UnityEngine;
using System.Linq;

/// <summary>
/// Cascade Protocol (Legendary)
/// Her saldırının zar toplamı (kritik/multiplier HARİÇ) bir sonraki saldırıya flat bonus olarak eklenir.
/// Lv1: %100, Lv2: %125, Lv3: %150 birikim oranı.
/// Oda bitince VEYA hasar alınca sıfırlanır.
/// </summary>
public class CascadeProtocolPerk : BasePerk
{
    private int accumulatedDamage = 0;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
        Subscribe();
        description = GetDescription();
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        accumulatedDamage = 0;
        description = GetDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (accumulatedDamage > 0)
        {
            float bonus = accumulatedDamage * (1f + (currentLevel - 1) * 0.25f);
            payload.flatBonus += Mathf.FloorToInt(bonus);
            TriggerVisualPop();
        }

        // Bu saldırının zar toplamını birikime ekle (kritik/mult hariç, sadece raw dice)
        int diceSum = payload.diceRolls.Sum();
        accumulatedDamage += diceSum;
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        accumulatedDamage = 0;
        description = GetDescription();
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

    private void OnPlayerDamaged(int remainingHP)
    {
        accumulatedDamage = 0;
        description = GetDescription();
    }

    private string GetDescription()
    {
        int percent = 100 + (currentLevel - 1) * 25;
        return $"Each attack's dice sum carries forward as flat bonus to the next attack ({percent}%). Resets on damage taken or level clear.\nAccumulated: {accumulatedDamage}";
    }
}
