using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Stelzer's Gambit — Common. SADECE tek-yuzlu (1, 3, 5) zarlar bir kez daha islenir.
/// 30 roll (saldiri) sonra implant yok olur.
/// Mimetic ile replay olunca: dice retrigger katkisi 2x olur (her tek zar 2 ekstra retrigger),
/// ama rollsRemaining decay ve patlama SADECE orijinalde.
/// </summary>
public class StelzerGambitPerk : BasePerk
{
    private const int MaxRolls = 30;
    public int rollsRemaining = MaxRolls;

    // Bu kombatta kac kez Mimetic/Leftmost/Parasitic ile replay edildim?
    // OnDiceScored bu sayiya gore ekstra retrigger ister. BeforeCombat'ta sifirlanir.
    private int replayCount = 0;

    // Replay aktif — dice retrigger katkisi cogalir, ama decay/patlama sadece orijinalde.
    public override bool CanBeRetriggeredByPerks => true;

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "odd",   GameKeywords.Status("odd dice") },
        { "extra", GameKeywords.RetriggerN(1) },
        { "rolls", GameKeywords.Counter(rollsRemaining.ToString()) }
    };

    public override IEnumerator OnEvent(CombatContext ctx)
    {
        switch (ctx.eventType)
        {
            case CombatEventType.BeforeCombat:
                replayCount = 0;
                yield break;

            case CombatEventType.OnAttack:
                if (ctx.currentPerk != this) yield break;
                if (rollsRemaining <= 0) yield break;

                if (ctx.isReplay)
                {
                    // Replay: sadece dice retrigger katkisini cogalt, decay yapma.
                    replayCount++;
                    yield break;
                }

                // Orijinal: rollsRemaining decay + patlama
                rollsRemaining--;
                RebuildDescription();
                if (rollsRemaining <= 0)
                {
                    ctx.AnimatePop(this);
                    if (this != null && gameObject != null && RunManager.instance != null)
                        StartCoroutine(DecayRoutine());
                }
                break;

            case CombatEventType.OnDiceScored:
                if (rollsRemaining <= 0) yield break;
                if (ctx.retrigCountSoFar != 0) yield break;
                if (ctx.diceValue % 2 != 1) yield break;
                // 1 (orijinal) + replayCount (her Mimetic kopyasi 1 ekler)
                ctx.RequestExtraDicePass(ctx.diceIndex, 1 + replayCount);
                break;
        }
    }

    private System.Collections.IEnumerator DecayRoutine()
    {
        // Combat sonuna kadar UI'da goster, sonra kaldir.
        // Combat bittikten sonra silinmesi icin TurnManager'in OnCombatEnd hook'una baglanmadik;
        // bu coroutine bir frame bekleyip RunManager listesinden cikarir, sonra UI refresh tetikler.
        yield return null;
        if (RunManager.instance == null) yield break;
        RunManager.instance.activePerks.Remove(this);
        RunManager.instance.inventoryPerks.Remove(this);
        OnUnequip();

        // UI refresh — perk listesi ve aktif perk barini guncelle
        if (RunManager.instance != null)
            RunManager.instance.RefreshPerkUI();
        if (ActivePerkBar.instance != null)
            ActivePerkBar.instance.RefreshBar();

        Destroy(gameObject);
    }
}
