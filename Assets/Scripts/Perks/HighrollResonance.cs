using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Highroll Resonance â€” Rare. Triboulet çakmasi.
/// Her atilan 5 veya 6 icin runningDamage *= 1.5 (ARDISIK uygulanir).
/// Photovoltaic gibi per-die bagimli: ilgili zar retriggerlandiginda her seferinde
/// bir kez daha carpilir (Hanging Nerve / Stelzer ile sinerji).
///
/// Pipeline ornegi: zarlar [5, 6, 3], runningDamage taban = 14:
///   base       -> 14 *1.5 (zar0=5) = 21, *1.5 (zar1=6) = 31.5
///   zar0 retrig (Hanging Nerve) -> +5 (= 36.5), Highroll *1.5 (= 54.75)
/// </summary>
public class HighrollResonancePerk : BasePerk
{
    private const float MultPerHighroll = 1.5f;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "five",  GameKeywords.Status("5") },
        { "six",   GameKeywords.Status("6") },
        { "mult",  GameKeywords.Mult(MultPerHighroll, "damage") }
    };

    public override bool IsIncompatible() => false;

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload == null || payload.diceRolls == null) return;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            int v = payload.diceRolls[i];
            if (v == 5 || v == 6)
                payload.ApplyMult(MultPerHighroll);
        }
    }

    /// <summary>
    /// Retriggerlanan zar 5 veya 6 ise carpani bir kez daha ARDISIK uygula.
    /// </summary>
    public override void OnDiceRetriggerEvent(int diceIndex, int diceValue, CombatPayload payload)
    {
        if (diceValue != 5 && diceValue != 6) return;
        payload.ApplyMult(MultPerHighroll);

        TriggerVisualPop();
        if (PerkListUI.instance != null)
            PerkListUI.instance.TriggerShakeForPerk(this);
    }

    /// <summary>
    /// Her 5/6 zar icin "bom" — zarlari sirayla animate eder, her birinde perk pop'u atar
    /// ve total damage'i guncel goster.
    /// </summary>
    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        if (payload == null || payload.diceRolls == null) yield break;

        bool anyHighroll = false;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            int v = payload.diceRolls[i];
            if (v != 5 && v != 6) continue;
            anyHighroll = true;

            if (diceUI != null && !diceUI.skipDiceVisuals)
            {
                if (diceUI.SpawnedDiceUI != null && i < diceUI.SpawnedDiceUI.Count)
                    diceUI.AnimateSpecificDie(i, v);

                TriggerVisualPop();
                if (PerkListUI.instance != null)
                    PerkListUI.instance.TriggerShakeForPerk(this);

                diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
                yield return StartCoroutine(diceUI.SkippableWait(0.25f));
            }
        }

        if (!anyHighroll && diceUI != null && !diceUI.skipDiceVisuals)
        {
            // Highroll yoksa sessiz kal — bos pop atma
            yield break;
        }
    }
}
