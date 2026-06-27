using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Leftmost Resonance — Legendary. En soldaki implantin OnAttack'ini iki kez daha tetikler.
/// </summary>
public class LeftmostResonancePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "again", GameKeywords.RetriggerN(2) }
    };

    public override bool IsIncompatible() => GetLeftmostTarget() == null;
    public override string GetIncompatibleReason() => "No valid leftmost implant";

    private BasePerk GetLeftmostTarget()
    {
        if (RunManager.instance == null) return null;
        var list = RunManager.instance.activePerks;
        for (int i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate == null) continue;
            if (candidate == this) return null;
            if (candidate is MimeticGrowthPerk || candidate is LeftmostResonancePerk || candidate is ParasiticChorusPerk)
                return null;
            return candidate;
        }
        return null;
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        var target = GetLeftmostTarget();
        if (target == null) yield break;

        ctx.RequestPerkReplay(target);
        ctx.RequestPerkReplay(target);
        ctx.AnimatePop(this);
    }
}
