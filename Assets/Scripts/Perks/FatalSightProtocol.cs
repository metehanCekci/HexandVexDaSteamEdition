using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Fatal Sight Protocol (Legendary)
/// Attacks are always critical hits.
/// criticalChance is converted to extra criticalDamageMultiplier (1:1).
/// </summary>
public class FatalSightProtocolPerk : BasePerk
{
    // Replay edilirse criticalChance->criticalDamageMultiplier transferini iki kez yapardi.
    public override bool CanBeRetriggeredByPerks => false;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attacks", GameKeywords.Action("attacks") },
        { "crit",    GameKeywords.Crit("Critical Hits") },
        { "chance",  GameKeywords.CritPlus(1, "crit chance") },
        { "dmg",     GameKeywords.CritPlus(1, "crit damage") }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        var rm = RunManager.instance;
        if (rm == null) yield break;

        if (rm.criticalChance > 0f)
        {
            rm.criticalDamageMultiplier += rm.criticalChance;
            rm.criticalChance = 0f;
        }

        ctx.payload.isCriticalHit = true;
    }
}
