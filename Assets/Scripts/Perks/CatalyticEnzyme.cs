using UnityEngine;

public class CatalyticEnzymePerk : BasePerk
{
    private int skipStacks = 0;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    public override void OnSkip()
    {
        skipStacks++;
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (skipStacks <= 0) return;
        // Each stack = +30% multiplier
        payload.multiplier *= 1f + (skipStacks * 0.3f);
        skipStacks = 0; // Consume on attack
    }

    public override void OnLevelStart()
    {
        skipStacks = 0;
    }
}
