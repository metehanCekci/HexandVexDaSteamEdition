using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// MonoBehaviour'dan miras ALMIYOR!
//
// =========================================================================
// BALATRO-STYLE ARDISIK DAMAGE MODELI
// =========================================================================
// Eski sistem: her perk flatBonus += X veya multiplier *= Y ayri kovalara yaziyordu,
// en sonda (base + flat) * mult tek formulle hesapliyordu -> perk sirasi ONEMSIZDI.
//
// Yeni sistem: tek bir `runningDamage` kovasi var. Her perk SIRAYLA calisir ve
// runningDamage'i ANINDA degistirir:
//   - "+X veren" perk  -> payload.ApplyAdd(X)  -> runningDamage += X
//   - "xY veren" perk  -> payload.ApplyMult(Y) -> runningDamage *= Y
// Perk inspector sirasi = Balatro joker sirasi. Sira degisirse sonuc degisir.
//
// Retrigger: per-die retrigger event'ine abone perkler (Photovoltaic gibi) her zar
// retriggerlandiginda anlik olarak kendi cakmalarini uygular. Pipeline:
//   base-pass perk A -> runningDamage += 3
//   base-pass perk B (Photovoltaic) -> runningDamage *= 3
//   retrigger-pass: ilk zar 2 kez retrigger -> her retrigger icin:
//     runningDamage += 3 (zar degeri), sonra OnDiceRetriggerEvent yayinlanir,
//     Photovoltaic hook'ta runningDamage *= 3 yapar.
// Sonuc: +3 x3 +3 x3 +3 x3 ardisik uygulanir.
// =========================================================================
public class CombatPayload
{
    public List<int> diceRolls = new List<int>();

    // Her zar icin kac kez retriggerlandigini tutar (pre-pass tarafindan doldurulur).
    // Index = zarin diceRolls icindeki orijinal konumu. Deger = ekstra retrigger sayisi (0 = retrigger yok).
    // Per-die bagimli perkler (Photovoltaic Pulse, Triboulet-clone vb.) bu listeyi ek olarak okuyabilir,
    // ama ESAS entegrasyon OnDiceRetriggerEvent abonelik pattern'i uzerinden olur (bkz. BasePerk).
    public List<int> diceRetriggerCounts = new List<int>();

    // Ardisik damage kovasi. Perkler ApplyAdd/ApplyMult ile bunu dogrudan gunceller.
    public double runningDamage = 0.0;

    // Legendary perk'ler için özel kurallar
    public bool isCriticalHit = false;
    public bool triggerExplosion = false; // Blast Impact
    public float explosionDamagePercent = 0.5f; // Volatile Cells seviyesine göre değişir
    public bool multiplyInsteadOfAdd = false; // Sword Dance — base hesabi carpim olarak init edilir

    // =========================================================================
    // GERI UYUMLULUK KATMANI (deprecated — mevcut perkleri kirmadan gecis icin).
    // Yeni perkler ApplyAdd / ApplyMult kullanmali.
    // Bu setter'lar runningDamage'i anlik gunceller.
    // =========================================================================
    private long _flatBonusShadow = 0;
    private float _multiplierShadow = 1.0f;

    [Obsolete("Use ApplyAdd(value). flatBonus artik ardisik olarak runningDamage'a yazilir.")]
    public long flatBonus
    {
        get => _flatBonusShadow;
        set
        {
            long delta = value - _flatBonusShadow;
            _flatBonusShadow = value;
            if (delta != 0) runningDamage += delta;
        }
    }

    [Obsolete("Use ApplyMult(value). multiplier artik ardisik olarak runningDamage'a yazilir.")]
    public float multiplier
    {
        get => _multiplierShadow;
        set
        {
            if (_multiplierShadow == 0f)
            {
                _multiplierShadow = value;
                return;
            }
            float ratio = value / _multiplierShadow;
            _multiplierShadow = value;
            if (!Mathf.Approximately(ratio, 1f)) runningDamage *= ratio;
        }
    }

    // =========================================================================
    // PRIMARY API — yeni perkler bunlari kullanmali.
    // =========================================================================
    public void ApplyAdd(double value)
    {
        if (value == 0) return;
        runningDamage += value;
    }

    public void ApplyMult(double value)
    {
        if (value == 1.0) return;
        runningDamage *= value;
    }

    // =========================================================================
    // Per-die retrigger event'i. PerkCombatProcessor her retrigger icin yayinlar.
    // Aboneler: Photovoltaic gibi belirli zara bagli perkler (BasePerk.OnDiceRetriggerEvent).
    // =========================================================================
    public event Action<int, int, CombatPayload> DiceRetriggerEvent;
    public void DispatchDiceRetrigger(int diceIndex, int diceValue)
    {
        DiceRetriggerEvent?.Invoke(diceIndex, diceValue, this);
    }

    public CombatPayload(List<int> rolls)
    {
        // Zarların kopyasını alıyoruz
        diceRolls = new List<int>(rolls);
        // Her zar icin retrigger sayisini 0 ile baslat; pre-pass doldurur.
        diceRetriggerCounts = new List<int>(new int[rolls.Count]);
        // Running damage'i zar tabanindan baslat (Sword Dance icin carpim modu).
        InitializeRunningDamage();
    }

    private void InitializeRunningDamage()
    {
        if (diceRolls.Count == 0) { runningDamage = 0.0; return; }

        if (multiplyInsteadOfAdd)
        {
            double product = diceRolls[0];
            for (int i = 1; i < diceRolls.Count; i++) product *= diceRolls[i];
            runningDamage = product;
        }
        else
        {
            runningDamage = diceRolls.Sum();
        }
    }

    /// <summary>
    /// Base damage'i (zar toplami/carpim) yeniden hesaplar. Perkler diceRolls'a zar eklediginde
    /// veya Sword Dance gibi bayraklar degistiginde cagrilir.
    /// UYARI: runningDamage'i sifirlayip sadece yeni base'i set eder — biriken perk efektleri kaybolur.
    /// Sadece pre-pass veya reset noktalarinda kullan.
    /// </summary>
    public void RebaseRunningDamage()
    {
        InitializeRunningDamage();
    }

    public long GetFinalDamage()
    {
        double total = runningDamage;

        // Kritik vuruş varsa RunManager'daki çarpanla çarp
        if (isCriticalHit && RunManager.instance != null)
        {
            total *= RunManager.instance.criticalDamageMultiplier;
        }

        // Hasarın eksiye düşmemesini ve long.MaxValue'da clamp'lemesini sağla
        if (total <= 0) return 0;
        if (total >= long.MaxValue) return long.MaxValue;
        return (long)total;
    }
}
