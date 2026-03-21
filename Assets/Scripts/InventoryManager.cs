using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the player's item inventory (hotbar items).
/// Persists across scenes via DontDestroyOnLoad.
/// Communicates through GameEvents — no direct references to Shop or HotbarUI.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    [Header("Inventory Config")]
    [Tooltip("Maximum number of hotbar slots the player has.")]
    public int maxSlots = 3;

    /// <summary>
    /// The actual inventory. Null entries mean empty slots.
    /// Index = hotbar slot index.
    /// </summary>
    private BaseItem[] slots;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSlots();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable()
    {
        GameEvents.OnItemPurchased += HandleItemPurchased;
    }

    void OnDisable()
    {
        GameEvents.OnItemPurchased -= HandleItemPurchased;
    }

    // ─── Initialization ───

    private void InitializeSlots()
    {
        slots = new BaseItem[maxSlots];
    }

    /// <summary>
    /// Call at the start of a new run to wipe inventory clean.
    /// </summary>
    public void ResetForNewRun()
    {
        slots = new BaseItem[maxSlots];
        GameEvents.InventoryChanged();
    }

    // ─── Event Handlers ───

    private void HandleItemPurchased(BaseItem item)
    {
        if (item == null) return;

        // Instant items (SecretPerkOrb, MutationCatalyst) are used immediately — never stored
        if (item.itemType == ItemType.Instant)
        {
            item.Use();
            return;
        }

        // Consumable items go into the first available hotbar slot
        if (!TryAddItem(item))
        {
            Debug.LogWarning($"InventoryManager: Inventory full! Could not add {item.itemName}.");
        }
    }

    // ─── Public API ───

    /// <summary>
    /// Try to add an item to the first empty slot. Returns true on success.
    /// </summary>
    public bool TryAddItem(BaseItem item)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                GameEvents.InventoryChanged();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Use the item in the given slot. Returns true if item was consumed.
    /// </summary>
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;

        BaseItem item = slots[slotIndex];
        if (item == null) return false;

        bool used = item.Use();
        if (used)
        {
            // Cache item info for ESC cancel before clearing slot
            if (TurnManager.instance != null && TurnManager.instance.IsAnyTargetingActive)
                TurnManager.instance.SetTargetingItemCache(item, slotIndex);

            GameEvents.ItemUsed(item, slotIndex);
            slots[slotIndex] = null;
            GameEvents.InventoryChanged();

            if (AudioManager.instance != null)
                AudioManager.instance.PlayPurchase(); // Reuse the purchase SFX for item use
        }
        return used;
    }

    /// <summary>
    /// Get the item in a specific slot (null if empty).
    /// </summary>
    public BaseItem GetItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return null;
        return slots[slotIndex];
    }

    /// <summary>
    /// Get the current slot count.
    /// </summary>
    public int SlotCount => slots != null ? slots.Length : 0;

    /// <summary>
    /// Check if inventory has any empty slot.
    /// </summary>
    public bool HasEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) return true;
        return false;
    }

    /// <summary>
    /// Get count of occupied slots.
    /// </summary>
    public int OccupiedSlotCount()
    {
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) count++;
        return count;
    }

    /// <summary>
    /// Restore an item to a specific slot (used when targeting is cancelled with ESC).
    /// </summary>
    public void RestoreItem(int slotIndex, BaseItem item)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        slots[slotIndex] = item;
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// Remove an item from a specific slot without using it.
    /// </summary>
    public void RemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        slots[slotIndex] = null;
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// Expand inventory by adding more slots.
    /// </summary>
    public void AddSlots(int count)
    {
        int newMax = maxSlots + count;
        BaseItem[] newSlots = new BaseItem[newMax];
        for (int i = 0; i < slots.Length; i++)
            newSlots[i] = slots[i];
        slots = newSlots;
        maxSlots = newMax;
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// Shrink inventory by removing slots from the end.
    /// Only call after confirming those slots are empty.
    /// </summary>
    public void RemoveSlots(int count)
    {
        int newMax = Mathf.Max(3, maxSlots - count);
        BaseItem[] newSlots = new BaseItem[newMax];
        for (int i = 0; i < newMax; i++)
            newSlots[i] = slots[i];
        slots = newSlots;
        maxSlots = newMax;
        GameEvents.InventoryChanged();
    }

    /// <summary>
    /// Check if a specific slot has an item.
    /// </summary>
    public bool IsSlotOccupied(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;
        return slots[slotIndex] != null;
    }
}
