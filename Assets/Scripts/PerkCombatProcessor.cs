using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// =========================================================================
// BALATRO-STYLE ARDISIK PIPELINE
// =========================================================================
// 1) BASE PASS: perkler inspector sirasinda (+ processLast sonradan) sirayla calisir.
//    Her perk ModifyCombat'ta payload.ApplyAdd / payload.ApplyMult ile runningDamage'i
//    ANINDA degistirir. Sira degisirse sonuc degisir.
// 2) RETRIGGER POST-PASS: her zar icin dice-retrigger perkleri (Hanging Nerve, Stelzer,
//    Sensory Overload, Mimetic/Leftmost/Parasitic uzerinden kopyalananlar) kac ekstra
//    retrigger verdigini soyler. Her retrigger icin:
//      a) payload.ApplyAdd(diceValue)       -> zar degeri ardisik eklenir
//      b) payload.DispatchDiceRetrigger(i,v) -> per-die perkler (Photovoltaic vb.) hook'ta carpanini uygular
// =========================================================================
public class PerkCombatProcessor : MonoBehaviour
{
    public const int MAX_DICE_PROCESS_PER_COMBAT = 16;
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
        perksToProcess.Sort((a, b) => a.processLast.CompareTo(b.processLast));

        // 1) BASE PASS — perkler sirayla calisir, her biri runningDamage'i anlik degistirir.
        yield return StartCoroutine(ProcessPerksFromList(payload, rolls, perksToProcess));

