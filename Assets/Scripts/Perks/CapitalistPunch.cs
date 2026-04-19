using UnityEngine;
using System.Collections.Generic;

public class CapitalistPunchPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
        if (string.IsNullOrEmpty(description))
            description = "Every {cost} you have grants {bonus} to all dice.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "cost",  "5 gold" },
        { "bonus", "+1 damage" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        if (RunManager.instance == null) return;

        int bonus = RunManager.instance.currentGold / 5; // Her 5 altın için +1 hasar
        if (bonus > 0)
        {
            for (int i = 0; i < payload.diceRolls.Count; i++)
                payload.diceRolls[i] += bonus;
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
