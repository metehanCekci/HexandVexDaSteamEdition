using UnityEngine;

public class AlphaOmegaStrandPerk : BasePerk
{
    // YENİ: Kart tekrar seçilirse seviye artsın
    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload.diceRolls.Count > 0)
        {
            // İlk zara seviye başına +2 ekler
            payload.diceRolls[0] += (2 * currentLevel);

            if (payload.diceRolls.Count > 1)
            {
                // Son zara seviye başına +2 ekler
                payload.diceRolls[payload.diceRolls.Count - 1] += (2 * currentLevel);
            }
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
