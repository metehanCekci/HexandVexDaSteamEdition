using UnityEngine;
using System.Collections.Generic;

public class ToxinEdgePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(currentLevel) }
    };

    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        int delta = 0;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            payload.diceRolls[i] += currentLevel;
            delta += currentLevel;
        }
        payload.ApplyAdd(delta);
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
