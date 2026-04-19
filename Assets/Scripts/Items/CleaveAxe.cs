using UnityEngine;

[CreateAssetMenu(menuName = "Items/CleaveAxe", fileName = "CleaveAxe")]
public class CleaveAxe : BaseItem
{
    void OnEnable()
    {
        itemName = "Cleave-Axe";
        description = $"Next attack deals <color=#{UIColors.Damage}>full damage</color> to all adjacent enemies without splitting";
        price = 21;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;
        RunManager.instance.cleaveNextCombat = true;
        return true;
    }
}
