using UnityEngine;
using System.Collections.Generic;

public class DeadweightPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Stunned enemies take {mult} damage.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", $"x{1 + currentLevel}" }
    };

    public float GetStunnedMultiplier()
    {
        return 1f + currentLevel;
    }

    public bool IsStunned(EnemyMovement enemy)
    {
        return enemy != null && enemy.skipTurns > 0;
    }
}
