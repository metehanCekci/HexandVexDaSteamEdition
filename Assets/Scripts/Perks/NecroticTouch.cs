using UnityEngine;
using System.Collections.Generic;

public class NecroticTouchPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "threshold", GameKeywords.HealthText("50% HP") },
        { "mult", GameKeywords.Mult(2) }
    };

    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;
        if (hpPercent <= 0.50f) return 2f;
        return 1f;
    }
}
