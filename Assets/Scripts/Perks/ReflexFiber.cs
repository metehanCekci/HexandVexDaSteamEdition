using System.Collections.Generic;

public class ReflexFiberPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
        if (string.IsNullOrEmpty(description))
            description = "Gain {moves} extra move per turn.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "moves", $"+{currentLevel}" }
    };

    public override void OnAcquire()
    {
        RunManager.instance.extraMovesPerTurn += 1;
        TriggerVisualPop();
    }

    public override void Upgrade()
    {
        base.Upgrade();
        RunManager.instance.extraMovesPerTurn += 1;
    }
}
