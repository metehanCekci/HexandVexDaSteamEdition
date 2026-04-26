using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fatal Sight Protocol (Legendary)
/// Attacks are always critical hits.
/// criticalChance is converted to extra criticalDamageMultiplier (1:1).
/// </summary>
public class FatalSightProtocolPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attacks", GameKeywords.Action("attacks") },
        { "crit",    GameKeywords.Crit("Critical Hits") },
        { "chance",  GameKeywords.CritPlus(1, "crit chance") },
        { "dmg",     GameKeywords.CritPlus(1, "crit damage") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Her saldÄ±rÄ±da: birikmiÅŸ critChance varsa dÃ¶nÃ¼ÅŸtÃ¼r
        if (rm.criticalChance > 0f)
        {
            // critChance â†’ critDamage: 1:1 dÃ¶nÃ¼ÅŸÃ¼m (0.10 critChance = +0.10 critDamage)
            rm.criticalDamageMultiplier += rm.criticalChance;
            rm.criticalChance = 0f;
        }

        payload.isCriticalHit = true;
    }
}
