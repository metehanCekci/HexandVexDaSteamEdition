using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Leftmost Resonance — Legendary. En soldaki implantin efektini iki kez daha tetikler
/// (toplamda o implant 3 kere islenir). Eger en soldaki bu perkin kendisiyse veya
/// baska bir perk-retrigger implantiysa INCOMPATIBLE.
/// </summary>
public class LeftmostResonancePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
        processLast = true;
        isPerkRetrigger = true;
        if (string.IsNullOrEmpty(description))
            description = "The leftmost implant triggers {again}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "again", "X2 additional times" }
    };

    public override bool IsIncompatible() => GetLeftmostTarget() == null;

    public override string GetIncompatibleReason() => "No valid leftmost implant";

    private BasePerk GetLeftmostTarget()
    {
        if (RunManager.instance == null) return null;
        var list = RunManager.instance.activePerks;
        for (int i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate == null) continue;
            if (candidate == this) return null;           // Kendisi en soldaysa gecersiz
            if (candidate.isPerkRetrigger) return null;   // Baska bir perk-retrigger: loop riski
            return candidate;
        }
        return null;
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        var target = GetLeftmostTarget();
        if (target == null) yield break;

        var processor = TurnManager.instance != null ? TurnManager.instance.PerkProcessor : null;
        if (processor == null) yield break;

        List<int> rolls = new List<int>(payload.diceRolls);

        // Target pure dice-retrigger ise pre-pass zaten 2 ekstra uygulandi — perk pass'inde sessiz kal.
        for (int i = 0; i < 2; i++)
        {
            long before = payload.GetFinalDamage();
            yield return processor.RetriggerPerk(target, payload, rolls);
            long after = payload.GetFinalDamage();

            if (after != before)
            {
                TriggerVisualPop();
                if (PerkListUI.instance != null)
                    PerkListUI.instance.TriggerShakeForPerk(this);
            }
        }
    }
}
