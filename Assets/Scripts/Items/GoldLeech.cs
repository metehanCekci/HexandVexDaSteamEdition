using UnityEngine;

[CreateAssetMenu(menuName = "Items/GoldLeech", fileName = "GoldLeech")]
public class GoldLeech : BaseItem
{
    void OnEnable()
    {
        itemName = "Gold-Leech";
        description = $"Next enemy drops {GameKeywords.Times(2f)} <color=#{UIColors.Gold}>gold</color>";
        price = 12;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleGoldNextKill = true;
        return true;
    }
}
