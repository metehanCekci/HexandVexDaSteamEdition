using UnityEngine;

public class VolatileRollPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        isRerollPerk = true;
        priority = -10; // Run early so other perks see the final 1/6 values
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        bool anyChanged = false;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            int original = payload.diceRolls[i];
            int newVal = Random.value < 0.5f ? 1 : 6;
            if (newVal != original)
            {
                payload.diceRolls[i] = newVal;
                anyChanged = true;
            }
        }
        if (anyChanged)
            TriggerVisualPop();
    }
}
