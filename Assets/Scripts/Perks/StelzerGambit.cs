using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stelzer's Gambit â€” Common. SADECE tek-yuzlu (1, 3, 5) zarlar bir kez daha islenir.
/// 30 roll (saldiri) sonra implant yok olur.
/// </summary>
public class StelzerGambitPerk : BasePerk
{
    private const int MaxRolls = 30;
    public int rollsRemaining = MaxRolls;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "odd",   GameKeywords.Status("odd dice") },
        { "extra", GameKeywords.RetriggerN(1) },
        { "rolls", GameKeywords.Counter(rollsRemaining.ToString()) }
    };

    public override int GetDiceRetriggerCount(int diceIndex, int diceValue, CombatPayload payload)
    {
        if (rollsRemaining <= 0) return 0;
        // Sadece tek-yuzlu zarlar (1, 3, 5) retrigger olur — mekanik nerf
        return (diceValue % 2 == 1) ? 1 : 0;
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