        // 2) RETRIGGER POST-PASS — dice retrigger perkleri devreye girer.
        yield return StartCoroutine(ApplyDiceRetriggerPostPass(payload, rolls));
    }

    /// <summary>
    /// Her orijinal zar icin, dice-retrigger perklerinin verdigi retrigger sayisi kadar
    /// "bu zar bir kez daha oynaniyor" olayini yayinlar. Olay icinde:
    ///   - runningDamage += value
    ///   - OnDiceRetriggerEvent tum perklere yayinlanir (Photovoltaic gibi aboneler kendi efektini uygular)
    /// Perk-retrigger perkleri (Mimetic/Leftmost/Parasitic) kopyaladiklari dice-retrigger perkinin
    /// retrigger katkisini bir kez daha uygular (kendi pop'u ile).
    /// </summary>
    private IEnumerator ApplyDiceRetriggerPostPass(CombatPayload payload, List<int> rolls)
    {
        if (rolls == null || rolls.Count == 0) yield break;
        if (RunManager.instance == null) yield break;

        int originalCount = rolls.Count;

        for (int i = 0; i < originalCount; i++)
        {
            int value = rolls[i];

            // 1) Normal dice-retrigger perkleri
            foreach (var p in RunManager.instance.activePerks)
            {
                if (p == null || p.IsIncompatible()) continue;
                if (p.isPerkRetrigger) continue;
                int cnt = Mathf.Max(0, p.GetDiceRetriggerCount(i, value, payload));
                if (cnt <= 0) continue;
                cnt = Mathf.Min(cnt, MAX_DICE_PROCESS_PER_COMBAT - 1);
                yield return StartCoroutine(EmitDiceRetriggers(payload, i, value, cnt, p));
            }

            // 2) Perk-retrigger perkleri — kopyaladiklari perkin dice-retrigger katkisini bir kez daha uygular
            foreach (var pr in RunManager.instance.activePerks)
            {
                if (pr == null || pr.IsIncompatible()) continue;
                if (!pr.isPerkRetrigger) continue;

                foreach (var target in GetPerkRetriggerTargets(pr))
                {
                    if (target == null || target.IsIncompatible()) continue;
                    int cnt = Mathf.Max(0, target.GetDiceRetriggerCount(i, value, payload));
                    if (cnt <= 0) continue;
                    cnt = Mathf.Min(cnt, MAX_DICE_PROCESS_PER_COMBAT - 1);
                    yield return StartCoroutine(EmitDiceRetriggers(payload, i, value, cnt, pr));
                }
            }
        }
    }

    private IEnumerator EmitDiceRetriggers(CombatPayload payload, int diceIndex, int value, int count, BasePerk source)
    {
        for (int r = 0; r < count; r++)
        {
            // a) Zar degeri ardisik eklenir — runningDamage += value
            payload.ApplyAdd(value);

            // Retrigger sayac'i (per-die perkler diceRetriggerCounts'u alternatif olarak da okuyabilir)
            if (diceIndex >= 0 && diceIndex < payload.diceRetriggerCounts.Count)
                payload.diceRetriggerCounts[diceIndex]++;

            // b) Per-die bagimli perkler event uzerinden carpanini uygular (Photovoltaic, ilerideki Triboulet-clone vs.)
            //    Abone olan her perk BasePerk.OnDiceRetriggerEvent'i override eder.
            DispatchRetriggerToAllPerks(payload, diceIndex, value);

            if (diceUI != null && !diceUI.skipDiceVisuals)
            {
                if (diceIndex < diceUI.SpawnedDiceUI.Count)
                    diceUI.AnimateSpecificDie(diceIndex, value);
                diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());

                source.TriggerVisualPop();
                if (PerkListUI.instance != null)
                    PerkListUI.instance.TriggerShakeForPerk(source);

                yield return StartCoroutine(diceUI.SkippableWait(0.4f));
            }
        }
    }

    /// <summary>
    /// Retrigger event'ini tum aktif perklere yayinlar. CombatPayload.DispatchDiceRetrigger
    /// event abonelerine + dogrudan aktif perklerin OnDiceRetriggerEvent override'lerine haber verir.
    /// </summary>
    private void DispatchRetriggerToAllPerks(CombatPayload payload, int diceIndex, int diceValue)
    {
        payload.DispatchDiceRetrigger(diceIndex, diceValue);
        foreach (var p in RunManager.instance.activePerks)
        {
            if (p == null || p.IsIncompatible()) continue;
            p.OnDiceRetriggerEvent(diceIndex, diceValue, payload);
        }
    }

    private IEnumerable<BasePerk> GetPerkRetriggerTargets(BasePerk perk)
    {
        if (RunManager.instance == null) yield break;
        var list = RunManager.instance.activePerks;
        int idx = list.IndexOf(perk);
        if (idx < 0) yield break;

        switch (perk)
        {
            case MimeticGrowthPerk _:
                if (idx < list.Count - 1 && list[idx + 1] != null && !list[idx + 1].isPerkRetrigger)
                    yield return list[idx + 1];
                break;

            case LeftmostResonancePerk _:
                for (int k = 0; k < list.Count; k++)
                {
                    var t = list[k];
                    if (t == null || t == perk || t.isPerkRetrigger) continue;
                    yield return t;
                    yield return t; // Leftmost 2 ekstra tetikleme verir
                    yield break;
                }
                break;

            case ParasiticChorusPerk _:
                for (int k = 0; k < list.Count; k++)
                {
                    var c = list[k];
                    if (c == null || c.rarity != PerkRarity.Common) continue;
                    if (k + 1 >= list.Count) continue;
                    var right = list[k + 1];
                    if (right == null || right.isPerkRetrigger) continue;
                    yield return right;
                }
                break;
        }
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
            yield return StartCoroutine(diceUI.SkippableWait(0.8f));
        }

        List<BasePerk> secondPass = RunManager.instance.activePerks
            .FindAll(p => p != null && !(p is LetsGoAgainPerk));

        // Pass reset — runningDamage'i zar tabanindan yeniden baslat.
        payload.RebaseRunningDamage();
        // Retrigger sayaclarini sifirla
        for (int i = 0; i < payload.diceRetriggerCounts.Count; i++)
            payload.diceRetriggerCounts[i] = 0;
        while (payload.diceRetriggerCounts.Count < rolls.Count)
            payload.diceRetriggerCounts.Add(0);

        yield return StartCoroutine(ProcessPerksFromList(payload, rolls, secondPass));
        yield return StartCoroutine(ApplyDiceRetriggerPostPass(payload, rolls));

        foreach (var perk in secondPass)
            perk.OnLetsGoAgain();

        var sfPerk = RunManager.instance.activePerks.Find(p => p is SymbioticFuryPerk);
        if (sfPerk != null && !diceUI.skipDiceVisuals)
        {
            sfPerk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(sfPerk);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return StartCoroutine(diceUI.SkippableWait(0.6f));
        }
    }

    private IEnumerator ProcessPerksFromList(CombatPayload payload, List<int> rolls, List<BasePerk> perks)
    {
        Dictionary<int, int> diceProcessCount = new Dictionary<int, int>();

        foreach (BasePerk perk in perks)
        {
            if (perk == null) continue;
            if (perk.IsIncompatible()) continue;

            yield return StartCoroutine(RunSinglePerk(perk, payload, rolls, diceProcessCount));
        }
    }

    /// <summary>
    /// Tek bir perk'in ModifyCombat + AnimatedCombatEffect pass'ini calistirir.
    /// </summary>
    private IEnumerator RunSinglePerk(BasePerk perk, CombatPayload payload, List<int> rolls, Dictionary<int, int> diceProcessCount)
    {
        long beforeTotal = payload.GetFinalDamage();
        perk.ModifyCombat(payload);

        yield return StartCoroutine(SyncDiceVisuals(perk, payload, rolls, beforeTotal));
        yield return StartCoroutine(perk.AnimatedCombatEffect(payload, diceUI));
    }

    public IEnumerator RetriggerPerk(BasePerk perk, CombatPayload payload, List<int> rolls)
    {
        if (perk == null || perk.IsIncompatible()) yield break;
        if (perk.isPerkRetrigger) yield break;
        if (perkRetriggerDepth >= MAX_PERK_RETRIGGER_DEPTH) yield break;

        perkRetriggerDepth++;
        try
        {
            Dictionary<int, int> diceCount = new Dictionary<int, int>();
            yield return StartCoroutine(RunSinglePerk(perk, payload, rolls, diceCount));
        }
        finally
        {
            perkRetriggerDepth--;
        }
    }

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
            // Yeni zarlari retrigger sayac listesine de ekle
            while (payload.diceRetriggerCounts.Count < payload.diceRolls.Count)
                payload.diceRetriggerCounts.Add(0);
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
                    yield return StartCoroutine(diceUI.SkippableWait(1.0f));
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
                yield return StartCoroutine(diceUI.SkippableWait(0.6f));
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
                yield return StartCoroutine(diceUI.SkippableWait(0.6f));
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
                yield return StartCoroutine(diceUI.SkippableWait(0.6f));
            }
        }
    }
}
