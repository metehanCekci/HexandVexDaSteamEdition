using UnityEngine;
using System.Collections.Generic;

public class PressurePointPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Full HP: {mult1}. Above half: {mult2}. Below half: {mult3}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult1", "X2" },
        { "mult2", "X1.75" },
        { "mult3", "X1.5" }
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
