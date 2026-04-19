using UnityEngine;

public class OrganPouchPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Gain {slots} item slot per level (cap 5).";
        RebuildDescription();
    }

    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "slots", $"+{currentLevel}" }
    };

    // İlk alındığında çalışır (1. Seviye)
    public override void OnAcquire()
    {
        ExpandHotbar();
        TriggerVisualPop();
    }

    // Kart tekrar seçilirse çalışır (2. ve 3. Seviyeler)
    public override void Upgrade()
    {
        base.Upgrade();
        ExpandHotbar();
        TriggerVisualPop();
    }

    // Stash'ten aktife taşındığında slotları tekrar aç
    public override void OnEquip()
    {
        // OnAcquire ile çifte çağrı koruması: InventoryManager zaten doğru slot sayısındaysa açma
        if (InventoryManager.instance == null) return;
        int target = 3 + currentLevel;
        int missing = target - InventoryManager.instance.maxSlots;
        for (int i = 0; i < missing; i++)
            ExpandHotbar();
    }

    // Stash'e taşındığında slotları küçült
    public override void OnUnequip()
    {
        for (int i = 0; i < currentLevel; i++)
            ShrinkHotbar();
    }

    // Fazla slotlarda item varsa çıkarmaya izin verme
    public override bool CanUnequip()
    {
        if (InventoryManager.instance == null) return true;
        int currentMax = InventoryManager.instance.maxSlots;
        int baseSlots = 3;
        int slotsToRemove = Mathf.Min(currentLevel, currentMax - baseSlots);

        for (int i = 0; i < slotsToRemove; i++)
        {
            int slotIndex = currentMax - 1 - i;
            if (InventoryManager.instance.IsSlotOccupied(slotIndex))
                return false;
        }
        return true;
    }

    private void ExpandHotbar()
    {
        // Hotbar slot ekle (max 5)
        if (InventoryManager.instance != null && InventoryManager.instance.maxSlots < 5)
        {
            InventoryManager.instance.AddSlots(1);
            if (HotbarUI.instance != null)
                HotbarUI.instance.AddSlot();
        }
    }

    private void ShrinkHotbar()
    {
        if (InventoryManager.instance != null && InventoryManager.instance.maxSlots > 3)
        {
            InventoryManager.instance.RemoveSlots(1);
            if (HotbarUI.instance != null)
                HotbarUI.instance.RemoveSlot();
        }
    }
}