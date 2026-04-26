using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Parasitic Chorus â€” Legendary. Aktif slottaki her common implant icin, o common'un
/// sagindaki implantin efektini bir kez daha tetikler. Bir common en sagdaysa o atlanir.
/// Retriggerlanan implant baska bir perk-retrigger ise (Mimetic/Leftmost/Chorus) atlanir.
/// </summary>
public class ParasiticChorusPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "common", GameKeywords.Status("common") },
        { "retrig", GameKeywords.Retrigger("retriggers once") }
    };

    public override bool IsIncompatible() => CollectTargets().Count == 0;

    public override string GetIncompatibleReason() => "No common implants with a right neighbor";

    private List<BasePerk> CollectTargets()
    {
        var targets = new List<BasePerk>();
        if (RunManager.instance == null) return targets;
        var list = RunManager.instance.activePerks;

        for (int i = 0; i < list.Count; i++)
        {
            var common = list[i];
            if (common == null) continue;
            if (common.rarity != PerkRarity.Common) continue;
            if (i + 1 >= list.Count) continue; // En sagdaki common'un sagi yok

            var rightOfCommon = list[i + 1];
            if (rightOfCommon == null) continue;
            if (rightOfCommon == this) continue;          // Kendini retriggerlama
            if (rightOfCommon.isPerkRetrigger) continue;  // Baska perk-retrigger'i cagirma
            targets.Add(rightOfCommon);
        }
        return targets;
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        var targets = CollectTargets();
        if (targets.Count == 0) yield break;

        var processor = TurnManager.instance != null ? TurnManager.instance.PerkProcessor : null;
        if (processor == null) yield break;

        List<int> rolls = new List<int>(payload.diceRolls);

        foreach (var target in targets)
        {
            long before = payload.GetFinalDamage();
            yield return processor.RetriggerPerk(target, payload, rolls);
            long after = payload.GetFinalDamage();

            if (after != before)
            {
                TriggerVisualPop();
                if (PerkListUI.instance != null)
                    PerkListUI.instance.TriggerShakeForPerk(this);
            }
        }
    }
}
