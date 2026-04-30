using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// =========================================================================
// COMBAT PIPELINE — Balatro-style event orchestrator
// =========================================================================
// Tek event-bus. Perkler tip olarak tanimaz, switch-case yok.
// Akis:
//   1) BeforeCombat — tum aktif perklere yayinlanir.
//   2) OnAttack — her perk icin sirayla yayinlanir (inspector sirasi + processLast).
//      Perk OnEvent icinde ctx.RequestPerkReplay(target) cagirirsa o perk daha sonra
//      yeniden run edilir.
//   3) Dice retrigger pass — her zar icin OnDiceScored yayinlanir.
//      Perkler ctx.RequestExtraDicePass(i, n) ile o zarin yeniden event'lenmesini ister.
//   4) AfterCombat — tum perkler bilgilenir.
// Animasyon: perk OnEvent icinde yield return ctx.WaitFor(saniye) ve ctx.AnimatePop(this).
// =========================================================================
public class CombatPipeline
{
    public const int MAX_DICE_RETRIGGERS_PER_INDEX = 16;

    private readonly DiceUIController diceUI;
    private CombatContext ctx;

    private readonly Dictionary<int, int> extraDicePassRequests = new Dictionary<int, int>();
    private readonly List<BasePerk> perkReplayQueue = new List<BasePerk>();

    public CombatPipeline(DiceUIController diceUI)
    {
        this.diceUI = diceUI;
        this.ctx = new CombatContext(this);
    }

    // ====================================================================
    // Public API — perkler ctx uzerinden cagirir.
    // ====================================================================

    public void RequestExtraDicePass(int diceIndex, int count)
    {
        if (count <= 0) return;
        if (extraDicePassRequests.TryGetValue(diceIndex, out int existing))
            extraDicePassRequests[diceIndex] = Mathf.Min(existing + count, MAX_DICE_RETRIGGERS_PER_INDEX);
        else
            extraDicePassRequests[diceIndex] = Mathf.Min(count, MAX_DICE_RETRIGGERS_PER_INDEX);
    }

    public void RequestPerkReplay(BasePerk target)
    {
        if (target == null) return;
        if (!target.CanBeRetriggeredByPerks) return;
        if (ctx != null && target == ctx.currentPerk) return; // kendini retriggerlama
        perkReplayQueue.Add(target);
    }

    // ====================================================================
    // Pipeline phases
    // ====================================================================

    public IEnumerator RunBaseCombat(CombatPayload payload, List<int> rolls)
    {
        if (RunManager.instance == null) yield break;

        ctx.payload = payload;
        ctx.rolls = rolls;
        ctx.diceUI = diceUI;

        yield return Broadcast(CombatEventType.BeforeCombat);

        var perks = GetActivePerks();
        perks.Sort((a, b) => a.processLast.CompareTo(b.processLast));

        for (int i = 0; i < perks.Count; i++)
        {
            var p = perks[i];
            if (p == null || p.IsIncompatible()) continue;
            yield return RunPerkAttack(p, payload, rolls);
        }

        yield return RunDicePass(payload, rolls);
    }

    public IEnumerator RunLetsGoAgain(CombatPayload payload, List<int> rolls)
    {
        ctx.payload = payload;
        ctx.rolls = rolls;
        ctx.diceUI = diceUI;

        var perks = GetActivePerks();
        perks.RemoveAll(p => p is LetsGoAgainPerk);
        perks.Sort((a, b) => a.processLast.CompareTo(b.processLast));

        yield return Broadcast(CombatEventType.OnLetsGoAgain);

        for (int i = 0; i < perks.Count; i++)
        {
            var p = perks[i];
            if (p == null || p.IsIncompatible()) continue;
            yield return RunPerkAttack(p, payload, rolls);
        }

        yield return RunDicePass(payload, rolls);
    }

    public IEnumerator RunAfterCombat(CombatPayload payload, List<int> rolls)
    {
        ctx.payload = payload;
        ctx.rolls = rolls;
        ctx.diceUI = diceUI;
        yield return Broadcast(CombatEventType.AfterCombat);
    }

