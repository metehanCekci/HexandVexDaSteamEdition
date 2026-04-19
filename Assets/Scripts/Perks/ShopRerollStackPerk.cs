using UnityEngine;
using System.Collections.Generic;

public class ShopRerollStackPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Each shop reroll grants a permanent {bonus} to all your dice. Stack: {stack}";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", "+1" },
        { "stack", RunManager.instance != null ? RunManager.instance.shopRerollStack : 0 }
    };

    public override void OnAcquire()
    {
        priority = 30;
        RebuildDescription();
    }

    public override void OnShopReroll()
    {
        RebuildDescription();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        // Reroll stack bonusu artık doğrudan zar atılırken ekleniyor (TurnManager)
    }
}
