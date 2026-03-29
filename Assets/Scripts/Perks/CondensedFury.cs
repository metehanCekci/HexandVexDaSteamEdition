using UnityEngine;

public class CondensedFuryPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        maxLevel = 1;
    }

    /// <summary>
    /// TurnManager tarafından çağrılır — zar sayısını 1 azaltır.
    /// </summary>
    public int GetDiceReduction()
    {
        return 1;
    }

    /// <summary>
    /// Zar 1'e düşünce atılan zarı 2 katına çıkarır (toplam değil, zar değeri).
    /// </summary>
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