    // ====================================================================
    // Internal: tek perkin OnAttack pass'i + biriken replay istekleri.
    // ====================================================================
    private IEnumerator RunPerkAttack(BasePerk perk, CombatPayload payload, List<int> rolls, bool isReplay = false)
    {
        long beforeTotal = payload.GetFinalDamage();

        ctx.ResetDiceFields();
        ctx.eventType = CombatEventType.OnAttack;
        ctx.currentPerk = perk;
        ctx.isReplay = isReplay;
        yield return RunPerkEvent(perk);
        ctx.isReplay = false;

        yield return SyncDiceVisualsForPerk(perk, payload, rolls, beforeTotal);

        yield return DrainPerkReplayQueue(payload, rolls);

        // Lets Go Again — Balatro retrigger style: each perk fires, then immediately fires again.
        // Skip if this attack was already a replay (don't recurse), or if the perk being run IS LetsGoAgain
        // (it doesn't retrigger itself), or if the perk opts out via CanBeRetriggeredByPerks.
        if (!isReplay && perk != null && !(perk is LetsGoAgainPerk) && perk.CanBeRetriggeredByPerks
            && RunManager.instance != null
            && RunManager.instance.activePerks.Exists(p => p is LetsGoAgainPerk))
        {
            var lga = RunManager.instance.activePerks.Find(p => p is LetsGoAgainPerk);
            if (lga != null && !lga.IsIncompatible())
            {
                if (diceUI != null && !diceUI.skipDiceVisuals)
                {
                    lga.TriggerVisualPop();
                    if (PerkListUI.instance != null) PerkListUI.instance.TriggerShakeForPerk(lga);
                    yield return diceUI.SkippableWait(0.25f);
                }
                yield return RunPerkAttack(perk, payload, rolls, isReplay: true);
            }
        }
    }

    private IEnumerator DrainPerkReplayQueue(CombatPayload payload, List<int> rolls)
    {
        if (perkReplayQueue.Count == 0) yield break;

        var batch = new List<BasePerk>(perkReplayQueue);
        perkReplayQueue.Clear();

        foreach (var target in batch)
        {
            if (target == null || target.IsIncompatible()) continue;
            yield return RunPerkAttack(target, payload, rolls, isReplay: true);
        }
    }

    // ====================================================================
    // Internal: dice retrigger pass.
    // ====================================================================
    private IEnumerator RunDicePass(CombatPayload payload, List<int> rolls)
    {
        if (rolls == null || rolls.Count == 0) yield break;

        int originalCount = rolls.Count;
        for (int i = 0; i < originalCount; i++)
        {
            yield return EmitDiceEvent(payload, i, rolls[i], retrigCountSoFar: 0, animate: false);

            int requested = 0;
            extraDicePassRequests.TryGetValue(i, out requested);
            extraDicePassRequests.Remove(i);

            for (int r = 0; r < requested; r++)
            {
                payload.ApplyAdd(rolls[i]);
                if (i >= 0 && i < payload.diceRetriggerCounts.Count)
                    payload.diceRetriggerCounts[i]++;

                yield return EmitDiceEvent(payload, i, rolls[i], retrigCountSoFar: r + 1, animate: true);

                if (extraDicePassRequests.TryGetValue(i, out int more) && more > 0)
                {
                    int extra = Mathf.Min(more, MAX_DICE_RETRIGGERS_PER_INDEX - (r + 1));
                    extraDicePassRequests.Remove(i);
                    requested += extra;
                }

                yield return DrainPerkReplayQueue(payload, rolls);
            }
        }
    }

