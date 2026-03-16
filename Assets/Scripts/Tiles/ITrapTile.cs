/// <summary>
/// Haritadaki tüm etkileşimli/tehlikeli tile'lar için ortak arayüz.
/// Dikenli tile, scaffold tile ve gelecekte eklenecek her tile bu interface'i uygular.
/// Entity bağımsızdır — Player/Enemy sınıflarını bilmez.
/// </summary>
public interface ITrapTile
{
    /// <summary>Tile'ın grid üzerindeki benzersiz hücre pozisyonu.</summary>
    UnityEngine.Vector3Int Cell { get; }

    /// <summary>Tile'ın şu anki durumu.</summary>
    TrapTileState CurrentState { get; }

    /// <summary>Herhangi bir varlık (oyuncu/düşman) tile'ın üzerine bastığında çağrılır.</summary>
    void OnEntityEnter(UnityEngine.Vector3Int cell);

    /// <summary>Herhangi bir varlık tile'dan ayrıldığında çağrılır.</summary>
    void OnEntityExit(UnityEngine.Vector3Int cell);

    /// <summary>Tile hâlâ üzerinden geçilebilir mi?</summary>
    bool IsWalkable { get; }

    /// <summary>Tile yok edilmiş/çökmüş mü?</summary>
    bool IsDestroyed { get; }
}

/// <summary>
/// Tüm trap tile'ların ortak durumları.
/// Her tile tipi kendi state'lerini ekleyebilir ama base state'ler bunlardır.
/// </summary>
public enum TrapTileState
{
    /// <summary>Tile yerinde duruyor, henüz tetiklenmedi.</summary>
    Idle,

    /// <summary>Bir varlık üzerinde — aktif durumda (titreşim, hasar vs.).</summary>
    Triggered,

    /// <summary>Tile yıkılıyor/çöküyor — geçiş animasyonu devam ediyor.</summary>
    Collapsing,

    /// <summary>Tile tamamen yok oldu, artık geçilemez veya kaldırıldı.</summary>
    Destroyed
}
