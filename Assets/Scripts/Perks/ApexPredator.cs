using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ApexPredatorPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues()
    {
        int dice = GetCurrentDiceCount();
        float netMult = Mathf.Max(5.0f - dice, 1.0f);

        return new Dictionary<string, object>
        {
            { "mult",    GameKeywords.Mult(5) },
            { "penalty", GameKeywords.Mult(1) },
            { "current", GameKeywords.Mult(netMult) }
        };
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        float penalty = ctx.payload.diceRolls.Count * 1.0f;
        float netMult = Mathf.Max(5.0f - penalty, 1.0f);
        ctx.payload.ApplyMult(netMult);
        ctx.AnimatePop(this);
    }

    int GetCurrentDiceCount()
    {
        var rm = RunManager.instance;
        if (rm == null) return 2;

        if (rm.activePerks.Exists(p => p is CondensedFuryPerk)) return 1;

        int count = rm.baseDiceCount;
        count += rm.bonusDiceNextCombat;

        foreach (var p in rm.activePerks)
            if (p is DiceHoarderPerk hoardPerk) count += hoardPerk.visitedLevels;
        foreach (var p in rm.inventoryPerks)
            if (p is DiceHoarderPerk hoardPerk) count += hoardPerk.visitedLevels;

        return Mathf.Max(1, count);
    }
}
