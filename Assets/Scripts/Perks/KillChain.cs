using UnityEngine;

/// <summary>
/// Kill Chain (Epic)
/// Bir turda düşman öldürdüğünde +1 ekstra hamle kazan.
/// Lv başına +1 ekstra hamle per kill.
/// Lv1: +1, Lv2: +2, Lv3: +3
/// </summary>
public class KillChainPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Epic;
    }

    public override void OnEnemyKilled(EnemyMovement enemy)
    {
        if (RunManager.instance == null || TurnManager.instance == null) return;

        RunManager.instance.remainingMoves += currentLevel;
        TriggerVisualPop();
    }
}
