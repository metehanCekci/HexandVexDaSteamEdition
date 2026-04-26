using System.Collections.Generic;

/// <summary>
/// Extra Ammo — Rare. Her combat'ta itemleri 1 fazla kullanabilirsin.
/// Combat basinda envanterdeki tum itemlere extraUses set eder.
/// Combat sirasinda satin alinan itemlere de buff uygular.
/// </summary>
public class ExtraAmmoPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Rare;
        if (string.IsNullOrEmpty(description))
            description = "Use your items {extra}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "extra", "1 extra time per combat" }
    };

    public override void OnAcquire()
    {
        GameEvents.OnInventoryChanged += ApplyToAllSlots;
        ApplyToAllSlots();
    }

    void OnDestroy()
    {
        GameEvents.OnInventoryChanged -= ApplyToAllSlots;
    }

    public override void OnLevelStart()
    {
        ApplyToAllSlots();
    }

    private void ApplyToAllSlots()
    {
        if (InventoryManager.instance == null) return;
        for (int i = 0; i < InventoryManager.instance.SlotCount; i++)
        {
            var item = InventoryManager.instance.GetItem(i);
            // Sadece henuz kullanilmamis itemlere ekstra hak ver — kullanildiktan sonra
            // tekrar set edersek sonsuz dongu olur.
            if (item != null && !item.usedThisCombat) item.extraUses = 1;
        }
    }

}
