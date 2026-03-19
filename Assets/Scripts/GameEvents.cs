using System;

/// <summary>
/// Static event bus for decoupled communication between systems.
/// Shop, Inventory, Hotbar, and other managers subscribe/publish here
/// instead of holding direct references to each other.
/// </summary>
public static class GameEvents
{
    // ─── Shop → Inventory ───
    /// <summary>
    /// Fired when a player purchases an item from the shop.
    /// Subscribers (InventoryManager) receive the item to store it.
    /// </summary>
    public static event Action<BaseItem> OnItemPurchased;

    public static void ItemPurchased(BaseItem item)
    {
        OnItemPurchased?.Invoke(item);
    }

    // ─── Inventory → Hotbar UI ───
    /// <summary>
    /// Fired when the inventory contents change (item added, removed, or rearranged).
    /// HotbarUI listens to this to refresh its visual slots.
    /// </summary>
    public static event Action OnInventoryChanged;

    public static void InventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    // ─── Hotbar → Game Systems ───
    /// <summary>
    /// Fired when a player uses/consumes an item from the hotbar.
    /// Passes the item and the slot index it was used from.
    /// </summary>
    public static event Action<BaseItem, int> OnItemUsed;

    public static void ItemUsed(BaseItem item, int slotIndex)
    {
        OnItemUsed?.Invoke(item, slotIndex);
    }

    // ─── Gold Changed ───
    /// <summary>
    /// Fired whenever the player's gold amount changes.
    /// Any UI showing gold can subscribe instead of polling.
    /// </summary>
    public static event Action<int> OnGoldChanged;

    public static void GoldChanged(int newAmount)
    {
        OnGoldChanged?.Invoke(newAmount);
    }

    // ─── Player Movement ───
    /// <summary>
    /// Fired after the player finishes moving to a new cell.
    /// ShopDealer listens to this to detect when the player steps on its tile.
    /// </summary>
    public static event Action<UnityEngine.Vector3Int> OnPlayerMoved;

    public static void PlayerMoved(UnityEngine.Vector3Int cell)
    {
        OnPlayerMoved?.Invoke(cell);
    }

    // ─── Shop Opened / Closed ───
    public static event Action OnShopOpened;
    public static event Action OnShopClosed;

    public static void ShopOpened()
    {
        OnShopOpened?.Invoke();
    }

    public static void ShopClosed()
    {
        OnShopClosed?.Invoke();
    }

    // ─── Collection / Unlock Events ───
    /// <summary>Fired when an enemy is killed. PerkCollectionManager listens for kill-count unlocks.</summary>
    public static event Action<int> OnEnemyKilledTotal;
    public static void EnemyKilledTotal(int totalKills) => OnEnemyKilledTotal?.Invoke(totalKills);

    /// <summary>Fired when a run is completed (win or lose). Passes true if the run was a victory.</summary>
    public static event Action<bool> OnRunCompleted;
    public static void RunCompleted(bool victory) => OnRunCompleted?.Invoke(victory);

    /// <summary>Fired when a boss is defeated.</summary>
    public static event Action OnBossDefeated;
    public static void BossDefeated() => OnBossDefeated?.Invoke();

    /// <summary>Fired when a perk is acquired for the first time in a run.</summary>
    public static event Action<string> OnPerkAcquired;
    public static void PerkAcquired(string perkTypeName) => OnPerkAcquired?.Invoke(perkTypeName);

    /// <summary>Fired when a new perk is unlocked in the collection.</summary>
    public static event Action<string> OnPerkUnlocked;
    public static void PerkUnlocked(string perkId) => OnPerkUnlocked?.Invoke(perkId);

    /// <summary>Fired when total gold earned changes (cumulative across runs).</summary>
    public static event Action<int> OnTotalGoldLifetime;
    public static void TotalGoldLifetime(int totalGold) => OnTotalGoldLifetime?.Invoke(totalGold);

    /// <summary>Fired when a level/room is cleared.</summary>
    public static event Action<int> OnLevelCleared;
    public static void LevelCleared(int totalLevels) => OnLevelCleared?.Invoke(totalLevels);

    /// <summary>Fired when an enemy is pushed into a spike/hazard tile.</summary>
    public static event Action<int> OnEnemyPushedIntoSpike;
    public static void EnemyPushedIntoSpike(int totalCount) => OnEnemyPushedIntoSpike?.Invoke(totalCount);

    /// <summary>Fired when kills-before-boss counter updates. Passes current layer kill count.</summary>
    public static event Action<int> OnKillsBeforeBossUpdated;
    public static void KillsBeforeBossUpdated(int killCount) => OnKillsBeforeBossUpdated?.Invoke(killCount);

    /// <summary>Fired when player skips a turn.</summary>
    public static event Action<int> OnSkipTurnPerformed;
    public static void SkipTurnPerformed(int totalSkips) => OnSkipTurnPerformed?.Invoke(totalSkips);
}
