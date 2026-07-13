using UnityEngine;
using System.Collections.Generic;

public class PassiveEnzymePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "skip",   GameKeywords.Action("Skipping") },
        { "reward", GameKeywords.Gold(4 * currentLevel) },
        { "boss",   GameKeywords.Status("bosses") }
    };

    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void OnSkip()
    {
        if (RunManager.instance != null && RunManager.instance.currentNodeType == MapNodeType.Boss) return;

        if (TurnManager.instance != null && TurnManager.instance.enemies != null)
        {
            int alive = 0;
            foreach (var e in TurnManager.instance.enemies)
                if (e != null && e.health.currentHP > 0) alive++;
            if (alive <= 1) return;
        }

        RunManager.instance.GrantGold(this, 4 * currentLevel);
    }
}
