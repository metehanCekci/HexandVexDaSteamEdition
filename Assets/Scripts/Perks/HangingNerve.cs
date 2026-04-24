using System.Collections.Generic;

/// <summary>
/// Hanging Nerve — Common. Ilk atilan zar iki kez daha retriggerlanir
/// (toplamda ilk zarin +/x zinciri 3 kez uygulanir).
/// </summary>
public class HangingNervePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "The first die triggers {extra}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "extra", "X2 more times" }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return diceIndex == 0 ? 2 : 0;
    }
}
