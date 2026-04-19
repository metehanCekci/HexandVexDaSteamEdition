using System.Collections.Generic;

public class LetsGoAgainPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Secret;
        maxLevel = 1;
        priority = 0;
        if (string.IsNullOrEmpty(description))
            description = "After all perks trigger, they all trigger {again}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "again", "x2 once more" }
    };
    // Bu perk ModifyCombat ile çalışmaz.
    // TurnManager, nihai hasar hesabı bittikten sonra bu perkin varlığını kontrol eder
    // ve tüm perkleri tekrar tetikler.
}
