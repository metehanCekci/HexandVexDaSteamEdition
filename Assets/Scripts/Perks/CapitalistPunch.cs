using UnityEngine;

public class CapitalistPunchPerk : BasePerk
{
    void OnEnable() { maxLevel = 1; rarity = PerkRarity.Legendary; description = "Every 5 gold you have grants +1 damage to all dice."; }

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
