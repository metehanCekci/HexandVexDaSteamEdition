using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Terminal Fury Gland (Legendary). Multiplier = 2 + (maxHP - currentHP).
/// </summary>
public class TerminalFuryGlandPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base",    GameKeywords.Mult(2f) },
        { "per",     GameKeywords.Mult(1f) },
        { "missing", GameKeywords.HealthText("missing HP") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        var rm = RunManager.instance;
        var tm = TurnManager.instance;
        if (rm == null || tm == null || tm.player == null) yield break;

        long maxHP = rm.playerMaxHealth;
        long currentHP = tm.player.health.currentHP;
        if (currentHP < 1) currentHP = 1;

        float tfgMult = 2f + (maxHP - currentHP);
        ctx.payload.ApplyMult(tfgMult);
        ctx.AnimatePop(this);
    }
}
