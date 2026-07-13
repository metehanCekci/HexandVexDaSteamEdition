using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/FragMine", fileName = "FragMine")]
public class FragMine : BaseItem
{
    void OnEnable()
    {
        itemName = "Frag-Mine";
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
