using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/GoldLeech", fileName = "GoldLeech")]
public class GoldLeech : BaseItem
{
    void OnEnable()
    {
        itemName = "Gold-Leech";
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X2" },
        { "gold", "gold" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleGoldNextKillStacks++;
        return true;
    }
}
