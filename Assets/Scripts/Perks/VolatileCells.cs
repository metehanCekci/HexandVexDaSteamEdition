using System.Collections.Generic;

public class VolatileCellsPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Killed enemies explode for {dmg} of their max HP to adjacent enemies.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "dmg", $"{currentLevel * 25}%" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.triggerExplosion = true;
        payload.explosionDamagePercent = currentLevel * 0.25f;
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
