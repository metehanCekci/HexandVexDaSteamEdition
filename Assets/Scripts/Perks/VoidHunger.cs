using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Void Hunger (Common)
/// Çöken her tile (scaffold + seismic) başına kalıcı +0.25x damage multiplier.
/// Run boyunca birikir, sıfırlanmaz.
/// </summary>
public class VoidHungerPerk : BasePerk
{
    private int collapsedCount = 0;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        maxLevel = 1;
        if (string.IsNullOrEmpty(description))
            description = "Each collapsed tile grants permanent {per} damage multiplier.\nCollapsed: {count} ({total})";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues()
    {
        float totalBonus = collapsedCount * 0.25f;
        return new Dictionary<string, object>
        {
            { "per",   "+0.25X" },
            { "count", collapsedCount },
            { "total", $"+{totalBonus:F1}X" }
        };
    }

    public override void OnAcquire()
    {
        Subscribe();
        RebuildDescription();
    }

    public override void OnEquip()
    {
        Subscribe();
        RebuildDescription();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        RebuildDescription();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        RebuildDescription();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        TrapTileEvents.OnTileDestroyed += OnTileDestroyed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        TrapTileEvents.OnTileDestroyed -= OnTileDestroyed;
        subscribed = false;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnTileDestroyed(Vector3Int cell)
    {
        collapsedCount++;
        RebuildDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (collapsedCount <= 0) return;

        payload.multiplier += collapsedCount * 0.25f;
        TriggerVisualPop();
    }
}
