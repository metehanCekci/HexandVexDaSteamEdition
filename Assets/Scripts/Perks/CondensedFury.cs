using UnityEngine;
using System.Collections.Generic;

public class CondensedFuryPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Roll {diceDelta} die but each die value is {mult}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "diceDelta", "-1" },
        { "mult", "X2" }
    };

    public int GetDiceReduction()
    {
        return 1;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            payload.diceRolls[i] *= 2;
        }
        if (payload.diceRolls.Count > 0)
            TriggerVisualPop();
    }
}
