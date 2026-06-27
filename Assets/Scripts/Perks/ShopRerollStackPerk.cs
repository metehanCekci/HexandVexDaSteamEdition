using System.Collections;
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

    // Reroll stack bonusu zar atilirken TurnManager tarafindan eklenir; OnEvent'te is yok.
    public override IEnumerator OnEvent(CombatContext ctx)
    {
        yield break;
    }
}
