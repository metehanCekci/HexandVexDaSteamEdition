using UnityEngine;

public class VolatileRollPerk : BasePerk
{
    private const int MAX_CHAIN = 50; // sonsuz döngü koruması

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
        isRerollPerk = true;
        priority = -10; // Run early so other perks see the final 1/6 values
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        // Mevcut zarları 1 veya 6 yap
        for (int i = 0; i < payload.diceRolls.Count; i++)
            payload.diceRolls[i] = Random.value < 0.5f ? 1 : 6;

        // Zincirleme: 6 gelen her zar +1 ekstra zar üretir
        int startIndex = 0;
        int chainCount = 0;
        while (chainCount < MAX_CHAIN)
        {
            int newSixes = 0;
            for (int i = startIndex; i < payload.diceRolls.Count; i++)
                if (payload.diceRolls[i] == 6) newSixes++;

            if (newSixes == 0) break;

            startIndex = payload.diceRolls.Count;
            for (int i = 0; i < newSixes; i++)
                payload.diceRolls.Add(Random.value < 0.5f ? 1 : 6);

            chainCount++;
        }

        TriggerVisualPop();
    }
}
