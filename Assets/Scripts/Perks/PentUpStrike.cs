using UnityEngine;
using System.Linq;

/// <summary>
/// Pent-Up Strike (Legendary)
/// Normal saldırıda 0 hasar verir (knockback kalır), zar toplamını biriktirir.
/// Skip ile saldırdığında biriken tüm hasarı tek seferde verir.
/// Seviye başına birikim %25 ekstra bonus.
/// Lv1: %100, Lv2: %125, Lv3: %150
/// </summary>
public class PentUpStrikePerk : BasePerk
{
    [HideInInspector] public int storedDamage = 0;
    [HideInInspector] public bool isReleasing = false;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (isReleasing)
        {
            // Skip saldırısı: biriken hasarı flatBonus olarak ekle
            // Lv1: %100, Lv2: %150, Lv3: %200
            float bonus = storedDamage * (0.5f + currentLevel * 0.5f);
            payload.flatBonus += Mathf.FloorToInt(bonus);
            storedDamage = 0;
            isReleasing = false;
            TriggerVisualPop();
        }
        else
        {
            // Normal saldırı: zar toplamını biriktir, hasarı sıfırla
            int diceSum = payload.diceRolls.Sum();
            storedDamage += diceSum;

            // Tüm zarları 0 yap — knockback hâlâ çalışır ama hasar 0
            for (int i = 0; i < payload.diceRolls.Count; i++)
                payload.diceRolls[i] = 0;
            payload.flatBonus = 0;
            payload.multiplier = 0f;

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
        isReleasing = false;
    }
}
