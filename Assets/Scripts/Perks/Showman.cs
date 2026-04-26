using System.Collections.Generic;

/// <summary>
/// Showman — Legendary. Ayni perkten ve itemden birden fazla bulundurmaya izin verir.
/// Perk tarafi: RunManager.AddPerk Showman varsa duplicate perki upgrade etmek yerine yeni instance olarak ekler.
/// Item tarafi: shop'tan ayni iteme tekrar sahip olmaya zaten engel yok (envanter slot bazli).
/// Onemli: ayni iteme sahip olundugunda her instance'in kendi state'i olur (InventoryManager item'i klonluyor).
/// Yani 2 Frag-Mine taktiginda her birinin kendi cooldown'u var, biri kullanildiginda digeri etkilenmez.
/// </summary>
public class ShowmanPerk : BasePerk
{
    void OnEnable()
    {
        maxLevel = 1;
        rarity = PerkRarity.Legendary;
        if (string.IsNullOrEmpty(description))
            description = "You may have multiple copies of the same {target}.";
        RebuildDescription();
    }

    public override Dictionary<string, object> GetDescValues() => new Dictionary<string, object>
    {
        { "target", "perk and item" }
    };
}
