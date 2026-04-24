using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pent-Up Strike (Epic)
/// Normal saldırıda 0 hasar verir (knockback kalır), zar toplamını biriktirir.
/// Skip ile saldırdığında biriken tüm hasarı tek seferde verir.
/// </summary>
public class PentUpStrikePerk : BasePerk
{
    [HideInInspector] public long storedDamage = 0;
    [HideInInspector] public long storedStacks = 0;
    [HideInInspector] public bool isReleasing = false;

    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        // Balatro modelinde bu perk ardisik pipeline'in EN SONUNDA calismali,
        // cunku runningDamage'i sifirlayip kendi bonusunu uyguluyor.
        processLast = true;
        if (string.IsNullOrEmpty(description))
            description = "Attacks deal {zero} but still knockback. Dice values are stored. Skip to unleash all stored damage at {percent}.\nStored: {stored} ({stacks} stacks)";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "zero",    "0 damage" },
        { "percent", $"+{50 + currentLevel * 50}%" },
        { "stored",  $"{storedDamage} damage" },
        { "stacks",  storedStacks }
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
