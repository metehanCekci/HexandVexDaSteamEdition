using System.Collections.Generic;

public class AdrenalSurgePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Every attack deals {mult} damage.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "mult", "X2" }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.ApplyMult(2.0f);
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
