using UnityEngine;
using System.Collections.Generic;

public class VolatileRollPerk : BasePerk
{
    private const int MAX_CHAIN = 50; // sonsuz dÃ¶ngÃ¼ korumasÄ±

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "one",    GameKeywords.Status("1") },
        { "six",    GameKeywords.Status("6") },
        { "reroll", GameKeywords.Action("re-rolls") }
    };

    /// <summary>
    /// Base zarlarÄ± 1 veya 6 olarak Ã¼retir. ShowDiceSequence'dan Ã–NCE Ã§aÄŸrÄ±lÄ±r.
    /// </summary>
    public void ApplyToBaseRolls(List<int> rolls)
    {
        for (int i = 0; i < rolls.Count; i++)
            rolls[i] = Random.value < 0.5f ? 1 : 6;
    }

    /// <summary>
    /// 6 gelen zarlardan zincirleme extra zarlar Ã¼retir.
    /// Her chain adÄ±mÄ±nda yeni zarlarÄ± dÃ¶ndÃ¼rÃ¼r (animasyon iÃ§in tek tek Ã§aÄŸrÄ±lacak).
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
