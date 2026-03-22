using UnityEngine;

public class GlassCanonPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
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
        int oldMax = rm.playerMaxHealth;
        int newMax = 3;

        // Orantili HP: 4/5 (80%) -> 2/3 (66% -> floor)
        float ratio = oldMax > 0 ? (float)rm.playerCurrentHealth / oldMax : 1f;
        rm.playerMaxHealth = newMax;
        rm.playerCurrentHealth = Mathf.Clamp(Mathf.RoundToInt(ratio * newMax), 1, newMax);

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
        int defaultMaxHP = TurnManager.instance != null ? TurnManager.instance.startingMaxHP : 5;

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
