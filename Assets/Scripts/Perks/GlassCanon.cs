using UnityEngine;

public class GlassCanonPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Max HP drops to {hp}, but you deal {mult} damage.";
        RebuildDescription();
    }

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "hp", "3 HP" },
        { "mult", "X2" }
    };

    public override void OnAcquire()
    {
        // Sadece max HP 3'ten büyükse düşür — zaten 3 veya altındaysa dokunma
        if (RunManager.instance.playerMaxHealth <= 3) return;

        RunManager.instance.playerMaxHealth = 3;
        if (RunManager.instance.playerCurrentHealth > 3)
            RunManager.instance.playerCurrentHealth = 3;

        // Sahnedeki player health component'ini de güncelle
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
        // Current HP'yi 3'e düşür ama asla mevcut değerin altına indirme
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

        // Sadece maxHP'yi geri yükselt, currentHP'ye dokunma (stash abuse fix)
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
        // Glass Cannon: Her koşulda 2x hasar
        payload.multiplier *= 2f;

        Debug.Log($"[GlassCanon] 2x damage applied, Final multiplier: {payload.multiplier}x");

        TriggerVisualPop();
    }
}
