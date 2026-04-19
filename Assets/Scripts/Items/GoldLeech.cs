using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/GoldLeech", fileName = "GoldLeech")]
public class GoldLeech : BaseItem
{
    void OnEnable()
    {
        itemName = "Gold-Leech";
        if (string.IsNullOrEmpty(description))
            description = "Next enemy drops {mult} {gold}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X2" },
        { "gold", "gold" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleGoldNextKill = true;
        return true;
    }
}
