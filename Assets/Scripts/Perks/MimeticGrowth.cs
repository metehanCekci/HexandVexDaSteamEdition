using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Mimetic Growth — Epic. Sagindaki implantin ozelliklerini taklit eder:
/// her saldirida sagindaki implantin ModifyCombat'ini bir kez daha tetikler.
/// En sagda ise veya sagindaki implant da bir retrigger implantiysa INCOMPATIBLE.
/// </summary>
public class MimeticGrowthPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Epic;
        processLast = true;
        isPerkRetrigger = true;
        if (string.IsNullOrEmpty(description))
            description = "Mimics the implant to its right, triggering its effect {again}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "again", "X1 more time" }
    };

    public override bool IsIncompatible()
    {
        return GetRightNeighbor() == null;
    }

    public override string GetIncompatibleReason() => "Nothing to the right";

    private BasePerk GetRightNeighbor()
    {
        if (RunManager.instance == null) return null;
        var list = RunManager.instance.activePerks;
        int myIndex = list.IndexOf(this);
        if (myIndex < 0 || myIndex >= list.Count - 1) return null;

        var neighbor = list[myIndex + 1];
        if (neighbor == null) return null;
        // Diger perk-retrigger implantlarini kopyalayamaz (infinite loop koruma).
        if (neighbor.isPerkRetrigger) return null;
        return neighbor;
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        var neighbor = GetRightNeighbor();
        if (neighbor == null) yield break;

        TriggerVisualPop();
        if (PerkListUI.instance != null)
            PerkListUI.instance.TriggerShakeForPerk(this);

        // Komsu perki tek basina yeniden calistir (dice-retrigger pass'i de dahil).
        var processor = TurnManager.instance != null ? TurnManager.instance.PerkProcessor : null;
        if (processor == null) yield break;

        List<int> rolls = new List<int>(payload.diceRolls);
        yield return processor.RetriggerPerk(neighbor, payload, rolls);
    }
}
