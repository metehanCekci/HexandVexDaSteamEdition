using System.Collections.Generic;

/// <summary>
/// Hanging Nerve â€” Common. Ilk atilan zar iki kez daha retriggerlanir
/// (toplamda ilk zarin +/x zinciri 3 kez uygulanir).
/// </summary>
public class HangingNervePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "retrigger", GameKeywords.RetriggerN(2) }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return diceIndex == 0 ? 2 : 0;
    }
}
