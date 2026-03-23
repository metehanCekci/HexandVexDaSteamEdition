using UnityEngine;

public class PressurePointPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    // Hasar carpani dusmanin mevcut HP yuzdesine gore belirlenir.
    // TurnManager hasar uygulamadan once bu metodu cagirir.
    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;

        if (hpPercent >= 1f) return 2f;     // %100 HP -> 2x
        if (hpPercent >= 0.50f) return 1.75f; // %99-%50 HP -> 1.75x
        return 1.5f;                        // %50 alti -> 1.5x
    }
}
