using System.Collections.Generic;

public class VolatileCellsPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "kill",    GameKeywords.Action("Killed") },
        { "explode", GameKeywords.Action("explode") },
        { "dmg",     GameKeywords.Plus(currentLevel * 25, "%") },
        { "maxHp",   GameKeywords.HealthText("max HP") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.triggerExplosion = true;
        payload.explosionDamagePercent = currentLevel * 0.25f;
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
