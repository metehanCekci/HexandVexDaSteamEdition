using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fatal Sight Protocol (Legendary)
/// Attacks are always critical hits.
/// criticalChance is converted to extra criticalDamageMultiplier (1:1).
/// </summary>
public class FatalSightProtocolPerk : BasePerk
{
    void OnEnable()
    {
        rarity      = PerkRarity.Legendary;
        maxLevel    = 1;
        priority    = 1; // Önce çalışsın, sonraki perkler critHit=true üzerine eklensin
        if (string.IsNullOrEmpty(description))
            description = "All attacks are Critical Hits. Each {chance} Crit Chance converts to {dmg}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "chance", "+1%" },
        { "dmg",    "+1% Crit Damage" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        var rm = RunManager.instance;
        if (rm == null) return;

        // Her saldırıda: birikmiş critChance varsa dönüştür
        if (rm.criticalChance > 0f)
        {
            // critChance → critDamage: 1:1 dönüşüm (0.10 critChance = +0.10 critDamage)
            rm.criticalDamageMultiplier += rm.criticalChance;
            rm.criticalChance = 0f;
        }

        payload.isCriticalHit = true;
    }
}
