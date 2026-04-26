using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Photovoltaic Pulse â€” Rare. Ilk zarin degeri kadar multiplier uygular (runningDamage *= first).
/// Ilk zar dice-retrigger perkleriyle (Hanging Nerve, Stelzer, Sensory + Mimetic/Leftmost/Parasitic
/// uzerinden kopyalananlar) her retriggerlaninca multiplier'i bir kez daha ARDISIK uygular.
///
/// Pipeline ornegi: zar 3, Photovoltaic, Hanging Nerve (+2 retrigger):
///   base     -> runningDamage = 3,   Photovoltaic x3 -> 9
///   retrig 1 -> +3 (= 12), Photovoltaic x3 (= 36)
///   retrig 2 -> +3 (= 39), Photovoltaic x3 (= 117)
///
/// Pattern: per-die bagimli perkler BasePerk.OnDiceRetriggerEvent hook'unu override eder.
/// </summary>
public class PhotovoltaicPulsePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "first",  GameKeywords.Status("first die") },
        { "retrig", GameKeywords.Retrigger("retriggers") }
    };

    public override bool IsIncompatible() => false;

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload == null || payload.diceRolls == null || payload.diceRolls.Count == 0) return;
        int first = payload.diceRolls[0];
        if (first <= 0) return;
        payload.ApplyMult(first);
    }

    /// <summary>
    /// Ilk zar her retriggerlandiginda multiplier'i bir kez daha ARDISIK uygula.
    /// runningDamage += value (processor yapmis olur) sonra BU hook calisir: runningDamage *= value.
    /// </summary>
    public override void OnDiceRetriggerEvent(int diceIndex, int diceValue, CombatPayload payload)
    {
        if (diceIndex != 0) return;
        if (diceValue <= 0) return;
        payload.ApplyMult(diceValue);
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        TriggerVisualPop();
        if (PerkListUI.instance != null)
            PerkListUI.instance.TriggerShakeForPerk(this);

        if (diceUI != null && !diceUI.skipDiceVisuals)
        {
            if (diceUI.SpawnedDiceUI != null && diceUI.SpawnedDiceUI.Count > 0 && payload.diceRolls.Count > 0)
                diceUI.AnimateSpecificDie(0, payload.diceRolls[0]);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return StartCoroutine(diceUI.SkippableWait(0.3f));
        }
    }
}
