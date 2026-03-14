using UnityEngine;

[CreateAssetMenu(menuName = "Items/NecroShot", fileName = "NecroShot")]
public class NecroShot : BaseItem
{
    void OnEnable()
    {
        itemName = "Necro-Shot";
        description = "Instantly kill any enemy on the map";
        price = 10;
    }

    public override bool Use()
    {
        if (TurnManager.instance == null) return false;

        // Haritada NecroShot ile öldürülebilecek düşman yoksa (sadece Boss kaldıysa) kullanılamaz
        bool hasNonBossEnemy = false;
        foreach (var enemy in TurnManager.instance.enemies)
        {
            if (enemy != null && enemy.health.currentHP > 0 && enemy.enemyBehavior != EnemyAI.EnemyBehavior.Boss)
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
