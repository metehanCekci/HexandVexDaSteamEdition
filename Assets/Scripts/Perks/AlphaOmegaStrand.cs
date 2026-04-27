using UnityEngine;
using System.Collections.Generic;

public class AlphaOmegaStrandPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(2 * currentLevel, "damage") }
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
            int bonus = 2 * currentLevel;
            int delta = 0;

            payload.diceRolls[0] += bonus;
            delta += bonus;

            if (payload.diceRolls.Count > 1)
            {
                payload.diceRolls[payload.diceRolls.Count - 1] += bonus;
                delta += bonus;
            }
            payload.ApplyAdd(delta);

            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
