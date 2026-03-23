using UnityEngine;

public class NecroticTouchPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    /// <summary>
    /// Returns the damage multiplier based on enemy HP%.
    /// Enemies below 25% HP take 2x damage from all sources.
    /// Called by TurnManager before applying damage.
    /// </summary>
    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;
        if (hpPercent <= 0.25f) return 2f;
        return 1f;
    }
}
