using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Void Hunger (Common)
/// Ã‡Ã¶ken her tile (scaffold + seismic) baÅŸÄ±na kalÄ±cÄ± +0.25x damage multiplier.
/// Run boyunca birikir, sÄ±fÄ±rlanmaz.
/// </summary>
public class VoidHungerPerk : BasePerk
{
    private int collapsedCount = 0;
    private bool subscribed = false;

    public override Dictionary<string, object> GetDescValues()
    {
        float totalBonus = collapsedCount * 0.25f;
        return new Dictionary<string, object>
        {
            { "collapse", GameKeywords.Action("collapsed tile") },
            { "per",     GameKeywords.Mult(0.25f) },
            { "count",   GameKeywords.Counter(collapsedCount.ToString()) },
            { "total",   GameKeywords.Mult(totalBonus) }
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

        // Eski model: multiplier += X -> efektif (1+X). Balatro modelinde ApplyMult(1+X).
        payload.ApplyMult(1f + collapsedCount * 0.25f);
        TriggerVisualPop();
    }
}
