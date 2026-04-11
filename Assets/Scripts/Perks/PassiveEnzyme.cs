using UnityEngine;

public class PassiveEnzymePerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        description = "Skipping a turn grants 4 gold per level. Does not work on bosses or the last enemy.";
    }

    // YENİ: Kart tekrar seçilirse sadece seviyeyi artır (Matematiği OnSkip içinde halledeceğiz)
    public override void Upgrade()
    {
        base.Upgrade(); 
        TriggerVisualPop();
    }

    public override void OnSkip()
    {
        // Boss sahnesinde çalışmasın
        if (RunManager.instance != null && RunManager.instance.currentNodeType == MapNodeType.Boss) return;

        // Son 1 düşman kaldıysa çalışmasın — sonsuz para kasma engeli
        if (TurnManager.instance != null && TurnManager.instance.enemies != null)
        {
            int alive = 0;
            foreach (var e in TurnManager.instance.enemies)
                if (e != null && e.health.currentHP > 0) alive++;
            if (alive <= 1) return;
        }

        RunManager.instance.currentGold += (4 * currentLevel);
        GameEvents.GoldChanged(RunManager.instance.currentGold);
        TriggerVisualPop();
    }
}
