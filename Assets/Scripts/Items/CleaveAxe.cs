using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/CleaveAxe", fileName = "CleaveAxe")]
public class CleaveAxe : BaseItem
{
    void OnEnable()
    {
        itemName = "Cleave-Axe";
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        // Token kullanim ornegi: { "tokenName", GameKeywords.Mult(2) } gibi.
        // Bu item'in dinamik degeri yok; suffix'leri inline tag'le Inspector'da yazilir.
    };

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.cleaveNextCombatStacks++;
        return true;
    }
}
