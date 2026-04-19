using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/SurgeBoot", fileName = "SurgeBoot")]
public class SurgeBoot : BaseItem
{
    void OnEnable()
    {
        itemName = "Surge-Boot";
        if (string.IsNullOrEmpty(description))
            description = "Next turn you can move up to {max} hexes instead of {base}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "max", 2 },
        { "base", 1 }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.surgeBootActive = true;
        if (TurnManager.instance?.player != null)
            TurnManager.instance.player.UpdateHighlights();
        return true;
    }
}
