using UnityEngine;

public class OrganPouchPerk : BasePerk
{
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
}