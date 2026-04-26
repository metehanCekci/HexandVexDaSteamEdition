using System.Collections.Generic;

/// <summary>
/// Sensory Overload â€” Rare. 5 veya 6 gelen her zar bir kez daha retriggerlanir.
/// </summary>
public class SensoryOverloadPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "five",  GameKeywords.Status("5") },
        { "six",   GameKeywords.Status("6") },
        { "extra", GameKeywords.RetriggerN(1) }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return (diceValue == 5 || diceValue == 6) ? 1 : 0;
    }
}
