using UnityEngine;
using System.Collections.Generic;

public class CondensedFuryPerk : BasePerk
{
    // TurnManager hesaplama sirasinda doldurur: "kac retrigger uygulanmali".
    // = (intendedDice - 1) cunku artik tek zar atiliyor.
    [System.NonSerialized] public int pendingRetriggerCount = 0;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "diceDelta", GameKeywords.Minus(1) },
        { "mult",      GameKeywords.Mult(2) }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        // Sadece atilan tek zar (index 0) icin retrigger uygula.
        if (diceIndex != 0) return 0;
        return Mathf.Max(0, pendingRetriggerCount);
    }
}