    private IEnumerator EmitDiceEvent(CombatPayload payload, int diceIndex, int diceValue, int retrigCountSoFar, bool animate)
    {
        ctx.eventType = CombatEventType.OnDiceScored;
        ctx.diceIndex = diceIndex;
        ctx.diceValue = diceValue;
        ctx.retrigCountSoFar = retrigCountSoFar;
        ctx.currentPerk = null;

        var perks = GetActivePerks();
        foreach (var p in perks)
        {
            if (p == null || p.IsIncompatible()) continue;
            yield return RunPerkEvent(p);
        }

        ctx.ResetDiceFields();

        if (animate && diceUI != null && !diceUI.skipDiceVisuals)
        {
            if (diceIndex < diceUI.SpawnedDiceUI.Count)
                diceUI.AnimateSpecificDie(diceIndex, diceValue);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return diceUI.SkippableWait(0.4f);
        }
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private List<BasePerk> GetActivePerks()
    {
        var src = RunManager.instance.activePerks;
        var list = new List<BasePerk>(src.Count);
        for (int i = 0; i < src.Count; i++)
            if (src[i] != null) list.Add(src[i]);
        return list;
    }

    private IEnumerator Broadcast(CombatEventType ev)
    {
        ctx.eventType = ev;
        ctx.ResetDiceFields();
        ctx.currentPerk = null;
        var perks = GetActivePerks();
        foreach (var p in perks)
        {
            if (p == null || p.IsIncompatible()) continue;
            yield return RunPerkEvent(p);
        }
    }

    /// <summary>
    /// Bir perkin OnEvent IEnumerator'ini sirayla yurutur. Inner IEnumerator'in
    /// her adimini explicit olarak iter eder ki Unity'nin nested coroutine
    /// scheduling davranisina bagli kalmasin.
    /// </summary>
    private IEnumerator RunPerkEvent(BasePerk perk)
    {
        IEnumerator inner = perk.OnEvent(ctx);
        if (inner == null) yield break;
        while (true)
        {
            object current;
            try
            {
                if (!inner.MoveNext()) yield break;
                current = inner.Current;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator SyncDiceVisualsForPerk(BasePerk perk, CombatPayload payload, List<int> rolls, long beforeTotal)
    {
        if (diceUI == null) yield break;

        bool anyDieChanged = false;
        var changedIndices = new List<int>();
        for (int i = 0; i < rolls.Count && i < payload.diceRolls.Count; i++)
            if (rolls[i] != payload.diceRolls[i])
                changedIndices.Add(i);

        var addedDiceIndices = new List<int>();
        while (rolls.Count < payload.diceRolls.Count)
        {
            addedDiceIndices.Add(rolls.Count);
            rolls.Add(payload.diceRolls[rolls.Count]);
            while (payload.diceRetriggerCounts.Count < payload.diceRolls.Count)
                payload.diceRetriggerCounts.Add(0);
        }

        if (changedIndices.Count > 0 && !diceUI.skipDiceVisuals)
        {
            if (perk.isRerollPerk)
            {
                foreach (int idx in changedIndices)
                {
                    if (idx < diceUI.SpawnedDiceUI.Count)
                    {
                        var dieAnim = diceUI.SpawnedDiceUI[idx].GetComponent<Animator>();
                        var dieText = diceUI.SpawnedDiceUI[idx].GetComponentInChildren<TMP_Text>();
                        if (dieAnim != null) dieAnim.enabled = true;
                        if (dieText != null) dieText.text = "!";
                    }
                }
                yield return diceUI.SkippableWait(1.0f);
                foreach (int idx in changedIndices)
                {
                    rolls[idx] = payload.diceRolls[idx];
                    if (idx < diceUI.SpawnedDiceUI.Count)
                    {
                        var dieAnim = diceUI.SpawnedDiceUI[idx].GetComponent<Animator>();
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
            yield return diceUI.SkippableWait(0.6f);
        }
        else if (changedIndices.Count > 0)
        {
            foreach (int idx in changedIndices)
                rolls[idx] = payload.diceRolls[idx];
            anyDieChanged = true;
        }

        if (addedDiceIndices.Count > 0)
        {
            if (!diceUI.skipDiceVisuals)
            {
                foreach (int idx in addedDiceIndices)
                    diceUI.SpawnExtraDie(payload.diceRolls[idx]);
                yield return diceUI.SkippableWait(0.6f);
            }
            anyDieChanged = true;
        }

        long afterTotal = payload.GetFinalDamage();
        if ((beforeTotal != afterTotal || anyDieChanged) && !diceUI.skipDiceVisuals)
        {
            perk.TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(perk);
            diceUI.UpdateTotalDamageDisplay(afterTotal);
            yield return diceUI.SkippableWait(0.6f);
        }
    }
}
