using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tüm perk koleksiyon verilerini barındıran ana database.
/// </summary>
[CreateAssetMenu(menuName = "HexAndVex/Perk Collection Database")]
public class PerkCollectionDatabase : ScriptableObject
{
    public List<PerkCollectionData> entries = new List<PerkCollectionData>();
}
