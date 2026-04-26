using UnityEngine;

/// <summary>
/// Kill Chain (Epic)
/// Bir turda dÃ¼ÅŸman Ã¶ldÃ¼rdÃ¼ÄŸÃ¼nde +1 ekstra hamle kazan.
/// Lv baÅŸÄ±na +1 ekstra hamle per kill.
/// Lv1: +1, Lv2: +2, Lv3: +3
/// </summary>
public class KillChainPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "kill",  GameKeywords.Action("Killing") },
        { "moves", GameKeywords.Plus(currentLevel) }
    };

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        RunManager.instance.remainingMoves += currentLevel;
        TriggerVisualPop();
    }
}
