using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/PhaseShift", fileName = "PhaseShift")]
public class PhaseShift : BaseItem
{
    void OnEnable()
    {
        itemName = "Phase-Shift";
        if (string.IsNullOrEmpty(description))
            description = "Select an enemy and {swap} with it.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "swap", "swap positions" }
    };

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;
        TurnManager.instance.StartPhaseShiftTargeting();
        return true;
    }
}
