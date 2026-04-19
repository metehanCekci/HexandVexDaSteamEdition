using UnityEngine;
using System.Collections.Generic;

public class AlphaOmegaStrandPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "First and last die gain {bonus}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", $"+{2 * currentLevel}" }
    };

    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload.diceRolls.Count > 0)
        {
            payload.diceRolls[0] += (2 * currentLevel);

            if (payload.diceRolls.Count > 1)
            {
                payload.diceRolls[payload.diceRolls.Count - 1] += (2 * currentLevel);
            }
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
