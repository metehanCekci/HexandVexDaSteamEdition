using UnityEngine;
using System.Collections;

public class CarrionFeederPerk : BasePerk
{
    private int killStreak = 0;
    private bool pendingReset = false;

    private int MaxStacks => currentLevel; // lv1: 1, lv2: 2, lv3: 3

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Rare;
    }

    public override void OnAcquire()
    {
        description = GetDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (pendingReset)
        {
            killStreak = 0;
        }

        pendingReset = true;
        description = GetDescription();
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
        description = GetDescription();
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (killStreak < MaxStacks)
            killStreak++;
        pendingReset = false;
        description = GetDescription();
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();
        description = GetDescription();
    }

    private string GetDescription()
    {
        float currentMultiplier = killStreak > 0 ? Mathf.Pow(2, killStreak) : 1;
        float maxMultiplier = Mathf.Pow(2, MaxStacks);
        return $"Each consecutive kill doubles your total damage (max x{maxMultiplier}). Resets when an attack fails to kill.\nKill Streak: {killStreak}/{MaxStacks} (x{currentMultiplier} dmg)";
    }
}
