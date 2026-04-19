using System.Collections.Generic;

public class LuckyCloverPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        maxLevel = 3;
        if (string.IsNullOrEmpty(description))
            description = "Reroll {rerolls} low dice per combat.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "rerolls", $"+{currentLevel}" }
    };

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
