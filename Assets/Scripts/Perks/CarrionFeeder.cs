using UnityEngine;

public class CarrionFeederPerk : BasePerk
{
    private int stacks = 0;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        // Heal 1 HP
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            TurnManager.instance.player.health.Heal(1);
            if (RunManager.instance != null)
                RunManager.instance.playerCurrentHealth = TurnManager.instance.player.health.currentHP;
        }

        // Stack +2 flat bonus for next attack
        stacks += 2;
        description = GetDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (stacks <= 0) return;
        payload.flatBonus += stacks;
        stacks = 0; // Consume stacks on attack
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        stacks = 0;
        description = GetDescription();
    }

    private string GetDescription()
    {
        return $"Heal 1 HP on kill. Each kill grants +2 flat damage for next attack (stacks).\nStacks: {stacks} (+{stacks} dmg)";
    }
}
