using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

public class MutantSwarmPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Mult(0.5f * currentLevel) }
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

        payload.ApplyMult(extraMult);
        TriggerVisualPop();
    }
}
