using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Highroll Resonance — Rare. Triboulet çakmasi.
/// Her atilan 5 veya 6 icin runningDamage *= 1.5 (ARDISIK uygulanir).
/// Per-die: ilgili zar retriggerlandiginda her seferinde bir kez daha carpilir.
/// </summary>
public class HighrollResonancePerk : BasePerk
{
    private const float MultPerHighroll = 1.5f;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "five",  GameKeywords.Status("5") },
        { "six",   GameKeywords.Status("6") },
        { "mult",  GameKeywords.Mult(MultPerHighroll, "damage") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        switch (ctx.eventType)
        {
            case CombatEventType.OnAttack:
                if (ctx.currentPerk != this) yield break;
                bool anyHigh = false;
                for (int i = 0; i < ctx.payload.diceRolls.Count; i++)
                {
                    int v = ctx.payload.diceRolls[i];
                    if (v != 5 && v != 6) continue;
                    ctx.payload.ApplyMult(MultPerHighroll);
                    anyHigh = true;
                    ctx.AnimateDie(i);
                    ctx.AnimatePop(this);
                    yield return ctx.WaitFor(0.25f);
                }
                if (!anyHigh) yield break;
                break;

            case CombatEventType.OnDiceScored:
                if (ctx.retrigCountSoFar == 0) yield break;
                if (ctx.diceValue != 5 && ctx.diceValue != 6) yield break;
                ctx.payload.ApplyMult(MultPerHighroll);
                ctx.AnimatePop(this);
                break;
        }
    }
}
