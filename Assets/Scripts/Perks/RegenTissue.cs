using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RegenTissuePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "heal",  GameKeywords.Heal(currentLevel) },
        { "clear", GameKeywords.Action("clearing") }
    };

    public override void OnAcquire()
    {
        ApplyHealthBoost();
    }

    public override void Upgrade()
    {
        base.Upgrade();
        ApplyHealthBoost();
    }

    public override void OnLevelClear()
    {
        ApplyHealthBoost();
    }

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        if (ctx.eventType != CombatEventType.OnLetsGoAgain) yield break;
        ApplyHealthBoost();
    }

    private void ApplyHealthBoost()
    {
        if (RunManager.instance == null) return;
        RunManager.instance.GrantHeal(this, currentLevel);
    }
}
