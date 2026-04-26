using UnityEngine;
using System.Linq;

public class RiggedDicePerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "reroll",  GameKeywords.Action("rerolled") },
        { "highest", GameKeywords.Status("highest") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload.diceRolls.Count < 2) return;

        int minVal = payload.diceRolls.Min();
        int maxVal = payload.diceRolls.Max();

        if (minVal == maxVal) return; // Zaten aynÄ±ysa dokunma

        bool changed = false;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            if (payload.diceRolls[i] != maxVal)
            {
                payload.diceRolls[i] = maxVal;
                changed = true;
            }
        }

        if (changed) TriggerVisualPop();
    }
}
