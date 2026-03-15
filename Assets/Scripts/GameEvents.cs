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
}
