using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PerkCombatProcessor : MonoBehaviour
{
    // Dice retrigger sonsuz dongu korumasi: bir zar en fazla bu kadar kez islenebilir.
    public const int MAX_DICE_PROCESS_PER_COMBAT = 16;

    // Perk retrigger stack derinligi — ic ice retrigger'lari engellemek icin global sayac.
    // Retrigger perkleri diger retrigger perklerini cagrirken bu sayac artar, MAX asilirsa skip.
    public const int MAX_PERK_RETRIGGER_DEPTH = 3;
    private int perkRetriggerDepth = 0;

    private DiceUIController diceUI;

    public void Initialize(DiceUIController diceUIController)
    {
        diceUI = diceUIController;
    }

    public IEnumerator ProcessPerks(CombatPayload payload, List<int> rolls)
    {
        if (RunManager.instance == null || RunManager.instance.activePerks.Count == 0)
            yield break;

        List<BasePerk> perksToProcess = RunManager.instance.activePerks.FindAll(p => p != null);

        // processLast perkleri sona at (SymbioticArsenal vb.)
        perksToProcess.Sort((a, b) => a.processLast.CompareTo(b.processLast));

        yield return StartCoroutine(ProcessPerksFromList(payload, rolls, perksToProcess));
    }

    public IEnumerator ProcessLetsGoAgainPass(CombatPayload payload, List<int> rolls)
    {
        if (RunManager.instance == null) yield break;
        if (!RunManager.instance.activePerks.Exists(p => p is LetsGoAgainPerk)) yield break;

        var lgaPerk = RunManager.instance.activePerks.Find(p => p is LetsGoAgainPerk);
        if (lgaPerk != null && !diceUI.skipDiceVisuals)
        {
            lgaPerk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(lgaPerk);
            yield return StartCoroutine(diceUI.SkippableWait(0.4f));
        }

        List<BasePerk> secondPass = RunManager.instance.activePerks
            .FindAll(p => p != null && !(p is LetsGoAgainPerk));

        payload.flatBonus = 0;
        payload.multiplier = 1.0f;

        yield return StartCoroutine(ProcessPerksFromList(payload, rolls, secondPass));

        // Non-combat perk efektlerini de tetikle (RegenTissue vs.)
        foreach (var perk in secondPass)
            perk.OnLetsGoAgain();

        var sfPerk = RunManager.instance.activePerks.Find(p => p is SymbioticFuryPerk);
        if (sfPerk != null && !diceUI.skipDiceVisuals)
        {
            sfPerk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(sfPerk);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return StartCoroutine(diceUI.SkippableWait(0.3f));
        }
    }

    private IEnumerator ProcessPerksFromList(CombatPayload payload, List<int> rolls, List<BasePerk> perks)
    {
        // Her zarin kac kez islendigini takip et (sonsuz retrigger korumasi).
        Dictionary<int, int> diceProcessCount = new Dictionary<int, int>();

        foreach (BasePerk perk in perks)
        {
            if (perk == null) continue;
            // Incompatible perkler hicbir efekt uygulamaz.
            if (perk.IsIncompatible()) continue;

            yield return StartCoroutine(RunSinglePerk(perk, payload, rolls, diceProcessCount));
        }
    }

    /// <summary>
    /// Tek bir perk'in ModifyCombat + AnimatedCombatEffect pass'ini calistirir.
    /// ModifyCombat sonrasi her zar icin tum dice-retrigger perklerini sorar ve
    /// eslesenlerde ayni perk'in efektini bir kez daha calistirir.
    /// Mimetic/Leftmost/Parasitic gibi perk-retrigger perkleri bu metodu public wrapper
    /// (RetriggerPerk) uzerinden tetikler.
    /// </summary>
    private IEnumerator RunSinglePerk(BasePerk perk, CombatPayload payload, List<int> rolls, Dictionary<int, int> diceProcessCount)
    {
        long beforeTotal = payload.GetFinalDamage();
        perk.ModifyCombat(payload);

        yield return StartCoroutine(SyncDiceVisuals(perk, payload, rolls, beforeTotal));

        // Adim adim animasyonlu efektler (CarrionFeeder x2 x2 x2 vb.)
        yield return StartCoroutine(perk.AnimatedCombatEffect(payload, diceUI));

        // Dice retrigger pass'i: her zar icin tum aktif dice-retrigger perklerini sor.
        // Her retrigger, perkin ModifyCombat'ini bir kez daha cagirir (tum +/x zinciri tekrar uygulanir).
        if (RunManager.instance != null && rolls != null && rolls.Count > 0)
        {
            for (int i = 0; i < rolls.Count; i++)
            {
                int totalRetriggers = 0;
                foreach (var other in RunManager.instance.activePerks)
                {
                    if (other == null || other.IsIncompatible()) continue;
                    totalRetriggers += Mathf.Max(0, other.GetDiceRetriggerCount(i, rolls[i], payload));
                }

                for (int r = 0; r < totalRetriggers; r++)
                {
                    if (!diceProcessCount.TryGetValue(i, out int cnt)) cnt = 0;
                    if (cnt >= MAX_DICE_PROCESS_PER_COMBAT) break;
                    diceProcessCount[i] = cnt + 1;

                    long beforeRetrigger = payload.GetFinalDamage();
                    perk.ModifyCombat(payload);
                    yield return StartCoroutine(SyncDiceVisuals(perk, payload, rolls, beforeRetrigger));

                    // Dice retrigger visual cue: ilgili zari bir kez "titret"
                    if (diceUI != null && !diceUI.skipDiceVisuals && i < diceUI.SpawnedDiceUI.Count)
                    {
                        diceUI.AnimateSpecificDie(i, rolls[i]);
                        yield return StartCoroutine(diceUI.SkippableWait(0.15f));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Bir perk'i tek basina yeniden calistirir (Mimetic Growth, Leftmost Resonance, Parasitic Chorus).
    /// isPerkRetrigger iceren perkler cagirilmaz (infinite loop koruma).
    /// </summary>
    public IEnumerator RetriggerPerk(BasePerk perk, CombatPayload payload, List<int> rolls)
    {
        if (perk == null || perk.IsIncompatible()) yield break;
        // Perk-retrigger perkleri diger perk-retrigger perklerini retriggerlayamaz.
        if (perk.isPerkRetrigger) yield break;
        if (perkRetriggerDepth >= MAX_PERK_RETRIGGER_DEPTH) yield break;

        perkRetriggerDepth++;
        try
        {
            // RunSinglePerk icindeki dice-retrigger pass'i de tekrar calisacak, bu istenen davranis:
            // ornek "Hanging Chad 2x, Sensory Overload 5/6" -> retriggerlanan perk de bu zar retrigger'larini
            // yine hisseder (senin istedigin "hanging chad 2x 2x 2x" davranisi).
            Dictionary<int, int> diceCount = new Dictionary<int, int>();
            yield return StartCoroutine(RunSinglePerk(perk, payload, rolls, diceCount));
        }
        finally
        {
            perkRetriggerDepth--;
        }
    }

    /// <summary>
    /// ModifyCombat sonrasi zar degisikliklerini UI'a yansitir, yeni eklenen zarlari spawn eder,
    /// ve hasar degisimi oldugunda perk pop/shake animasyonunu oynatir.
    /// </summary>
    private IEnumerator SyncDiceVisuals(BasePerk perk, CombatPayload payload, List<int> rolls, long beforeTotal)
    {
        bool anyDieChanged = false;
        List<int> changedIndices = new List<int>();
        for (int i = 0; i < rolls.Count && i < payload.diceRolls.Count; i++)
            if (rolls[i] != payload.diceRolls[i])
                changedIndices.Add(i);

        List<int> addedDiceIndices = new List<int>();
        while (rolls.Count < payload.diceRolls.Count)
        {
            addedDiceIndices.Add(rolls.Count);
            rolls.Add(payload.diceRolls[rolls.Count]);
        }

        if (changedIndices.Count > 0)
        {
            if (!diceUI.skipDiceVisuals)
            {
                if (perk.isRerollPerk)
                {
                    foreach (int idx in changedIndices)
                    {
                        if (idx < diceUI.SpawnedDiceUI.Count)
                        {
                            Animator dieAnim = diceUI.SpawnedDiceUI[idx].GetComponent<Animator>();
                            TMP_Text dieText = diceUI.SpawnedDiceUI[idx].GetComponentInChildren<TMP_Text>();
                            if (dieAnim != null) dieAnim.enabled = true;
                            if (dieText != null) dieText.text = "!";
                        }
                    }
                    yield return StartCoroutine(diceUI.SkippableWait(0.5f));
                    foreach (int idx in changedIndices)
                    {
                        rolls[idx] = payload.diceRolls[idx];
                        if (idx < diceUI.SpawnedDiceUI.Count)
                        {
                            Animator dieAnim = diceUI.SpawnedDiceUI[idx].GetComponent<Animator>();
                            if (dieAnim != null) dieAnim.enabled = false;
                        }
                        diceUI.AnimateSpecificDie(idx, rolls[idx]);
                    }
                }
                else
                {
                    foreach (int idx in changedIndices)
                    {
                        rolls[idx] = payload.diceRolls[idx];
                        diceUI.AnimateSpecificDie(idx, rolls[idx]);
                    }
                }
                anyDieChanged = true;
                yield return StartCoroutine(diceUI.SkippableWait(0.3f));
            }
            else
            {
                foreach (int idx in changedIndices)
                    rolls[idx] = payload.diceRolls[idx];
                anyDieChanged = true;
            }
        }

        if (addedDiceIndices.Count > 0)
        {
            if (!diceUI.skipDiceVisuals)
            {
                foreach (int idx in addedDiceIndices)
                    diceUI.SpawnExtraDie(payload.diceRolls[idx]);
                yield return StartCoroutine(diceUI.SkippableWait(0.3f));
            }
            anyDieChanged = true;
        }

        long afterTotal = payload.GetFinalDamage();
        if (beforeTotal != afterTotal || anyDieChanged)
        {
            if (!diceUI.skipDiceVisuals)
            {
                perk.TriggerVisualPop();
                if (PerkListUI.instance != null)
                    PerkListUI.instance.TriggerShakeForPerk(perk);
                diceUI.UpdateTotalDamageDisplay(afterTotal);
                yield return StartCoroutine(diceUI.SkippableWait(0.3f));
            }
        }
    }
}
