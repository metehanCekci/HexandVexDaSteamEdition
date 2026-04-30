using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Mimetic Growth — Epic. Sagindaki implantin OnAttack'ini bir kez daha tetikler.
/// </summary>
public class MimeticGrowthPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "trigger", GameKeywords.Retrigger("triggering once more") }
    };

    public override bool IsIncompatible() => GetRightNeighbor() == null;
    public override string GetIncompatibleReason() => "Nothing to the right";

    private BasePerk GetRightNeighbor()
    {
        if (RunManager.instance == null) return null;
        var list = RunManager.instance.activePerks;
        int myIndex = list.IndexOf(this);
        if (myIndex < 0 || myIndex >= list.Count - 1) return null;

        var neighbor = list[myIndex + 1];
        if (neighbor == null) return null;
        if (neighbor is MimeticGrowthPerk || neighbor is LeftmostResonancePerk || neighbor is ParasiticChorusPerk)
            return null;
        return neighbor;
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        var neighbor = GetRightNeighbor();
        if (neighbor == null) yield break;

        ctx.RequestPerkReplay(neighbor);
        ctx.AnimatePop(this);
    }
}
