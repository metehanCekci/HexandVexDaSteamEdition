using UnityEngine;
using System.Collections.Generic;

public class DeadweightPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "stunned", GameKeywords.Status("Stunned") },
        { "mult",    GameKeywords.Mult(1 + currentLevel) }
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
