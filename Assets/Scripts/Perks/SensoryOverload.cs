using System.Collections.Generic;

/// <summary>
/// Sensory Overload — Rare. 5 veya 6 gelen her zar bir kez daha retriggerlanir.
/// </summary>
public class SensoryOverloadPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Every {five} and {six} triggers {extra}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "five", "5" },
        { "six", "6" },
        { "extra", "X1 more time" }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return (diceValue == 5 || diceValue == 6) ? 1 : 0;
    }
}
