using UnityEngine;

public class OrganPouchPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "slots", GameKeywords.Counter("+" + currentLevel) },
        { "level", GameKeywords.Action("level") },
        { "cap",   GameKeywords.Counter("5") }
    };

    // Ä°lk alÄ±ndÄ±ÄŸÄ±nda Ã§alÄ±ÅŸÄ±r (1. Seviye)
    public override void OnAcquire()
    {
        ExpandHotbar();
        TriggerVisualPop();
    }

    // Kart tekrar seÃ§ilirse Ã§alÄ±ÅŸÄ±r (2. ve 3. Seviyeler)
    public override void Upgrade()
    {
        base.Upgrade();
        ExpandHotbar();
        TriggerVisualPop();
    }

    // Stash'ten aktife taÅŸÄ±ndÄ±ÄŸÄ±nda slotlarÄ± tekrar aÃ§
    public override void OnEquip()
    {
        // OnAcquire ile Ã§ifte Ã§aÄŸrÄ± korumasÄ±: InventoryManager zaten doÄŸru slot sayÄ±sÄ±ndaysa aÃ§ma
        if (InventoryManager.instance == null) return;
        int target = 3 + currentLevel;
        int missing = target - InventoryManager.instance.maxSlots;
        for (int i = 0; i < missing; i++)
            ExpandHotbar();
    }

    // Stash'e taÅŸÄ±ndÄ±ÄŸÄ±nda slotlarÄ± kÃ¼Ã§Ã¼lt
    public override void OnUnequip()
    {
        for (int i = 0; i < currentLevel; i++)
            ShrinkHotbar();
    }

    // Fazla slotlarda item varsa Ã§Ä±karmaya izin verme
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