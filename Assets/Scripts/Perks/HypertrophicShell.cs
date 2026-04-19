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
        // HP is handled by OnEquip — no action needed here.
        // OnEquip runs right after OnAcquire when perk goes to active slot.
        // If perk goes to stash, HP bonus is deferred until OnEquip.
    }

    public override void Upgrade()
    {
        base.Upgrade();
        // If active, recalculate HP with the new level
        if (RunManager.instance != null && RunManager.instance.activePerks.Contains(this))
            ApplyShellHP(true);
    }

    public override void OnEquip()
    {
        ApplyShellHP(true);
    }

    public override void OnUnequip()
    {
        ApplyShellHP(false);
    }

    /// <summary>
    /// Directly sets max HP based on whether the shell is equipped or not.
    /// Does NOT check activePerks list — the equipped parameter is authoritative.
    /// This fixes the SwapPerk bug where callbacks fire before the list is updated.
    /// </summary>
    private void ApplyShellHP(bool equipped)
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Glass Canon aktifse max HP'yi degistirme
        if (rm.activePerks.Exists(p => p is GlassCanonPerk)) return;

        int baseHP = TurnManager.instance != null ? TurnManager.instance.startingMaxHP : 5;
        int shellBonus = equipped ? currentLevel : 0;
        int newMax = Mathf.Min(baseHP + shellBonus, HP_CAP);
        int oldMax = rm.playerMaxHealth;

        if (newMax == oldMax) return;

        rm.playerMaxHealth = newMax;
        if (newMax > oldMax)
            rm.playerCurrentHealth = Mathf.Min(rm.playerCurrentHealth + (newMax - oldMax), newMax);
        else
            rm.playerCurrentHealth = Mathf.Min(rm.playerCurrentHealth, newMax);

        SyncHealthToScene();
        TriggerVisualPop();
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
