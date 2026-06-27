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
        TrapTileEvents.OnExplosionTileTriggered += HandleExplosion;
        TrapTileEvents.OnTeleportTileTriggered += HandleTeleport;
    }

    void OnDisable()
    {
        TrapTileEvents.OnTileCollapsing -= HandleCollapse;
        TrapTileEvents.OnTileShakeStarted -= HandleShakeStart;
        TrapTileEvents.OnExplosionTileTriggered -= HandleExplosion;
        TrapTileEvents.OnTeleportTileTriggered -= HandleTeleport;
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

    private void HandleExplosion(Vector3Int cell)
    {
        // Explosion tile ses: AnimateExplosionFX içinde PlayExplosion zaten çağrılıyor.
        // İleride farklı bir ses istersen buraya yaz:
        // if (AudioManager.instance != null) AudioManager.instance.PlayExplosion();
    }

    private void HandleTeleport(Vector3Int fromCell, Vector3Int toCell)
    {
        // Teleport tile ses:
        if (AudioManager.instance != null) AudioManager.instance.PlayTextEffect();
    }
}
