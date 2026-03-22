using UnityEngine;

public class PassiveEnzymePerk : BasePerk
{
    // YENİ: Kart tekrar seçilirse sadece seviyeyi artır (Matematiği OnSkip içinde halledeceğiz)
    public override void Upgrade()
    {
        base.Upgrade(); 
        TriggerVisualPop();
    }

    public override void OnSkip()
    {
        // Son 1 düşman kaldıysa çalışmasın — sonsuz para kasma engeli
        if (TurnManager.instance != null && TurnManager.instance.enemies != null)
        {
            int alive = 0;
            foreach (var e in TurnManager.instance.enemies)
                if (e != null && e.health.currentHP > 0) alive++;
            if (alive <= 1) return;
        }

        RunManager.instance.currentGold += (2 * currentLevel);
        GameEvents.GoldChanged(RunManager.instance.currentGold);
        TriggerVisualPop();
    }
}
