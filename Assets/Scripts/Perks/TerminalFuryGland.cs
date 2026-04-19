using UnityEngine;

/// <summary>
/// Terminal Fury Gland (Legendary)
/// Always active. Multiplier = maxHP / currentHP
/// 5/5 HP = 1x, 1/5 HP = 5x
/// </summary>
public class TerminalFuryGlandPerk : BasePerk
{
    void OnEnable()
    {
        perkName    = "Terminal Fury Gland";
        description = "Always deal double damage, get +1x mult per missing health.";
        rarity      = PerkRarity.Legendary;
        maxLevel    = 1;
        priority    = 15;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        var rm = RunManager.instance;
        var tm = TurnManager.instance;
        if (rm == null || tm == null || tm.player == null) return;

        int maxHP = rm.playerMaxHealth;
        int currentHP = tm.player.health.currentHP;
        if (currentHP < 1) currentHP = 1;

        // Always 2x base, +1x per missing HP (5/5=2x, 4/5=3x, 1/5=6x)
        float tfgMult = 2f + (maxHP - currentHP);

        // Glass Cannon varsa: additif birlestir (TFG + GC), GC'nin *= 2'sini geri al
        bool hasGC = rm.activePerks.Exists(p => p is GlassCanonPerk);
        if (hasGC)
        {
            // GC daha once payload.multiplier *= 2 uyguladi, geri al ve toplami koy
            payload.multiplier = payload.multiplier / 2f * (tfgMult + 2f);
        }
        else
        {
            payload.multiplier *= tfgMult;
        }

        TriggerVisualPop();
    }
}
