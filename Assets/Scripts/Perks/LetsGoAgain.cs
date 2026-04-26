using System.Collections.Generic;

public class LetsGoAgainPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "again", GameKeywords.RetriggerN(1) }
    };
    // Bu perk ModifyCombat ile Ã§alÄ±ÅŸmaz.
    // TurnManager, nihai hasar hesabÄ± bittikten sonra bu perkin varlÄ±ÄŸÄ±nÄ± kontrol eder
    // ve tÃ¼m perkleri tekrar tetikler.
}
