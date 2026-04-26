using System.Collections.Generic;

public class ReflexFiberPerk : BasePerk
{
    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "moves", GameKeywords.Plus(currentLevel) }
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
