using UnityEngine;

public class AcidBloodPerk : BasePerk
{
    void OnEnable() { maxLevel = 3; rarity = PerkRarity.Common; UpdateDescription(); }

    public override void OnAcquire() { UpdateDescription(); }

    public override void Upgrade()
    {
        base.Upgrade();
        UpdateDescription();
    }

    private void UpdateDescription()
    {
        description = $"Pushing an enemy into spikes heals you for {currentLevel} HP.";
    }
}
