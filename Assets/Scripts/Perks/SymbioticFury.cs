public class SymbioticFuryPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "mult", GameKeywords.Crit("multiply") },
        { "add",  GameKeywords.Status("adding") }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.multiplyInsteadOfAdd = true;
    }
}
