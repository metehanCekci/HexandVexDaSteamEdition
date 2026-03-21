using UnityEngine;

public class HypertrophicShellPerk : BasePerk
{
    private const int HP_CAP = 10;

    void OnEnable()
    {
        maxLevel = 5;
    }

    public override void OnAcquire()
    {
        ApplyHPBonus(1);
    }

    public override void Upgrade()
    {
        base.Upgrade();
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

        int newMax = Mathf.Min(rm.playerMaxHealth + amount, HP_CAP);
        if (newMax == rm.playerMaxHealth) return;

        rm.playerMaxHealth = newMax;
        // Yeni HP'yi de artır (can bonusu olarak)
        rm.playerCurrentHealth = Mathf.Min(rm.playerCurrentHealth + amount, newMax);

        SyncHealthToScene();
        TriggerVisualPop();
    }

    private void RecalculateMaxHP()
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Glass Canon aktifse shell hiç etki etmez
        if (rm.activePerks.Exists(p => p is GlassCanonPerk)) return;

        int baseHP = TurnManager.instance != null ? TurnManager.instance.startingMaxHP : 5;

        // Check if shell is currently active (equipped)
        bool isActive = rm.activePerks.Contains(this);
        int shellBonus = isActive ? currentLevel : 0;

        int newMax = Mathf.Min(baseHP + shellBonus, HP_CAP);
        int oldMax = rm.playerMaxHealth;

        if (newMax != oldMax)
        {
            // Proportional HP adjustment
            float ratio = oldMax > 0 ? (float)rm.playerCurrentHealth / oldMax : 1f;
            rm.playerMaxHealth = newMax;
            rm.playerCurrentHealth = Mathf.Clamp(Mathf.RoundToInt(ratio * newMax), 1, newMax);
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
