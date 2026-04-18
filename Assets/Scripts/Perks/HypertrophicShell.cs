using UnityEngine;

public class HypertrophicShellPerk : BasePerk
{
    private const int HP_CAP = 10;

    void OnEnable()
    {
        maxLevel = 5;
        rarity = PerkRarity.Common;
    }

    public override void OnAcquire()
    {
        // Sadece aktif slottaysa HP bonusu uygula
        if (RunManager.instance != null && RunManager.instance.activePerks.Contains(this))
            ApplyHPBonus(1);
    }

    public override void Upgrade()
    {
        base.Upgrade();
        // Sadece aktif slottaysa HP bonusu uygula
        if (RunManager.instance != null && RunManager.instance.activePerks.Contains(this))
            ApplyHPBonus(1);
    }

    public override void OnEquip()
    {
        // Re-apply full bonus when moved to active slots
        // (currentLevel includes upgrades already applied)
        // Don't re-add — just ensure max HP reflects shell level
        RecalculateMaxHP();
    }

    public override void OnUnequip()
    {
        // Remove shell bonus
        RecalculateMaxHP();
    }

    private void ApplyHPBonus(int amount)
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Glass Canon aktifse max HP'yi değiştirme
        if (rm.activePerks.Exists(p => p is GlassCanonPerk)) return;

        long newMax = System.Math.Min(rm.playerMaxHealth + amount, HP_CAP);
        if (newMax == rm.playerMaxHealth) return;

        rm.playerMaxHealth = newMax;
        // Yeni HP'yi de artır (can bonusu olarak)
        rm.playerCurrentHealth = System.Math.Min(rm.playerCurrentHealth + amount, newMax);

        SyncHealthToScene();
        TriggerVisualPop();
    }

    private void RecalculateMaxHP()
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Glass Canon aktifse shell hiç etki etmez
        if (rm.activePerks.Exists(p => p is GlassCanonPerk)) return;

        long baseHP = TurnManager.instance != null ? TurnManager.instance.startingMaxHP : 5;

        // Check if shell is currently active (equipped)
        bool isActive = rm.activePerks.Contains(this);
        long shellBonus = isActive ? currentLevel : 0;

        long newMax = System.Math.Min(baseHP + shellBonus, HP_CAP);
        long oldMax = rm.playerMaxHealth;

        if (newMax != oldMax)
        {
            rm.playerMaxHealth = newMax;
            rm.playerCurrentHealth = System.Math.Min(rm.playerCurrentHealth, newMax);
            SyncHealthToScene();
        }
    }

    private void SyncHealthToScene()
    {
        var rm = RunManager.instance;
        if (TurnManager.instance != null && TurnManager.instance.player != null)
        {
            var h = TurnManager.instance.player.health;
            h.maxHP = rm.playerMaxHealth;
            h.currentHP = rm.playerCurrentHealth;
            h.updateHealth();
        }
    }
}
