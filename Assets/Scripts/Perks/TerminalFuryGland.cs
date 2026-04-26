using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Terminal Fury Gland (Legendary)
/// Always active. Multiplier = maxHP / currentHP
/// 5/5 HP = 1x, 1/5 HP = 5x
/// </summary>
public class TerminalFuryGlandPerk : BasePerk
{
    void OnEnable()
    {
        rarity      = PerkRarity.Legendary;
        maxLevel    = 1;
        priority    = 15;
        if (string.IsNullOrEmpty(description))
            description = "Always deal {base} damage, get {per} mult per missing health.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base", "X2" },
        { "per",  "+1" }
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

        // Balatro model: her perk bagimsiz calisir. Eski GlassCanon coexistence hack'i kaldirildi —
        // iki perk ayri ayri ApplyMult yapiyor, inspector sirasina gore ardisik uygulanir.
        payload.ApplyMult(tfgMult);

        TriggerVisualPop();
    }
}
