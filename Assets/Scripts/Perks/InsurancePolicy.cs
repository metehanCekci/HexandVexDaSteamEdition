using UnityEngine;
using System.Collections.Generic;

public class InsurancePolicyPerk : BasePerk
{
    private bool subscribed = false;
    private long previousHP;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "amount", GameKeywords.PlusGold((currentLevel + 1) * 2) },
        { "missing", GameKeywords.HealthText("missing HP") }
    };

    public override void OnAcquire()
    {
        Subscribe();
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        TrackHP();
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
            TrackHP();
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

    private void TrackHP()
    {
        if (TurnManager.instance != null && TurnManager.instance.player != null)
            previousHP = TurnManager.instance.player.health.currentHP;
    }

    private void OnPlayerDamaged(long remainingHP)
    {
        long lost = previousHP - remainingHP;
        if (lost <= 0) { previousHP = remainingHP; return; }

        long maxHP = TurnManager.instance.player.health.maxHP;
        long missingHP = maxHP - remainingHP;
        int goldPerMissing = (currentLevel + 1) * 2; // Lv1: 4, Lv2: 6, Lv3: 8
        long goldGain = goldPerMissing * missingHP;

        if (RunManager.instance != null)
        {
            RunManager.instance.currentGold += (int)System.Math.Min(goldGain, int.MaxValue);
            if (TurnManager.instance != null) TurnManager.instance.UpdateCoinUI();
        }

        previousHP = remainingHP;
        TriggerVisualPop();
    }
}
