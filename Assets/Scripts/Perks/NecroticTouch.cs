using UnityEngine;
using System.Collections.Generic;

public class NecroticTouchPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Enemies below 50% HP take {mult} damage.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "x2" }
    };

    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;
        if (hpPercent <= 0.50f) return 2f;
        return 1f;
    }
}
