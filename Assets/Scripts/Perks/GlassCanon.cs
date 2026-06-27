using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GlassCanonPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "hpLabel", GameKeywords.HealthText("HP") },
        { "hp", GameKeywords.Hp(3) },
        { "mult", GameKeywords.Mult(2) }
    };

    public override void OnAcquire()
    {
        if (RunManager.instance.playerMaxHealth <= 3) return;

        RunManager.instance.playerMaxHealth = 3;
        if (RunManager.instance.playerCurrentHealth > 3)
            RunManager.instance.playerCurrentHealth = 3;

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = 3;
            if (h.currentHP > 3) h.currentHP = 3;
            h.updateHealth();
        }
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        var rm = RunManager.instance;
        if (rm.playerMaxHealth <= 3) return;

        long newMax = 3;
        rm.playerMaxHealth = newMax;
        rm.playerCurrentHealth = System.Math.Clamp(rm.playerCurrentHealth, 1, newMax);

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = newMax;
            h.currentHP = rm.playerCurrentHealth;
            h.updateHealth();
        }
    }

    public override void OnUnequip()
    {
        var rm = RunManager.instance;
        long defaultMaxHP = TurnManager.instance != null ? TurnManager.instance.startingMaxHP : 5;

        rm.playerMaxHealth = defaultMaxHP;

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = defaultMaxHP;
            h.updateHealth();
        }
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        ctx.payload.ApplyMult(2f);
        ctx.AnimatePop(this);
    }
}
