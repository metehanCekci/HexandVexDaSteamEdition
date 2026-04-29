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

    // Lets Go Again now retriggers each perk inline (right after that perk fires) inside
    // CombatPipeline.RunPerkAttack — Balatro-style. This pass used to run a full second
    // pipeline pass; that's superseded, so this method is a no-op kept so the existing
    // TurnManager call sites stay valid until they're cleaned up.
    public IEnumerator ProcessLetsGoAgainPass(CombatPayload payload, List<int> rolls)
    {
        yield break;
    }

}
