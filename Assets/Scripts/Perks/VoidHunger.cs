using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Void Hunger (Common). Cöken her tile basina kalici +0.25x damage.
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

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;
        if (collapsedCount <= 0) yield break;

        ctx.payload.ApplyMult(1f + collapsedCount * 0.25f);
        ctx.AnimatePop(this);
    }
}
