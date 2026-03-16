using UnityEngine;

/// <summary>
/// Event-driven ses dinleyicisi. TrapTileEvents'e abone olarak
/// scaffold çökmesi, titreşim başlaması vb. olaylarda ses çalar.
/// Tile lojiğine dokunmadan çalışır — sadece event'leri dinler.
///
/// Kullanım: Sahneye boş bir GameObject ekle, bu scripti ata.
/// AudioManager zaten mevcutsa bu opsiyoneldir — ileride özel sesler eklemek için hazır.
/// </summary>
public class TrapTileAudioListener : MonoBehaviour
{
    void OnEnable()
    {
        TrapTileEvents.OnTileCollapsing += HandleCollapse;
        TrapTileEvents.OnTileShakeStarted += HandleShakeStart;
    }

    void OnDisable()
    {
        TrapTileEvents.OnTileCollapsing -= HandleCollapse;
        TrapTileEvents.OnTileShakeStarted -= HandleShakeStart;
    }

    private void HandleCollapse(Vector3Int cell)
    {
        // Mevcut AudioManager entegrasyonu ScaffoldManager içinde korunuyor.
        // İleride scaffold'a özel çökme sesi eklemek istersen buraya yaz:
        // if (AudioManager.instance != null) AudioManager.instance.PlayScaffoldCollapse();
    }

    private void HandleShakeStart(Vector3Int cell)
    {
        // İleride scaffold'a özel titreşim sesi eklemek istersen buraya yaz:
        // if (AudioManager.instance != null) AudioManager.instance.PlayScaffoldShake();
    }
}
