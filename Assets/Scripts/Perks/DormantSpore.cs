using UnityEngine; // Unity motoru için
using System;      // Temel fonksiyonlar için
using System.Collections.Generic; // Listeler için
public class DormantSporePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
    }

    public int storedExtraDices = 0;

    public int ConsumeStoredDice()
    {
        int dice = storedExtraDices;
        storedExtraDices = 0;
        description = GetDescription();
        return dice;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public override void OnSkip()
    {
        storedExtraDices++;
        description = GetDescription();
        TriggerVisualPop();
    }

    public override void OnLevelStart()
    {
        description = GetDescription();
    }

    private string GetDescription()
    {
        return $"Each skip stores +1 extra die for your next attack.\nStored dice: {storedExtraDices}";
    }
}
