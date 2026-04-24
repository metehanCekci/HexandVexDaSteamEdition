using UnityEngine;
using System.Collections.Generic;

public class CatalyticEnzymePerk : BasePerk
{
    private int skipStacks = 0;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Each skip grants {per} damage multiplier (stacks). Consumed on attack.\nStacks: {count} ({bonus})";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "per",   "X1.3" },
        { "count", skipStacks },
        { "bonus", $"+{Mathf.RoundToInt(skipStacks * 30)}%" }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnSkip()
    {
        skipStacks++;
        RebuildDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (skipStacks <= 0) return;
        // Each stack = +30% multiplier
        payload.ApplyMult(1f + (skipStacks * 0.3f));
        skipStacks = 0; // Consume on attack
        RebuildDescription();
    }

    public override void OnLevelStart()
    {
        skipStacks = 0;
        RebuildDescription();
    }
}
