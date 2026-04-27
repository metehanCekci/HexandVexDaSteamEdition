using System.Collections.Generic;

public class SymbioticFuryPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", GameKeywords.Crit("multiply") },
        { "add",  GameKeywords.Status("adding") }
    };

    /// <summary>
    /// En sonda calisir (processLast = true). Diger perkler bittikten sonra:
    ///   1. Zarlarin guncel TOPLAMINI runningDamage'dan cikarir (base'i bos brakir)
    ///   2. Zarlarin guncel CARPIMINI runningDamage'a ekler
    /// Sonuc: ToxinEdge gibi zar-degistiren perklerin etkisi cikti, mult/add bonuslari korunur,
    /// sadece base "toplam" yerine "carpim" olur. Retrigger pass'i normal devam eder.
    /// </summary>
    public override void ModifyCombat(CombatPayload payload)
    {
        if (payload == null || payload.diceRolls == null || payload.diceRolls.Count == 0) return;

        double sum = 0;
        double product = 1;
        for (int i = 0; i < payload.diceRolls.Count; i++)
        {
            int v = payload.diceRolls[i];
            sum += v;
            product *= v;
        }

        payload.runningDamage = payload.runningDamage - sum + product;
        TriggerVisualPop();
    }
}
