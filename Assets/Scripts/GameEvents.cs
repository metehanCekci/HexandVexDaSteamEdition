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

    /// <summary>
    /// Fired when a perk is sold (sell box). Subscribers receive the perk instance BEFORE Destroy() runs,
    /// so they can read its type/level. Invisible Joker listens to this.
    /// </summary>
    public static event Action<BasePerk> OnPerkSold;
    public static void PerkSold(BasePerk perk) => OnPerkSold?.Invoke(perk);

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

    // ─── Piggy Bank / Interest ───
    /// <summary>
    /// Fired whenever the previewed interest amount changes (gold changed, cap changed, modifiers changed).
    /// UI subscribes to show the live "you will earn N/cap" readout. Passes (preview, cap).
    /// </summary>
    public static event Action<int, int> OnInterestPreview;
    public static void InterestPreview(int preview, int cap) => OnInterestPreview?.Invoke(preview, cap);

    /// <summary>
    /// Fired just before interest is paid out, giving perks/items a chance to mutate the payout.
    /// The payload is a mutable reference — subscribers write to Amount / Cap directly.
    /// Use this for Compound Interest perks, Fat Pig cap boosters, etc.
    /// </summary>
    public static event Action<InterestPayload> OnInterestCalculating;
    public static void InterestCalculating(InterestPayload payload) => OnInterestCalculating?.Invoke(payload);

    /// <summary>
    /// Fired after interest has been added to currentGold. Passes the final amount that was paid.
    /// Collection / achievements subscribe here to track "total interest earned".
    /// </summary>
    public static event Action<int> OnInterestPaid;
    public static void InterestPaid(int amount) => OnInterestPaid?.Invoke(amount);
}

/// <summary>
/// Mutable payload passed to OnInterestCalculating subscribers so perks/items can modify interest payout.
/// </summary>
public class InterestPayload
{
    /// <summary>Gold the player had when interest was calculated (read-only snapshot).</summary>
    public int SourceGold;
    /// <summary>Configured gold-per-interest ratio (e.g. 10 means 1 interest per 10 gold).</summary>
    public int GoldPerInterest;
    /// <summary>Final interest amount to pay — mutate freely.</summary>
    public int Amount;
    /// <summary>Interest cap — mutate freely (e.g. Fat Pig perk raises this).</summary>
    public int Cap;
}
