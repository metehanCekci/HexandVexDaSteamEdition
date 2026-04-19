using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class DoubleOrNothingPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "If dice sum is even, deal {mult} damage.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "x2" }
    };

    public override void OnAcquire()
    {
        priority = 50;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        int total = payload.diceRolls.Sum();

        if (total % 2 == 0)
        {
            payload.multiplier *= 2f;
            if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
                TriggerVisualPop();
        }
    }
}
