using UnityEngine;

/// <summary>
/// Dice Hoarder (Legendary)
/// Uğradığın her perk seçim ve shop leveli başına kalıcı +1 zar.
/// GetExtraDice() ile TurnManager'a entegre edilir.
/// </summary>
public class DiceHoarderPerk : BasePerk
{
    [HideInInspector] public int visitedLevels = 0;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
        maxLevel = 1;
    }

    /// <summary>
    /// Her perk/shop leveline girildiğinde TurnManager veya MapManager tarafından çağrılır.
    /// </summary>
    public void OnNonCombatLevelVisited()
    {
        visitedLevels++;
    }

    public int GetExtraDice()
    {
        if (visitedLevels > 0) TriggerVisualPop();
        return visitedLevels;
    }

    public override void OnLevelStart()
    {
        // Sıfırlanmaz — run boyunca birikir
    }
}
