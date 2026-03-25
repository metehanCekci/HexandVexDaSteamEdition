using UnityEngine;

public class IronWillPerk : BasePerk
{
    private bool tookDamageThisLevel = false;
    private bool buffActive = false;
    private bool subscribed = false;

    void OnEnable()
    {
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
        buffActive = false;
        tookDamageThisLevel = false;
    }

    public override void OnLevelStart()
    {
        Subscribe();
        // Previous level was clean -> buff activates
        if (!tookDamageThisLevel && buffActive == false)
        {
            // First level acquired: no buff yet (need to survive one clean level first)
        }
        tookDamageThisLevel = false;
    }

    public override void OnLevelClear()
    {
        // Finished level without damage -> activate buff for next combat
        buffActive = !tookDamageThisLevel;
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
        buffActive = false;
    }

    /// <summary>
    /// Buff active: Lv1 +2x, Lv2 +3x, Lv3 +4x
    /// </summary>
    public override void ModifyCombat(CombatPayload payload)
    {
        if (!buffActive) return;

        float bonus = 1f + currentLevel; // Lv1: 2, Lv2: 3, Lv3: 4
        payload.multiplier += bonus;
        TriggerVisualPop();

        // Consume buff after use — need another clean level
        buffActive = false;
    }
}
