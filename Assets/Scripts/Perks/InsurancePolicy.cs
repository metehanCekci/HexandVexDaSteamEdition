using UnityEngine;

public class InsurancePolicyPerk : BasePerk
{
    private bool subscribed = false;
    private int previousHP;

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Rare;
    }

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

    private void OnPlayerDamaged(int remainingHP)
    {
        int lost = previousHP - remainingHP;
        if (lost <= 0) { previousHP = remainingHP; return; }

        int maxHP = TurnManager.instance.player.health.maxHP;
        int missingHP = maxHP - remainingHP;
        int goldPerMissing = currentLevel + 1; // Lv1: 2, Lv2: 3, Lv3: 4
        int goldGain = goldPerMissing * missingHP;

        if (RunManager.instance != null)
        {
            RunManager.instance.currentGold += goldGain;
            if (TurnManager.instance != null) TurnManager.instance.UpdateCoinUI();
        }

        previousHP = remainingHP;
        TriggerVisualPop();
    }
}
