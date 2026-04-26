using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dice Hoarder (Legendary)
/// Her campfire'da kalÄ±cÄ± +1 zar.
/// </summary>
public class DiceHoarderPerk : BasePerk
{
    [HideInInspector] public int visitedLevels = 0;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "perFire", GameKeywords.Plus(1, "die") },
        { "count",   GameKeywords.Counter(visitedLevels.ToString()) },
        { "total",   GameKeywords.Plus(visitedLevels, "dice") }
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
        // SÄ±fÄ±rlanmaz â€” run boyunca birikir
        RebuildDescription();
    }
}
