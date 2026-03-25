using UnityEngine;
using System.Linq;

/// <summary>
/// Cascade Protocol (Legendary)
/// Her saldırının zar toplamı (kritik/multiplier HARİÇ) bir sonraki saldırıya flat bonus olarak eklenir.
/// Lv1: %100, Lv2: %125, Lv3: %150 birikim oranı.
/// Oda bitince sıfırlanır.
/// </summary>
public class CascadeProtocolPerk : BasePerk
{
    private int accumulatedDamage = 0;

    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (accumulatedDamage > 0)
        {
            float bonus = accumulatedDamage * (1f + (currentLevel - 1) * 0.25f);
            payload.flatBonus += Mathf.FloorToInt(bonus);
            TriggerVisualPop();
        }

        // Bu saldırının zar toplamını birikime ekle (kritik/mult hariç, sadece raw dice)
        int diceSum = payload.diceRolls.Sum();
        accumulatedDamage += diceSum;
    }

    public override void OnLevelStart()
    {
        accumulatedDamage = 0;
    }
}
