using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Photovoltaic Pulse — Rare. Ilk atilan zarin degeri kadar multiplier uygulanir
/// (ornek: ilk zar 4 ise multiplier *= 4). Dice retrigger implantlariyla birlestiginde
/// ilk zar kac kez islenirse o kadar kez catlar (X4 X4 X4 zinciri).
/// </summary>
public class PhotovoltaicPulsePerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Multiplies damage by the {first} value.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "first", "first die" }
    };

    public override bool IsIncompatible()
    {
        // Tek bir zar yoksa (teorik olarak her zaman en az 1 zar var ama guvenlik icin)
        return false;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload == null || payload.diceRolls == null || payload.diceRolls.Count == 0) return;
        int first = payload.diceRolls[0];
        if (first <= 0) return;
        payload.multiplier *= first;
    }

    public override IEnumerator AnimatedCombatEffect(CombatPayload payload, DiceUIController diceUI)
    {
        // Gorsel: zarin uzerinde kisa bir pop animasyonu + toplam hasar guncelle.
        // ModifyCombat'taki mult zaten uygulandi, burada sadece step-by-step gorsel geri bildirim.
        TriggerVisualPop();
        if (PerkListUI.instance != null)
            PerkListUI.instance.TriggerShakeForPerk(this);

        if (diceUI != null && !diceUI.skipDiceVisuals)
        {
            if (diceUI.SpawnedDiceUI != null && diceUI.SpawnedDiceUI.Count > 0 && payload.diceRolls.Count > 0)
                diceUI.AnimateSpecificDie(0, payload.diceRolls[0]);
            diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
            yield return StartCoroutine(diceUI.SkippableWait(0.3f));
        }
    }
}
