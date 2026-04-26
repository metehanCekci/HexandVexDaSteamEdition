using System.Collections.Generic;

/// <summary>
/// Hanging Nerve â€” Epic. Ilk atilan zar bir kez daha retriggerlanir
/// (toplamda ilk zarin +/x zinciri 2 kez uygulanir).
/// </summary>
public class HangingNervePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "retrigger", GameKeywords.RetriggerN(1) }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return diceIndex == 0 ? 1 : 0;
    }
}
