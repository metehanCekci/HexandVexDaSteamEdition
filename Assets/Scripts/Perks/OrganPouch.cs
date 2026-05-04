using UnityEngine;

public class OrganPouchPerk : BasePerk
{
    public override System.Collections.Generic.Dictionary<string, object> GetDescValues() => new System.Collections.Generic.Dictionary<string, object>
    {
        { "slots", GameKeywords.Counter("+" + currentLevel) },
        { "level", GameKeywords.Action("level") },
        { "cap",   GameKeywords.Counter("5") }
    };

    // Slot grant is handled entirely by OnEquip / OnUnequip — OnAcquire intentionally does
    // nothing extra so a perk that lands directly in stash does NOT grant slots until equipped.
    public override void OnAcquire()
    {
        TriggerVisualPop();
    }

    // Kart tekrar secilirse calisir — only re-apply if currently equipped (active).
    public override void Upgrade()
    {
        base.Upgrade();
        if (RunManager.instance != null && RunManager.instance.activePerks.Contains(this))
            ExpandHotbar();
        TriggerVisualPop();
    }

    // Stash'ten aktife tasindiginda (veya direk aktife edinildiginde) slotlari ac.
    // Idempotent — InventoryManager zaten dogru slot sayisindaysa hicbir sey yapmaz.
    public override void OnEquip()
    {
        if (InventoryManager.instance == null) return;
        int target = 3 + currentLevel;
        int missing = target - InventoryManager.instance.maxSlots;
        for (int i = 0; i < missing; i++)
            ExpandHotbar();
    }

    // Stash'e tasindiginda (veya satildiginda) slotlari kuculsun.
    // Always shrinks to base 3 — both stashing and selling drop the bonus slots, since
    // a stashed perk's bonuses don't apply. CanUnequip gates this from being destructive.
    public override void OnUnequip()
    {
        if (InventoryManager.instance == null) return;
        int excess = InventoryManager.instance.maxSlots - 3;
        for (int i = 0; i < excess; i++)
            ShrinkHotbar();
    }

    // Refuse unequip whenever any bonus slot (slot 3+) currently holds an item — those
    // slots are about to be torn down and the items would silently disappear.
    public override bool CanUnequip()
    {
        if (InventoryManager.instance == null) return true;
        int currentMax = InventoryManager.instance.maxSlots;
        int slotsToRemove = currentMax - 3;
        if (slotsToRemove <= 0) return true;

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