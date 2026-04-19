using UnityEngine;

[CreateAssetMenu(menuName = "Items/MutaGen", fileName = "MutaGen")]
public class MutaGen : BaseItem
{
    void OnEnable()
    {
        itemName = "Muta-Gen";
        description = $"Restore {GameKeywords.Heal(2)}";
        price = 15;
    }

    public override bool Use()
    {
        if (TurnManager.instance?.player?.health == null) return false;
        if (TurnManager.instance.player.health.currentHP >= TurnManager.instance.player.health.maxHP) return false;

        TurnManager.instance.player.health.Heal(2);
        return true;
    }
}
