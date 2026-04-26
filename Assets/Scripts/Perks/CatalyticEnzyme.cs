using UnityEngine;
using System.Collections.Generic;

public class CatalyticEnzymePerk : BasePerk
{
    private int skipStacks = 0;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "skip",   GameKeywords.Action("skip") },
        { "per",    GameKeywords.Mult(1.3f) },
        { "attack", GameKeywords.Action("attack") },
        { "count",  GameKeywords.Counter(skipStacks.ToString()) },
        { "bonus",  GameKeywords.Mult(1f + (skipStacks * 0.3f)) }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnSkip()
    {
        skipStacks++;
        RebuildDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (skipStacks <= 0) return;
        // Each stack = +30% multiplier
        payload.ApplyMult(1f + (skipStacks * 0.3f));
        skipStacks = 0; // Consume on attack
        RebuildDescription();
    }

    public override void OnLevelStart()
    {
        skipStacks = 0;
        RebuildDescription();
    }
}
