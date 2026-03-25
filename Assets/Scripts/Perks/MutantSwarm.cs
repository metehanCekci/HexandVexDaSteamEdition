using UnityEngine;

public class MutantSwarmPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Rare;
    }

    // YENİ: Kart tekrar seçilirse seviye artsın
    public override void Upgrade()
    {
        base.Upgrade();
        TriggerVisualPop();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        float bonusPerDie = 0.5f * currentLevel;
        float extraMult = 1.0f + (payload.diceRolls.Count * bonusPerDie);
        
        payload.multiplier *= extraMult;
        TriggerVisualPop();
    }
}
