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
    }

    public override void OnEquip()
    {
        Subscribe();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        cleanLevelStreak = 0;
        tookDamageThisLevel = false;
    }

    public override void OnLevelStart()
    {
        Subscribe();
        tookDamageThisLevel = false;
    }

    public override void OnLevelClear()
    {
        if (!tookDamageThisLevel)
        {
            cleanLevelStreak++;
        }
        // Hasar yediyse streak zaten OnPlayerDamaged'da sıfırlanmış olacak
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
    }

    /// <summary>
    /// Her hasar almadan geçilen bölüm +1x çarpan ekler.
    /// 7 bölüm hasar almadan = 8x çarpan (1 base + 7 streak).
    /// Hasar yiyince streak sıfırlanır.
    /// </summary>
    public override void ModifyCombat(CombatPayload payload)
    {
        if (cleanLevelStreak <= 0) return;

        payload.multiplier += cleanLevelStreak; // streak kadar ekstra çarpan
        TriggerVisualPop();
    }
}
