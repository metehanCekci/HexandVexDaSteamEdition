using UnityEngine;
using System.Collections.Generic;

public class ApexPredatorPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        if (string.IsNullOrEmpty(description))
            description = "Deal {mult} damage, but lose {penalty} per die rolled.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X5" },
        { "penalty", "X1" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.multiplier *= 5.0f;

        float penalty = payload.diceRolls.Count * 1.0f;
        payload.multiplier -= penalty;

        payload.multiplier = Mathf.Max(payload.multiplier, 1.0f);

        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
