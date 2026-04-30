using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// EVENT-DRIVEN COMBAT PIPELINE (Balatro-style)
// =========================================================================
// Pipeline sadece event yayinlar, perkleri tip olarak tanimaz.
// Perk: BasePerk.OnEvent(ctx) override eder, IEnumerator dondurur.
// Icinde animasyon icin yield return ctx.WaitFor(saniye) ve ctx.AnimatePop(this).
// Retrigger istegi:
//   ctx.RequestExtraDicePass(diceIndex, count)  -> dice retrigger
//   ctx.RequestPerkReplay(targetPerk)           -> perk retrigger
// =========================================================================

public enum CombatEventType
{
    /// <summary>Pipeline basliyor, base damage init edildi.</summary>
    BeforeCombat,
    /// <summary>Perkin ana etkisi. Inspector sirasinda yayinlanir. ctx.currentPerk sen misin diye kontrol et.</summary>
    OnAttack,
    /// <summary>Bir zar degerlendirilirken (orijinal veya retrigger) tum perklere yayinlanir.</summary>
    OnDiceScored,
    /// <summary>LetsGoAgain ikinci pass.</summary>
    OnLetsGoAgain,
    /// <summary>Tum pass'ler bitti.</summary>
    AfterCombat,
}

/// <summary>
/// Tek bir event'in payload'i. Perk OnEvent ile bunu alir, ihtiyaci olan field'lari okur.
/// Animasyon icin yield return ctx.WaitFor(saniye), ctx.AnimatePop(this) cagir.
/// Retrigger gerekiyorsa Request* metodlariyla pipeline'a bayrak diker.
/// </summary>
public class CombatContext
{
    public CombatEventType eventType;
    public CombatPayload payload;
    public List<int> rolls;
    public DiceUIController diceUI;

    // OnAttack icin: o an islenen perk (perkin kendisi). OnEvent icinde "ben miyim?" demek isteyenler icin.
    public BasePerk currentPerk;

    /// <summary>
    /// Bu OnAttack cagrisi Mimetic/Leftmost/Parasitic tarafindan tetiklenmis bir replay mi?
    /// Stackli/decay'li perkler buna bakarak farkli davranir:
    ///   - State decay (rollsRemaining--, stack=0) sadece orijinalde yapilir.
    ///   - Damage etkisi snapshot'tan tekrar uygulanir.
    /// Sadece OnAttack event'inde anlamli; diger event tipleri icin false.
    /// </summary>
    public bool isReplay = false;

    // OnDiceScored icin doldurulur:
    public int diceIndex = -1;
    public int diceValue = 0;
    /// <summary>Bu zar icin onceki kac retrigger yapildi (0 = orijinal pass).</summary>
    public int retrigCountSoFar = 0;

    private CombatPipeline pipeline;

    public CombatContext(CombatPipeline pipeline)
    {
        this.pipeline = pipeline;
    }

    // ====================================================================
    // RETRIGGER REQUESTS
    // ====================================================================

    /// <summary>
    /// Pipeline'dan istek: belirtilen zarin OnDiceScored'unu N kez daha yayinla.
    /// Hanging Nerve / Stelzer / Sensory tarzi perkler BU metodu cagirir.
    /// </summary>
    public void RequestExtraDicePass(int diceIndex, int count)
    {
        pipeline?.RequestExtraDicePass(diceIndex, count);
    }

    /// <summary>
    /// Pipeline'dan istek: target perkin OnAttack'ini bir kez daha calistir.
    /// Mimetic / Leftmost / Parasitic tarzi perkler BU metodu cagirir.
    /// </summary>
    public void RequestPerkReplay(BasePerk target)
    {
        pipeline?.RequestPerkReplay(target);
    }

    // ====================================================================
    // ANIMATION HELPERS — perkler bunlari yield return ile kullanir.
    // ====================================================================

    /// <summary>
    /// skipDiceVisuals true ise hizla geri doner (bekleme yok). Yoksa SkippableWait.
    /// Kullanim: yield return ctx.WaitFor(0.4f);
    /// </summary>
    public IEnumerator WaitFor(float seconds)
    {
        if (diceUI == null || diceUI.skipDiceVisuals) yield break;
        yield return diceUI.SkippableWait(seconds);
    }

    /// <summary>
    /// Perkin kendi pop animasyonunu tetikler + perk listesinde shake yapar.
    /// skipDiceVisuals'da no-op.
    /// </summary>
    public void AnimatePop(BasePerk perk)
    {
        if (perk == null) return;
        if (diceUI == null || diceUI.skipDiceVisuals) return;
        perk.TriggerVisualPop();
        if (PerkListUI.instance != null)
            PerkListUI.instance.TriggerShakeForPerk(perk);
    }

    /// <summary>Belirli bir zarin animasyonunu oynat ve damage display'i guncelle.</summary>
    public void AnimateDie(int diceIndex)
    {
        if (diceUI == null || diceUI.skipDiceVisuals) return;
        if (payload == null || diceIndex < 0 || diceIndex >= payload.diceRolls.Count) return;
        if (diceUI.SpawnedDiceUI != null && diceIndex < diceUI.SpawnedDiceUI.Count)
            diceUI.AnimateSpecificDie(diceIndex, payload.diceRolls[diceIndex]);
        diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
    }

    /// <summary>Zar yeniden yuvarlandiginda visual zarini guncelle (RiggedDice/NeuralReboot tarzi).</summary>
    public void RefreshDieVisual(int diceIndex)
    {
        if (diceUI == null || diceUI.skipDiceVisuals) return;
        if (payload == null || diceIndex < 0 || diceIndex >= payload.diceRolls.Count) return;
        if (diceUI.SpawnedDiceUI != null && diceIndex < diceUI.SpawnedDiceUI.Count)
            diceUI.AnimateSpecificDie(diceIndex, payload.diceRolls[diceIndex]);
    }

    /// <summary>Damage display'i taze degerle yenile.</summary>
    public void RefreshTotal()
    {
        if (diceUI == null || diceUI.skipDiceVisuals) return;
        if (payload == null) return;
        diceUI.UpdateTotalDamageDisplay(payload.GetFinalDamage());
    }

    // ====================================================================
    // Internal
    // ====================================================================
    internal void ResetDiceFields()
    {
        diceIndex = -1;
        diceValue = 0;
        retrigCountSoFar = 0;
    }
}
