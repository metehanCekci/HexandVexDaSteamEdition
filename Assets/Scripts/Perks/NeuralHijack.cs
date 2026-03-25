using UnityEngine;

/// <summary>
/// Neural Hijack (Legendary)
/// Düşmanı başka bir düşmana ittiğinde, itilen düşman taraf değiştirir.
/// Dost düşman: 3 HP, oyuncunun zarlarıyla hasar verir, ölene kadar yaşar.
/// Bir kez dost olan düşman bir daha düşman olamaz.
/// </summary>
public class NeuralHijackPerk : BasePerk
{
    void OnEnable()
    {
        rarity = PerkRarity.Legendary;
    }
}
