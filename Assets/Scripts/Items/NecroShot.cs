using UnityEngine;

[CreateAssetMenu(menuName = "Items/NecroShot", fileName = "NecroShot")]
public class NecroShot : BaseItem
{
    void OnEnable()
    {
        itemName = "Necro-Shot";
        description = $"<color=#{UIColors.Damage}>Instantly kill</color> any non-boss enemy on the map";
        price = 30;
    }

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;

        // Haritada NecroShot ile öldürülebilecek düşman yoksa (sadece Boss kaldıysa) kullanılamaz
        bool hasNonBossEnemy = false;
        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy != null && enemy.health.currentHP > 0 && !enemy.IsBoss)
            {
                hasNonBossEnemy = true;
                break;
            }
        }
        if (!hasNonBossEnemy) return false;

        // Oyuncunun bir düşman seçmesini beklemek için modu aç
        TurnManager.instance.StartNecroShotTargeting();
        return true;
    }
}
