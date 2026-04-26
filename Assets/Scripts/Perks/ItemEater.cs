using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Item Eater — Common. Itemleri bu perke "yedirerek" guclendirilir.
/// Base 1x carpan, her yedirilen item basina +0.5x (level scale: L1 +0.5, L2 +0.65, L3 +0.8).
/// Drag-drop UI henuz yok — UI eklendiginde FeedItem(BaseItem) public API'si kullanilir,
/// item envanterden silinir + feedCount artar.
/// Inspector flag feedOnItemUsed acilirsa kullanilan her item de besleme sayilir (fallback mod).
/// </summary>
public class ItemEaterPerk : BasePerk
{
    [Header("Item Eater")]
    [Tooltip("Drag-drop UI yokken: kullanilan her item beslenme olarak sayilir.")]
    public bool feedOnItemUsed = false;

    [System.NonSerialized] public int feedCount = 0;

    void OnEnable()
    {
        maxLevel = 3;
        rarity = PerkRarity.Common;
        if (string.IsNullOrEmpty(description))
            description = "Feed items to grow. Base {base}, each fed item adds {bonus}.";
        RebuildDescription();
    }

    public override void OnAcquire()
    {
        GameEvents.OnItemUsed += HandleItemUsed;
    }

    void OnDestroy()
    {
        GameEvents.OnItemUsed -= HandleItemUsed;
    }

    private void HandleItemUsed(BaseItem item, int slotIndex)
    {
        if (!feedOnItemUsed) return;
        if (item == null) return;
        feedCount++;
        RebuildDescription();
        TriggerVisualPop();
    }

    /// <summary>
    /// Drag-drop UI cagirir: bir item bu perke yedirildi. Item envanterden silinmis olmali (caller sorumlu).
    /// Tek seferlik permanent stack — combat sonu reset edilmez.
    /// </summary>
    public void FeedItem(BaseItem item)
    {
        if (item == null) return;
        feedCount++;
        RebuildDescription();
        TriggerVisualPop();
    }

    private float BonusPerFeed()
    {
        // L1 +0.5, L2 +0.65, L3 +0.8
        return 0.5f + 0.15f * (currentLevel - 1);
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "base", "X1" },
        { "bonus", "X" + BonusPerFeed().ToString("0.##", CultureInfo.InvariantCulture) },
        { "current", "X" + (1f + BonusPerFeed() * feedCount).ToString("0.##", CultureInfo.InvariantCulture) },
        { "fed", feedCount.ToString() }
    };

    public override void ModifyCombat(CombatPayload payload)
    {
        float mult = 1f + BonusPerFeed() * feedCount;
        if (mult <= 1f) return;
        payload.ApplyMult(mult);
        TriggerVisualPop();
    }
}
