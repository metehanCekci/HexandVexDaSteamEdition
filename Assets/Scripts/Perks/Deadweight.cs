using UnityEngine;

/// <summary>
/// Deadweight (Rare)
/// Stunlanmış (skipTurns > 0) düşmanlar ekstra hasar alır.
/// TurnManager hasar dağıtırken kontrol eder.
/// Lv1: 2x, Lv2: 3x, Lv3: 4x
/// </summary>
public class DeadweightPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
    }

    /// <summary>
    /// Stunlanmış düşmana uygulanan hasar çarpanı.
    /// </summary>
    public float GetStunnedMultiplier()
    {
        return 1f + currentLevel; // Lv1: 2x, Lv2: 3x, Lv3: 4x
    }

    /// <summary>
    /// Düşman stunlanmış mı kontrol et.
    /// </summary>
    public bool IsStunned(EnemyMovement enemy)
    {
        return enemy != null && enemy.skipTurns > 0;
    }
}
