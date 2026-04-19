using UnityEngine;

public class EchoStrikePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Epic;
    }

    /// <summary>
    /// Returns the echo chance based on current level.
    /// Lv1: 15%, Lv2: 30%, Lv3: 45%
    /// </summary>
    public float GetEchoChance()
    {
        return currentLevel * 0.15f;
    }

    /// <summary>
    /// Roll for echo. Called by TurnManager after dealing damage to each enemy.
    /// Returns true if the attack should echo.
    /// </summary>
    public bool ShouldEcho()
    {
        bool result = Random.value < GetEchoChance();
        if (result) TriggerVisualPop();
        return result;
    }
}
