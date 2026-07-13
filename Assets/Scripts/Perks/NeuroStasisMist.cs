using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Neuro-Stasis Mist (Common)
/// Any stun you apply to an enemy lasts +1 extra turn per level.
/// </summary>
public class NeuroStasisMistPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "stun",  GameKeywords.Status("Stuns") },
        { "turns", GameKeywords.Counter("+1 turn") },
        { "per",   GameKeywords.Counter("+1") },
        { "level", GameKeywords.Action("level") }
    };

    /// <summary>Returns how many extra stun turns this perk adds. +1 per level.</summary>
    public int GetStunBonus()
    {
        return currentLevel;
    }
}
