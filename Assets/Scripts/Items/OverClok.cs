using UnityEngine;

[CreateAssetMenu(menuName = "Items/OverClok", fileName = "OverClok")]
public class OverClok : BaseItem
{
    void OnEnable()
    {
        itemName = "Over-Clock";
        description = "Deal 2x damage on your next dice roll";
        price = 21;
    }

    public override bool Use()
    {
        if (RunManager.instance == null) return false;

        RunManager.instance.doubleDamageNextCombat = true;
        return true;
    }
}
