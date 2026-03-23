using UnityEngine;

public class PressurePointPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
    }

    // Hasar carpani dusmanin mevcut HP yuzdesine gore belirlenir.
    // TurnManager hasar uygulamadan once bu metodu cagirir.
    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;

        if (hpPercent >= 1f) return 3f;    // %100 HP -> 3x
        if (hpPercent >= 0.75f) return 2f; // %75+ HP -> 2x
        return 1.5f;                        // %50 ve alti -> 1.5x
    }
}
