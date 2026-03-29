public class LuckyCloverPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        maxLevel = 3;
    }

    /// <summary>Lucky Clover artik perk secim havuzunda cikmaz.</summary>
    public override bool CanBeOffered() { return false; }

    public override void OnAcquire()
    {
        RunManager.instance.luckyCloverLevel = currentLevel;
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();
        RunManager.instance.luckyCloverLevel = currentLevel;
        TriggerVisualPop();
    }
}
