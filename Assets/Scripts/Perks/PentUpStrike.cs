using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pent-Up Strike (Epic)
/// Normal saldÄ±rÄ±da 0 hasar verir (knockback kalÄ±r), zar toplamÄ±nÄ± biriktirir.
/// Skip ile saldÄ±rdÄ±ÄŸÄ±nda biriken tÃ¼m hasarÄ± tek seferde verir.
/// </summary>
public class PentUpStrikePerk : BasePerk
{
    [HideInInspector] public long storedDamage = 0;
    [HideInInspector] public long storedStacks = 0;
    [HideInInspector] public bool isReleasing = false;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attack",  GameKeywords.Action("Attacks") },
        { "zero",    GameKeywords.Plus(0, "damage") },
        { "push",    GameKeywords.Action("knockback") },
        { "skip",    GameKeywords.Action("Skip") },
        { "percent", GameKeywords.Plus(50 + currentLevel * 50, "%") },
        { "stored",  GameKeywords.Counter($"{storedDamage} damage") },
        { "stacks",  GameKeywords.Counter(storedStacks.ToString()) }
    };

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (isReleasing)
        {
            // Biriken hasari serbest birak: runningDamage'a dogrudan ekle (% ile olcekli).
            double bonus = storedDamage * (0.5 + currentLevel * 0.5);
            payload.ApplyAdd(bonus);
            storedDamage = 0;
            storedStacks = 0;
            isReleasing = false;
            RebuildDescription();
            TriggerVisualPop();
        }
        else
        {
            // Normal saldiri: zarlari biriktir, bu saldiri 0 hasar versin.
            long diceSum = payload.diceRolls.Sum();
            storedDamage += diceSum;
            storedStacks++;

            for (int i = 0; i < payload.diceRolls.Count; i++)
                payload.diceRolls[i] = 0;
            // Balatro model: processLast oldugumuz icin tum onceki perkler calisti,
            // simdi runningDamage'i sifirliyoruz (ve sonraki retrigger pass zar 0 olacagi icin bir sey eklemez).
            payload.runningDamage = 0.0;

            RebuildDescription();
            TriggerVisualPop();
        }
    }

    public override void OnSkip()
    {
        if (storedDamage > 0)
            isReleasing = true;
    }

    public override void OnLevelStart()
    {
        storedDamage = 0;
        storedStacks = 0;
        isReleasing = false;
        RebuildDescription();
    }
}
