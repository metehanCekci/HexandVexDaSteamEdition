using System;
using UnityEngine;

public enum TrapTileState
{
    Idle,
    Triggered,
    Collapsing,
    Destroyed
}

/// <summary>
/// Tüm trap tile'ların yayınladığı event'ler.
/// Ses, animasyon, VFX gibi sistemler bu event'leri dinleyerek tile lojiğine girmeden çalışır.
/// </summary>
public static class TrapTileEvents
{
    /// <summary>Bir trap tile tetiklendiğinde (entity üzerine bastığında) ateşlenir. (cell, state)</summary>
    public static event Action<Vector3Int, TrapTileState> OnTileTriggered;

    /// <summary>Bir trap tile çökmeye/yıkılmaya başladığında ateşlenir. (cell)</summary>
    public static event Action<Vector3Int> OnTileCollapsing;

    /// <summary>Bir trap tile tamamen yok olduğunda ateşlenir. (cell)</summary>
    public static event Action<Vector3Int> OnTileDestroyed;

    /// <summary>Bir trap tile'da titreşim başladığında ateşlenir. (cell)</summary>
    public static event Action<Vector3Int> OnTileShakeStarted;

    /// <summary>Bir trap tile'da titreşim durduğunda ateşlenir. (cell)</summary>
    public static event Action<Vector3Int> OnTileShakeStopped;

    /// <summary>Bir explosion tile patladığında ateşlenir. (cell)</summary>
    public static event Action<Vector3Int> OnExplosionTileTriggered;

    /// <summary>Bir teleport tile tetiklendiğinde ateşlenir. (fromCell, toCell)</summary>
    public static event Action<Vector3Int, Vector3Int> OnTeleportTileTriggered;

    // ──────── Yardımcı Fire Metotları ────────

    public static void FireTileTriggered(Vector3Int cell, TrapTileState state)
    {
        OnTileTriggered?.Invoke(cell, state);
    }

    public static void FireTileCollapsing(Vector3Int cell)
    {
        OnTileCollapsing?.Invoke(cell);
    }

    public static void FireTileDestroyed(Vector3Int cell)
    {
        OnTileDestroyed?.Invoke(cell);
    }

    public static void FireTileShakeStarted(Vector3Int cell)
    {
        OnTileShakeStarted?.Invoke(cell);
    }

    public static void FireTileShakeStopped(Vector3Int cell)
    {
        OnTileShakeStopped?.Invoke(cell);
    }

    public static void FireExplosionTileTriggered(Vector3Int cell)
    {
        OnExplosionTileTriggered?.Invoke(cell);
    }

    public static void FireTeleportTileTriggered(Vector3Int fromCell, Vector3Int toCell)
    {
        OnTeleportTileTriggered?.Invoke(fromCell, toCell);
    }
}
