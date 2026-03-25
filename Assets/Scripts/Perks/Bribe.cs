using UnityEngine;

public class BribePerk : BasePerk
{
    void OnEnable() { maxLevel = 1; rarity = PerkRarity.Epic; }
}
