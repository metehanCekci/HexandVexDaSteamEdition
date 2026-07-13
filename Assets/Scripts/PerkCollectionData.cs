using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bir perkin koleksiyondaki unlock koşullarını ve meta verisini tanımlar.
/// Her perk prefab'ı için bir tane oluşturulur.
/// </summary>
[CreateAssetMenu(menuName = "HexAndVex/Perk Collection Entry")]
public class PerkCollectionData : ScriptableObject
{
    [Header("Perk Referansı")]
    [Tooltip("Bu girişin bağlı olduğu perk prefab'ı")]
    public GameObject perkPrefab;

    [Header("Koleksiyon Bilgileri")]
    [Tooltip("Koleksiyonda gösterilecek benzersiz ID (prefab adı kullanılır)")]
    public string perkId;

    [Tooltip("Koleksiyonda gösterilecek lore/flavor text")]
    [TextArea(2, 4)]
    public string loreText;

    [Header("Rarity (MergedShopManager listelerinden belirlenir)")]
    public PerkRarity rarity = PerkRarity.Common;

    [Header("Unlock Koşulu")]
    public UnlockCondition unlockCondition = UnlockCondition.Default;

    [Tooltip("Koşul için gereken miktar (ör: 50 düşman öldür, 3 run tamamla)")]
    public int requiredAmount = 1;

    [Tooltip("Bazı koşullar için ek parametre (ör: belirli perk adı, boss adı)")]
    public string conditionParam = "";

    [Tooltip("Unlock koşulunun açıklaması (UI'da gösterilir)")]
    [TextArea(1, 2)]
    public string unlockHint = "";
}

/// <summary>
/// Perk unlock koşul türleri.
/// </summary>
public enum UnlockCondition
{
    /// <summary>Oyun başından itibaren açık.</summary>
    Default,

    /// <summary>Toplam X düşman öldür (tüm run'lar boyunca).</summary>
    KillEnemies,

    /// <summary>X run tamamla (kazanma/kaybetme fark etmez).</summary>
    CompleteRuns,

    /// <summary>X run kazan.</summary>
    WinRuns,

    /// <summary>Toplam X altın kazan (tüm run'lar boyunca).</summary>
    CollectGold,

    /// <summary>Herhangi bir boss'u yen.</summary>
    DefeatBoss,

    /// <summary>Belirli bir perki bir run'da kullan (conditionParam = perk type name).</summary>
    UseSpecificPerk,

    /// <summary>Toplam X level/oda temizle.</summary>
    ClearLevels,

    /// <summary>Tek run'da X düşman öldür.</summary>
    KillEnemiesSingleRun,

    /// <summary>X farklı perki aç (koleksiyonda).</summary>
    UnlockOtherPerks,

    /// <summary>Düşmanı spike'a it (toplam X kez).</summary>
    PushEnemyIntoSpike,

    /// <summary>Boss'a ulaşmadan tek layer'da X düşman öldür (boss node'unda sıfırlanır).</summary>
    KillBeforeBoss,

    /// <summary>Aynı anda X gold taşı (harcamadan elde tut).</summary>
    HoldGold,

    /// <summary>Toplam X tile yürü.</summary>
    MoveTiles,

    /// <summary>Toplam X kez tur atla (skip).</summary>
    SkipTurns
}