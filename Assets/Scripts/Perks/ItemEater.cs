using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemEaterPerk : BasePerk
{
    [Header("Item Eater")]
    [Tooltip("Drag-drop UI yokken: kullanilan her item beslenme olarak sayilir.")]
    public bool feedOnItemUsed = false;

    [System.NonSerialized] public int feedCount = 0;

    public override void OnAcquire()
    {
        GameEvents.OnItemUsed += HandleItemUsed;
    }

    void OnDestroy()
    {
        GameEvents.OnItemUsed -= HandleItemUsed;
    }

    private void HandleItemUsed(BaseItem item, int slotIndex)
    {
        if (!feedOnItemUsed) return;
        if (item == null) return;
        feedCount++;
        RebuildDescription();
        TriggerVisualPop();
    }

    public void FeedItem(BaseItem item)
    {
        if (item == null) return;
        feedCount++;
        RebuildDescription();
        TriggerVisualPop();
    }

    private float BonusPerFeed()
    {
        return 0.5f + 0.15f * (currentLevel - 1);
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base", GameKeywords.Mult(1) },
        { "bonus", GameKeywords.Mult(BonusPerFeed()) },
        { "current", GameKeywords.Mult(1f + BonusPerFeed() * feedCount) },
        { "fed", GameKeywords.Counter(feedCount.ToString()) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnAttack) yield break;
        if (ctx.currentPerk != this) yield break;

        float mult = 1f + BonusPerFeed() * feedCount;
        if (mult <= 1f) yield break;
        ctx.payload.ApplyMult(mult);
        ctx.AnimatePop(this);
    }
}
