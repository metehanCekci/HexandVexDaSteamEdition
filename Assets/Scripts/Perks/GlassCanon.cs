using UnityEngine;

public class GlassCanonPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "hpLabel", GameKeywords.HealthText("HP") },
        { "hp", GameKeywords.Hp(3) },
        { "mult", GameKeywords.Mult(2) }
    };

    public override void OnAcquire()
    {
        // Sadece max HP 3'ten bÃ¼yÃ¼kse dÃ¼ÅŸÃ¼r â€” zaten 3 veya altÄ±ndaysa dokunma
        if (RunManager.instance.playerMaxHealth <= 3) return;

        RunManager.instance.playerMaxHealth = 3;
        if (RunManager.instance.playerCurrentHealth > 3)
            RunManager.instance.playerCurrentHealth = 3;

        // Sahnedeki player health component'ini de gÃ¼ncelle
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = 3;
            if (h.currentHP > 3) h.currentHP = 3;
            h.updateHealth();
        }
        Debug.Log($"[GlassCanon] Acquired! Max HP set to 3, Current HP: {(TurnManager.instance?.player?.health?.currentHP ?? RunManager.instance.playerCurrentHealth)}");
        TriggerVisualPop();
    }

    public override void OnEquip()
    {
        var rm = RunManager.instance;
        if (rm.playerMaxHealth <= 3) return;

        long oldMax = rm.playerMaxHealth;
        long newMax = 3;

        rm.playerMaxHealth = newMax;
        // Current HP'yi 3'e dÃ¼ÅŸÃ¼r ama asla mevcut deÄŸerin altÄ±na indirme
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

        // Sadece maxHP'yi geri yÃ¼kselt, currentHP'ye dokunma (stash abuse fix)
        rm.playerMaxHealth = defaultMaxHP;

        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = defaultMaxHP;
            h.updateHealth();
        }
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        // Glass Cannon: Her koÅŸulda 2x hasar
        payload.ApplyMult(2f);

        Debug.Log($"[GlassCanon] 2x damage applied, Final multiplier: {payload.multiplier}x");

        TriggerVisualPop();
    }
}
