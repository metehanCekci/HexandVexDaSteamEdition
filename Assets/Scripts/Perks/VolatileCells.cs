using System.Collections;
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

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        ctx.payload.triggerExplosion = true;
        ctx.payload.explosionDamagePercent = currentLevel * 0.25f;
        ctx.AnimatePop(this);
    }
}
