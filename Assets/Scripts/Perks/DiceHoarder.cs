using UnityEngine;

/// <summary>
/// Dice Hoarder (Legendary)
/// Her campfire'da kalıcı +1 zar.
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
    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public void OnCampfireVisited()
    {
        visitedLevels++;
        description = GetDescription();
    }

    public int GetExtraDice()
    {
        if (visitedLevels > 0) TriggerVisualPop();
        return visitedLevels;
    }

    public override void OnLevelStart()
    {
        // Sıfırlanmaz — run boyunca birikir
        description = GetDescription();
    }

    private string GetDescription()
    {
        return $"Each campfire visited grants a permanent +1 die.\nCampfires: {visitedLevels} (+{visitedLevels} dice)";
    }
}
