using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/PhaseShift", fileName = "PhaseShift")]
public class PhaseShift : BaseItem
{
    void OnEnable()
    {
        itemName = "Phase-Shift";
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
