using UnityEngine;

public enum ItemType
{
    Consumable,   // Goes into inventory/hotbar, used during combat (FragMine, SurgeBoot, etc.)
    Instant       // Applied immediately on purchase, never enters inventory (SecretPerkOrb, MutationCatalyst)
}

public abstract class BaseItem : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public int price;
    public Sprite icon;

    [Header("Inventory Behavior")]
    [Tooltip("Consumable = goes to hotbar on purchase. Instant = applied immediately on purchase.")]
    public ItemType itemType = ItemType.Consumable;

    /// <summary>
    /// Runtime flag: true ise bu item bu combat'ta zaten kullanıldı.
    /// Combat bittiğinde InventoryManager tarafından resetlenir.
    /// </summary>
    [System.NonSerialized] public bool usedThisCombat;

    /// <summary>
    /// Item kullanıldığında çağrılır. true dönerse item tüketilmiş demektir.
    /// </summary>
    public abstract bool Use();
}
