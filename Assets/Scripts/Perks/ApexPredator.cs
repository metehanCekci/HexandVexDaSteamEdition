using UnityEngine;
using System.Collections.Generic;

public class ApexPredatorPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult",    GameKeywords.Mult(5) },
        { "penalty", GameKeywords.Mult(1) }
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
