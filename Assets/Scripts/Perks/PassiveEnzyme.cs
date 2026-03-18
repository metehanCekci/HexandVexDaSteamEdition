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
       
        RunManager.instance.currentGold += (2 * currentLevel);
        GameEvents.GoldChanged(RunManager.instance.currentGold);
        TriggerVisualPop();
    }
}
