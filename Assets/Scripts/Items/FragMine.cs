using UnityEngine;

[CreateAssetMenu(menuName = "Items/FragMine", fileName = "FragMine")]
public class FragMine : BaseItem
{
    void OnEnable()
    {
        itemName = "Frag-Mine";
        description = $"Place a bomb on any hex. Rolls dice and deals <color=#{UIColors.Damage}>damage</color> to all enemies within {GameKeywords.Number(1)} hex radius";
        price = 24;
    }

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;
        TurnManager.instance.StartBombPlacement();
        return true;
    }
}
