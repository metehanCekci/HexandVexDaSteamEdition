using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Items/NecroShot", fileName = "NecroShot")]
public class NecroShot : BaseItem
{
    void OnEnable()
    {
        itemName = "Necro-Shot";
        if (string.IsNullOrEmpty(description))
            description = "{kill} any non-boss enemy on the map.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "kill", "Instantly kill" }
    };

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
