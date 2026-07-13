using UnityEngine;
using System.Collections.Generic;

public class PressurePointPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "hpLabel", GameKeywords.HealthText("HP") },
        { "full",    GameKeywords.HealthText("full HP") },
        { "mult",    GameKeywords.Mult(2f) }
    };

    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;

        if (hpPercent >= 1f) return 2f;
        if (hpPercent >= 0.50f) return 1.75f;
        return 1.5f;
    }
}
