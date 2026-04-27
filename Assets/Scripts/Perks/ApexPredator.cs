using UnityEngine;
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

    public override void ModifyCombat(CombatPayload payload)
    {
        // Balatro model: net carpan = max(5 - zarSayisi, 1). Tek bir ApplyMult cagrisi.
        float penalty = payload.diceRolls.Count * 1.0f;
        float netMult = Mathf.Max(5.0f - penalty, 1.0f);
        payload.ApplyMult(netMult);

        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }

    // Tooltip "Currently: X?" degeri icin kalici/ongorulebilir zar sayisini hesaplar.
    // Kombat-ani dinamikleri (DormantSpore stored, green tile, HostSyndrome komsu sayaci) atlanir.
    int GetCurrentDiceCount()
    {
        var rm = RunManager.instance;
        if (rm == null) return 2;

        // Condensed Fury aktifse her zaman 1 zar atilir (kalanlar retrigger olur).
        if (rm.activePerks.Exists(p => p is CondensedFuryPerk)) return 1;

        int count = rm.baseDiceCount;
        count += rm.bonusDiceNextCombat;

        foreach (var p in rm.activePerks)
        {
            if (p is DiceHoarderPerk hoardPerk) count += hoardPerk.visitedLevels;
        }
        foreach (var p in rm.inventoryPerks)
        {
            if (p is DiceHoarderPerk hoardPerk) count += hoardPerk.visitedLevels;
        }

        return Mathf.Max(1, count);
    }
}
