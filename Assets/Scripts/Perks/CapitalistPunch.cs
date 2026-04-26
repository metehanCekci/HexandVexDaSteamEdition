using UnityEngine;
using System.Collections.Generic;

public class CapitalistPunchPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "cost",  GameKeywords.Gold(5) },
        { "bonus", GameKeywords.Plus(1, "damage") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        if (RunManager.instance == null) return;

        int bonus = RunManager.instance.currentGold / 5; // Her 5 altÄ±n iÃ§in +1 hasar
        if (bonus > 0)
        {
            for (int i = 0; i < payload.diceRolls.Count; i++)
                payload.diceRolls[i] += bonus;
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
