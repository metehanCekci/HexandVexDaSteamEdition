using UnityEngine;
using System.Linq;

public class RiggedDicePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "All dice are rerolled to match the highest rolled value.";
        RebuildDescription();
    }

    public override void OnAcquire()
    {
        isRerollPerk = true; // Zar değerlerini değiştirdiği için
        priority = 10;       // Diğer çarpanlardan önce çalışsın
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload.diceRolls.Count < 2) return;

        int minVal = payload.diceRolls.Min();
        int maxVal = payload.diceRolls.Max();

        if (minVal == maxVal) return; // Zaten aynıysa dokunma

        bool changed = false;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            if (payload.diceRolls[i] != maxVal)
            {
                payload.diceRolls[i] = maxVal;
                changed = true;
            }
        }

        if (changed) TriggerVisualPop();
    }
}
