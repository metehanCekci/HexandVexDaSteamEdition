using UnityEngine;
using System.Collections.Generic;

public class EchoStrikePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "chance", GameKeywords.Crit($"{currentLevel * 15}%") },
        { "echo", GameKeywords.Retrigger("echo") },
        { "attack", GameKeywords.Action("attack") }
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
