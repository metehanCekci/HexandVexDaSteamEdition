using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Neuro-Stasis Mist (Common)
/// Any stun you apply to an enemy lasts +1 extra turn per level.
/// </summary>
public class NeuroStasisMistPerk : BasePerk
{
    void OnEnable()
    {
        rarity      = PerkRarity.Common;
        maxLevel    = 3;
        if (string.IsNullOrEmpty(description))
            description = "Stuns you apply last {base}. {per} per level.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base", "+1 extra turn" },
        { "per",  "+1" }
    };

    /// <summary>Returns how many extra stun turns this perk adds. +1 per level.</summary>
    public int GetStunBonus()
    {
        return currentLevel;
    }
}
