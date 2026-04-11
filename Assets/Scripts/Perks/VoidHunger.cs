using UnityEngine;

/// <summary>
/// Void Hunger (Common)
/// Çöken her tile (scaffold + seismic) başına kalıcı +0.5x damage multiplier.
/// Run boyunca birikir, sıfırlanmaz.
/// +0.25x per collapsed tile, max level 1
/// </summary>
public class VoidHungerPerk : BasePerk
{
    private int collapsedCount = 0;
    private bool subscribed = false;

    void OnEnable()
    {
        rarity = PerkRarity.Common;
        maxLevel = 1;
    }

    public override void OnAcquire()
    {
        Subscribe();
        description = GetDescription();
    }

    public override void OnEquip()
    {
        Subscribe();
        description = GetDescription();
    }

    public override void OnUnequip()
    {
        Unsubscribe();
        description = GetDescription();
    }

    public override void OnLevelStart()
    {
        Subscribe();
        description = GetDescription();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        TrapTileEvents.OnTileDestroyed += OnTileDestroyed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        TrapTileEvents.OnTileDestroyed -= OnTileDestroyed;
        subscribed = false;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnTileDestroyed(Vector3Int cell)
    {
        collapsedCount++;
        description = GetDescription();
    }

    public override void ModifyCombat(CombatPayload payload)
    {
        if (collapsedCount <= 0) return;

        payload.multiplier += collapsedCount * 0.25f;
        TriggerVisualPop();
    }

    private string GetDescription()
    {
        float totalBonus = collapsedCount * 0.25f;
        return $"Each collapsed tile grants permanent +0.25x damage multiplier.\nCollapsed: {collapsedCount} (+{totalBonus:F1}x)";
    }
}
