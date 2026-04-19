using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarrionFeederPerk : BasePerk
{
    private int killStreak = 0;
    private bool pendingReset = false;

    private int MaxStacks => currentLevel; // lv1: 1, lv2: 2, lv3: 3

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Each consecutive kill doubles your total damage (max {max}). Resets when an attack fails to kill.\nKill Streak: {streak} ({current} dmg)";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues()
    {
        float currentMultiplier = killStreak > 0 ? Mathf.Pow(2, killStreak) : 1;
        float maxMultiplier = Mathf.Pow(2, MaxStacks);
        return new Dictionary<string, object>
        {
            { "max",     $"x{maxMultiplier}" },
            { "streak",  $"{killStreak}/{MaxStacks}" },
            { "current", $"x{currentMultiplier}" }
        };
    }

    public override void OnAcquire()
    {
        RebuildDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (pendingReset)
        {
            killStreak = 0;
        }

        pendingReset = true;
        RebuildDescription();
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        if (killStreak <= 0) yield break;

        for (int i = 0; i < killStreak; i++)
        {
            payload.multiplier *= 2f;
            TriggerVisualPop();
            if (PerkListUI.instance != null)
                PerkListUI.instance.TriggerShakeForPerk(this);
            if (diceUI != null && !diceUI.skipDiceVisuals)
            {
                diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
                yield return StartCoroutine(diceUI.SkippableWait(0.3f));
            }
        }
        RebuildDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (killStreak < MaxStacks)
            killStreak++;
        pendingReset = false;
        RebuildDescription();
        TriggerVisualPop();
    }
}
