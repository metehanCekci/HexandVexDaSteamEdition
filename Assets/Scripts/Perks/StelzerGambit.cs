using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stelzer's Gambit — Common. Her zar bir kez daha islenir (retrigger).
/// 30 roll (saldiri) sonra implant yok olur.
/// </summary>
public class StelzerGambitPerk : BasePerk
{
    private const int MaxRolls = 30;
    public int rollsRemaining = MaxRolls;

    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Each die triggers {extra}. Decays in {rolls}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "extra", "1 more time" },
        { "rolls", rollsRemaining + " rolls" }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        return rollsRemaining > 0 ? 1 : 0;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (rollsRemaining <= 0) return;
        rollsRemaining--;
        RebuildDescription();

        if (rollsRemaining <= 0)
        {
            // Decay: bir sonraki frame'de kendini kaldir (ModifyCombat icinde listeyi modifiye etme).
            TriggerVisualPop();
            if (this != null && gameObject != null && RunManager.instance != null)
                StartCoroutine(DecayRoutine());
        }
    }

    private System.Collections.IEnumerator DecayRoutine()
    {
        yield return null;
        if (RunManager.instance == null) yield break;
        RunManager.instance.activePerks.Remove(this);
        RunManager.instance.inventoryPerks.Remove(this);
        OnUnequip();
        Destroy(gameObject);
    }
}
