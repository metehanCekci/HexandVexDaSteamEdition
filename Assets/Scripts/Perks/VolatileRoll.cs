using UnityEngine;
using System.Collections.Generic;

public class VolatileRollPerk : BasePerk
{
    private const int MAX_CHAIN = 50; // sonsuz döngü koruması

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
    }

    /// <summary>
    /// Base zarları 1 veya 6 olarak üretir. ShowDiceSequence'dan ÖNCE çağrılır.
    /// </summary>
    public void ApplyToBaseRolls(List<int> rolls)
    {
        for (int i = 0; i < rolls.Count; i++)
            rolls[i] = Random.value < 0.5f ? 1 : 6;
    }

    /// <summary>
    /// 6 gelen zarlardan zincirleme extra zarlar üretir.
    /// Her chain adımında yeni zarları döndürür (animasyon için tek tek çağrılacak).
    /// </summary>
    public List<int> GenerateChainRolls(List<int> allRolls, int startIndex)
    {
        List<int> newRolls = new List<int>();
        int newSixes = 0;
        for (int i = startIndex; i < allRolls.Count; i++)
            if (allRolls[i] == 6) newSixes++;

        for (int i = 0; i < newSixes && allRolls.Count + newRolls.Count < startIndex + MAX_CHAIN; i++)
            newRolls.Add(Random.value < 0.5f ? 1 : 6);

        return newRolls;
    }
}
