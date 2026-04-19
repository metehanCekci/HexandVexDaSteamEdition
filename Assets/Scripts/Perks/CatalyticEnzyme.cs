using UnityEngine;

public class CatalyticEnzymePerk : BasePerk
{
    private int skipStacks = 0;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public override void OnSkip()
    {
        skipStacks++;
        description = GetDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (skipStacks <= 0) return;
        // Each stack = +30% multiplier
        payload.multiplier *= 1f + (skipStacks * 0.3f);
        skipStacks = 0; // Consume on attack
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        skipStacks = 0;
        description = GetDescription();
    }

    private string GetDescription()
    {
        int bonus = Mathf.RoundToInt(skipStacks * 30);
        return $"Each skip grants +30% damage multiplier (stacks). Consumed on attack.\nStacks: {skipStacks} (+{bonus}%)";
    }
}
