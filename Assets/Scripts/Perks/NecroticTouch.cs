using UnityEngine;

public class NecroticTouchPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
    }

    public float GetMultiplier(EnemyMovement enemy)
    {
        if (enemy == null || enemy.health == null) return 1f;

        float hpPercent = (float)enemy.health.currentHP / enemy.health.maxHP;
        if (hpPercent <= 0.50f) return 2f;
        return 1f;
    }
}
