using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class DoubleOrNothingPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", GameKeywords.Mult(2) }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        int total = payload.diceRolls.Sum();

        if (total % 2 == 0)
        {
            payload.ApplyMult(2f);
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
