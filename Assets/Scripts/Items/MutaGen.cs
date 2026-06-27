using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/MutaGen", fileName = "MutaGen")]
public class MutaGen : BaseItem
{
    void OnEnable()
    {
        itemName = "Muta-Gen";
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "heal", "2 HP" }
    };

    public override bool Use()
    {
        if (TurnManager.instance?.player?.health == null) return false;
        if (TurnManager.instance.player.health.currentHP >= TurnManager.instance.player.health.maxHP) return false;

        TurnManager.instance.player.health.Heal(2);
        return true;
    }
}
