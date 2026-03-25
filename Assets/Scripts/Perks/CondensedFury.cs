using UnityEngine;

public class CondensedFuryPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
    }

    /// <summary>
    /// TurnManager tarafından çağrılır — zar sayısını 1 azaltır.
    /// </summary>
    public int GetDiceReduction()
    {
        return 1;
    }

    /// <summary>
    /// Kalan her zara +3 flat damage (+1 per level) ekler.
    /// Lv1: +3, Lv2: +4, Lv3: +5
    /// </summary>
    public override void ModifyCombat(CombatPayload payload)
    {
        int bonusPerDie = 2 + currentLevel;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            payload.diceRolls[i] += bonusPerDie;
        }
        if (payload.diceRolls.Count > 0)
            TriggerVisualPop();
    }
}
