using System.Collections.Generic;

public class LuckyCloverPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "reroll", GameKeywords.Action("Reroll") },
        { "rerolls", GameKeywords.Plus(currentLevel) }
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
