using UnityEngine;
using System.Collections.Generic;

public class PassiveEnzymePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Skipping a turn grants {reward} per level. Does not work on bosses or the last enemy.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "reward", "4 gold" }
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

        RunManager.instance.currentGold += (4 * currentLevel);
        GameEvents.GoldChanged(RunManager.instance.currentGold);
        TriggerVisualPop();
    }
}
