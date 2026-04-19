using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/FragMine", fileName = "FragMine")]
public class FragMine : BaseItem
{
    void OnEnable()
    {
        itemName = "Frag-Mine";
        if (string.IsNullOrEmpty(description))
            description = "Place a bomb on any hex. Rolls dice and deals {dmg} to all enemies within {radius} hex radius.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "dmg", "damage" },
        { "radius", 1 }
    };

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;
        TurnManager.instance.StartBombPlacement();
        return true;
    }
}
