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
        // Balatro model: net carpan = max(5 - zarSayisi, 1). Tek bir ApplyMult cagrisi.
        float penalty = payload.diceRolls.Count * 1.0f;
        float netMult = Mathf.Max(5.0f - penalty, 1.0f);
        payload.ApplyMult(netMult);

        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
