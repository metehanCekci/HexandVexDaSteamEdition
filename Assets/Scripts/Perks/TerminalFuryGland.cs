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
        description = "Always deal bonus damage. The lower your HP, the stronger the multiplier.";
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

        // TFG = missingHP + 2 (5/5=2x, 4/5=3x, 1/5=6x)
        float tfgMult = maxHP - currentHP + 2;

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
