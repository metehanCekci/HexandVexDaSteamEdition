using UnityEngine;
using System.Collections.Generic;

public class ShopRerollStackPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "reroll", GameKeywords.Action("shop reroll") },
        { "bonus",  GameKeywords.Plus(1) },
        { "stack",  GameKeywords.Counter((RunManager.instance != null ? RunManager.instance.shopRerollStack : 0).ToString()) }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void OnShopReroll()
    {
        RebuildDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        // Reroll stack bonusu artÄ±k doÄŸrudan zar atÄ±lÄ±rken ekleniyor (TurnManager)
    }
}
