using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dice Hoarder (Legendary)
/// Her campfire'da kalıcı +1 zar.
/// </summary>
public class DiceHoarderPerk : BasePerk
{
    [HideInInspector] public int visitedLevels = 0;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Each campfire visited grants a permanent {perFire}.\nCampfires: {count} ({total})";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "perFire", "+1 die" },
        { "count",   $"+{visitedLevels}" },
        { "total",   $"+{visitedLevels} dies" }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public void OnCampfireVisited()
    {
        visitedLevels++;
        RebuildDescription();
    }

    public int GetExtraDice()
    {
        if (visitedLevels > 0) TriggerVisualPop();
        return visitedLevels;
    }

    public override void OnLevelStart()
    {
        // Sıfırlanmaz — run boyunca birikir
        RebuildDescription();
    }
}
