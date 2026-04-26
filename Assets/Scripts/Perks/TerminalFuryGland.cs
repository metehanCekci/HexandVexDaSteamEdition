using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Terminal Fury Gland (Legendary)
/// Always active. Multiplier = maxHP / currentHP
/// 5/5 HP = 1x, 1/5 HP = 5x
/// </summary>
public class TerminalFuryGlandPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base",    GameKeywords.Mult(2f) },
        { "per",     GameKeywords.Mult(1f) },
        { "missing", GameKeywords.HealthText("missing HP") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        var rm = RunManager.instance;
        var tm = TurnManager.instance;
        if (rm == null || tm == null || tm.player == null) return;

        long maxHP = rm.playerMaxHealth;
        long currentHP = tm.player.health.currentHP;
        if (currentHP < 1) currentHP = 1;

        float tfgMult = 2f + (maxHP - currentHP);

        // Balatro model: her perk bagimsiz calisir. Eski GlassCanon coexistence hack'i kaldirildi â€”
        // iki perk ayri ayri ApplyMult yapiyor, inspector sirasina gore ardisik uygulanir.
        payload.ApplyMult(tfgMult);

        TriggerVisualPop();
    }
}
