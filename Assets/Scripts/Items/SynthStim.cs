using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/SynthStim", fileName = "SynthStim")]
public class SynthStim : BaseItem
{
    void OnEnable()
    {
        itemName = "Synth-Stim";
        if (string.IsNullOrEmpty(description))
            description = "Roll {bonus} extra die in the next combat.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "bonus", "+1" }
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.bonusDiceNextCombat += 1;
        return true;
    }
}
