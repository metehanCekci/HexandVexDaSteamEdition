using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Parasitic Chorus — Legendary. Aktif slottaki her common implant icin, o common'un
/// sagindaki implantin OnAttack'ini bir kez daha tetikler.
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
            if (i + 1 >= list.Count) continue;

            var rightOfCommon = list[i + 1];
            if (rightOfCommon == null) continue;
            if (rightOfCommon == this) continue;
            if (rightOfCommon is MimeticGrowthPerk || rightOfCommon is LeftmostResonancePerk || rightOfCommon is ParasiticChorusPerk)
                continue;
            targets.Add(rightOfCommon);
        }
        return targets;
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        var targets = CollectTargets();
        if (targets.Count == 0) yield break;

        foreach (var target in targets)
            ctx.RequestPerkReplay(target);

        ctx.AnimatePop(this);
    }
}
