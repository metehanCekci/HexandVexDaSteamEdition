using System.Collections.Generic;

public class AdrenalSurgePerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "attack", GameKeywords.Action("attack") },
        { "mult",   GameKeywords.Mult(2) }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        payload.ApplyMult(2.0f);
        if (TurnManager.instance != null && !TurnManager.instance.skipDiceVisuals)
            TriggerVisualPop();
    }
}
