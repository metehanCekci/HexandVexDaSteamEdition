using UnityEngine;

public class IronWillPerk : BasePerk
{
    private int cleanLevelStreak = 0; // Arka arkaya hasar almadan geçilen bölüm sayısı
    private bool tookDamageThisLevel = false;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Rare;
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
        // cleanLevelStreak sifirlanmaz — stash'ten cikarip takinca stack korunur
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        tookDamageThisLevel = false;
        description = GetDescription();
    }

    public override void OnLevelClear()
    {
        if (!tookDamageThisLevel)
        {
            cleanLevelStreak++;
        }
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
        tookDamageThisLevel = true;
        cleanLevelStreak = 0;
        description = GetDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (cleanLevelStreak <= 0) return;

        payload.multiplier += cleanLevelStreak;
        TriggerVisualPop();
    }

    private string GetDescription()
    {
        return $"Each level cleared without taking damage grants +1x damage multiplier. Resets on damage.\nStreak: {cleanLevelStreak} (+{cleanLevelStreak}x)";
    }
}
