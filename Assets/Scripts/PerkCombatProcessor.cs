using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// =========================================================================
// PerkCombatProcessor — facade over CombatPipeline.
// =========================================================================
// Eski API'yi (ProcessPerks / ProcessLetsGoAgainPass) korur ki TurnManager kirilmasin.
// Asil mantik CombatPipeline'da event-driven olarak yasiyor.
// Mimetic/Leftmost/Parasitic gibi perk-retrigger perkleri artik switch-case ile
// hardcoded degil — perk ctx.RequestPerkReplay(target) cagirir, pipeline halleder.
// =========================================================================
public class PerkCombatProcessor : MonoBehaviour
{
    private DiceUIController diceUI;
    private CombatPipeline pipeline;

    public void Initialize(DiceUIController diceUIController)
    {
        diceUI = diceUIController;
        pipeline = new CombatPipeline(diceUI);
    }

    public IEnumerator ProcessPerks(CombatPayload payload, List<int> rolls)
    {
        if (pipeline == null) yield break;
        if (RunManager.instance == null || RunManager.instance.activePerks.Count == 0) yield break;

        yield return StartCoroutine(pipeline.RunBaseCombat(payload, rolls));
    }

    public IEnumerator ProcessLetsGoAgainPass(CombatPayload payload, List<int> rolls)
    {
        if (pipeline == null) yield break;
        if (RunManager.instance == null) yield break;
        if (!RunManager.instance.activePerks.Exists(p => p is LetsGoAgainPerk)) yield break;

        var lgaPerk = RunManager.instance.activePerks.Find(p => p is LetsGoAgainPerk);
        if (lgaPerk != null && diceUI != null && !diceUI.skipDiceVisuals)
        {
            lgaPerk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(lgaPerk);
            yield return StartCoroutine(diceUI.SkippableWait(0.8f));
        }

        // Pass reset — runningDamage'i zar tabanindan yeniden baslat.
        payload.RebaseRunningDamage();
        for (int i = 0; i < payload.diceRetriggerCounts.Count; i++)
            payload.diceRetriggerCounts[i] = 0;
        while (payload.diceRetriggerCounts.Count < rolls.Count)
            payload.diceRetriggerCounts.Add(0);

        yield return StartCoroutine(pipeline.RunLetsGoAgain(payload, rolls));

        var sfPerk = RunManager.instance.activePerks.Find(p => p is SymbioticFuryPerk);
        if (sfPerk != null && diceUI != null && !diceUI.skipDiceVisuals)
        {
            sfPerk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(sfPerk);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return StartCoroutine(diceUI.SkippableWait(0.6f));
        }
    }

}
