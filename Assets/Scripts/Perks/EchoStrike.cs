using UnityEngine;
using System.Collections.Generic;

public class EchoStrikePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "{chance}% chance to echo attack.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "chance", $"{currentLevel * 15}" }
    };

    public float GetEchoChance()
    {
        return currentLevel * 0.15f;
    }

    public bool ShouldEcho()
    {
        bool result = Random.value < GetEchoChance();
        if (result) TriggerVisualPop();
        return result;
    }
}
