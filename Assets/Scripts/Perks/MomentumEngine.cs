using UnityEngine;

public class MomentumEnginePerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "bonus", GameKeywords.Plus(1) }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        // EÄŸer TurnManager yoksa veya hiÃ§ yÃ¼rÃ¼mediysek boÅŸuna yorma
        if (TurnManager.instance == null || TurnManager.instance.hexesMovedThisTurn <= 0) return;

        int stepsTaken = TurnManager.instance.hexesMovedThisTurn;

        // BÃ¼tÃ¼n zarlara atÄ±lan adÄ±m kadar deÄŸer ekle
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            payload.diceRolls[i] += stepsTaken;
        }
        payload.ApplyAdd(stepsTaken * payload.diceRolls.Count);

        // Ã‡alÄ±ÅŸtÄ±ÄŸÄ±nÄ± belli etmek iÃ§in hoplat
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
