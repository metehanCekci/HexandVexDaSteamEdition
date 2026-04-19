using UnityEngine;
using System.Collections.Generic;

public class MutantSwarmPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Each die rolled adds {bonus} multiplier.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", $"x{0.5f * currentLevel:0.##}" }
    };

    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        float bonusPerDie = 0.5f * currentLevel;
        float extraMult = 1.0f + (payload.diceRolls.Count * bonusPerDie);

        payload.multiplier *= extraMult;
        TriggerVisualPop();
    }
}
