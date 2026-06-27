using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/SurgeBoot", fileName = "SurgeBoot")]
public class SurgeBoot : BaseItem
{
    void OnEnable()
    {
        itemName = "Surge-Boot";
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "max", 2 },
        { "base", 1 }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.surgeBootStacks++;
        if (TurnManager.instance?.player != null)
            TurnManager.instance.player.UpdateHighlights();
        return true;
    }
}
