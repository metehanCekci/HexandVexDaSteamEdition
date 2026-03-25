using UnityEngine;

/// <summary>
/// Void Hunger (Common)
/// Çöken her tile (scaffold + seismic) başına kalıcı +0.5x damage multiplier.
/// Run boyunca birikir, sıfırlanmaz.
/// Lv başına birikim oranı: Lv1: +0.5x, Lv2: +0.75x, Lv3: +1.0x
/// </summary>
public class VoidHungerPerk : BasePerk
{
    private int collapsedCount = 0;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
    }

    public override void OnAcquire()
    {
        Subscribe();
    }

    public override void OnLevelStart()
    {
        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        TrapTileEvents.OnTileDestroyed += OnTileDestroyed;
        subscribed = true;
    }

    void OnDestroy()
    {
        TrapTileEvents.OnTileDestroyed -= OnTileDestroyed;
        subscribed = false;
    }

    private void OnTileDestroyed(Vector3Int cell)
    {
        collapsedCount++;
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (collapsedCount <= 0) return;

        float bonusPerTile = 0.25f + currentLevel * 0.25f; // Lv1: 0.5, Lv2: 0.75, Lv3: 1.0
        payload.multiplier += collapsedCount * bonusPerTile;
        TriggerVisualPop();
    }
}
